using System.Data;
using LinuxGameCompat.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace LinuxGameCompat.Services.SummaryGeneration;

public enum SummaryGenerationOutcome { Generated, Skipped, Failed }

public sealed record SummaryGenerationResult(SummaryGenerationOutcome Outcome, int InputTokens = 0, int OutputTokens = 0);

public sealed class CompatibilitySummaryGenerator(
	CompatibilityDbContext dbContext,
	ICompatibilitySummaryProvider provider,
	EvidencePromptBuilder promptBuilder,
	GenerationOptions settings,
	TimeProvider timeProvider)
{
	public async Task<SummaryGenerationResult> GenerateGameAsync(int gameId, bool force, CancellationToken cancellationToken)
	{
		// Re-read persisted evidence so provider input uses the database-normalized timestamps
		// that the post-provider race check will observe.
		dbContext.ChangeTracker.Clear();
		Candidate? candidate = await LoadCandidateAsync(gameId, cancellationToken);
		if (candidate is null) return new SummaryGenerationResult(SummaryGenerationOutcome.Skipped);

		CanonicalEvidence evidence = EvidencePromptBuilder.Canonicalize(candidate.Claims);
		bool mismatch = candidate.Summary is not null &&
			(!string.Equals(candidate.Summary.EvidenceHash, evidence.Hash, StringComparison.Ordinal) ||
			 !string.Equals(candidate.Summary.EvidenceVersion, CanonicalEvidence.ContractVersion, StringComparison.Ordinal));
		if (mismatch)
		{
			candidate.Summary!.IsStale = true;
			if (candidate.Summary.State == SummaryState.Current) candidate.Summary.State = SummaryState.Stale;
			await dbContext.SaveChangesAsync(cancellationToken);
		}
		bool shouldGenerate = force || candidate.Summary is null || mismatch || candidate.Summary.IsStale ||
			candidate.Summary.State is SummaryState.Failed or SummaryState.Stale or SummaryState.NotGenerated;
		if (!shouldGenerate) return new SummaryGenerationResult(SummaryGenerationOutcome.Skipped);

		DateTimeOffset attemptedAt = timeProvider.GetUtcNow();
		try
		{
			PromptSelection selection = promptBuilder.Build(candidate.Claims, settings.MaximumClaims, settings.MaximumInputTokens);
			CompatibilitySummaryProviderResult result = await provider.GenerateAsync(
				new CompatibilitySummaryProviderRequest(settings.Model, selection.Prompt, settings.MaximumOutputTokens), cancellationToken);
			dbContext.ChangeTracker.Clear();
			bool evidenceChanged;
			IDbContextTransaction? transaction = dbContext.Database.CurrentTransaction;
			bool ownsTransaction = transaction is null;
			transaction ??= await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
			try
			{
				await dbContext.Database.ExecuteSqlRawAsync("LOCK TABLE \"SourceSystems\" IN SHARE MODE", cancellationToken);
				await dbContext.Database.ExecuteSqlRawAsync("LOCK TABLE \"SourceReferences\" IN SHARE MODE", cancellationToken);
				await dbContext.Database.ExecuteSqlRawAsync("LOCK TABLE \"EvidenceClaims\" IN SHARE MODE", cancellationToken);
				Candidate? refreshed = await LoadCandidateAsync(gameId, cancellationToken);
				evidenceChanged = refreshed is null || EvidencePromptBuilder.Canonicalize(refreshed.Claims).Hash != selection.Evidence.Hash;
				if (evidenceChanged)
				{
					if (ownsTransaction) await transaction.RollbackAsync(cancellationToken);
				}
				else
				{
					Game trackedGame = refreshed!.Game;
					GameCompatibilitySummary trackedSummary = refreshed.Summary ?? new GameCompatibilitySummary { GameId = trackedGame.Id };
					if (refreshed.Summary is null) dbContext.GameCompatibilitySummaries.Add(trackedSummary);
					CompatibilityStatus? deterministic = NativeStatusNormalizer.Reduce(refreshed.Claims
						.Where(claim => claim.ClaimType == EvidenceClaimType.Status)
						.Select(claim => new NativeStatusEvidence(claim.SourceType, claim.ClaimValue)));
					trackedSummary.State = SummaryState.Current;
					trackedSummary.SummaryStatus = result.Status;
					trackedSummary.SummaryText = result.Summary;
					trackedSummary.Provider = settings.Provider;
					trackedSummary.Model = settings.Model;
					trackedSummary.EvidenceVersion = CanonicalEvidence.ContractVersion;
					trackedSummary.EvidenceHash = selection.Evidence.Hash;
					trackedSummary.GeneratedAt = timeProvider.GetUtcNow();
					trackedSummary.LastAttemptedAt = attemptedAt;
					trackedSummary.InputTokenCount = result.InputTokens;
					trackedSummary.OutputTokenCount = result.OutputTokens;
					trackedSummary.IsStale = false;
					trackedSummary.ErrorCode = null;
					trackedSummary.ErrorMessage = null;
					trackedGame.CompatibilityStatus = deterministic ?? result.Status;
					trackedGame.UpdatedAt = timeProvider.GetUtcNow();
					await dbContext.SaveChangesAsync(cancellationToken);
					if (ownsTransaction) await transaction.CommitAsync(cancellationToken);
				}
			}
			finally
			{
				if (ownsTransaction) await transaction.DisposeAsync();
			}
			if (evidenceChanged)
			{
				dbContext.ChangeTracker.Clear();
				await MarkFailureAsync(gameId, attemptedAt, "evidence_changed", "Evidence changed during generation; output was discarded.", cancellationToken);
				return new SummaryGenerationResult(SummaryGenerationOutcome.Failed);
			}
			return new SummaryGenerationResult(SummaryGenerationOutcome.Generated, result.InputTokens, result.OutputTokens);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
		catch (Exception exception)
		{
			dbContext.ChangeTracker.Clear();
			string errorCode = exception is CompatibilitySummaryProviderException providerException ? $"provider_{providerException.Kind.ToString().ToLowerInvariant()}" : "generation_failed";
			string text = string.IsNullOrWhiteSpace(exception.Message) ? "Summary generation failed." : exception.Message.Trim();
			await MarkFailureAsync(gameId, attemptedAt, errorCode, text[..Math.Min(text.Length, 2000)], cancellationToken);
			return new SummaryGenerationResult(SummaryGenerationOutcome.Failed);
		}
	}

	private async Task MarkFailureAsync(int gameId, DateTimeOffset attemptedAt, string code, string message, CancellationToken cancellationToken)
	{
		GameCompatibilitySummary? summary = await dbContext.GameCompatibilitySummaries.SingleOrDefaultAsync(item => item.GameId == gameId, cancellationToken);
		if (summary is null)
		{
			summary = new GameCompatibilitySummary { GameId = gameId };
			dbContext.GameCompatibilitySummaries.Add(summary);
		}
		summary.State = SummaryState.Failed;
		summary.IsStale = true;
		summary.LastAttemptedAt = attemptedAt;
		summary.ErrorCode = code;
		summary.ErrorMessage = message;
		await dbContext.SaveChangesAsync(cancellationToken);
	}

	private async Task<Candidate?> LoadCandidateAsync(int gameId, CancellationToken cancellationToken)
	{
		Game? game = await dbContext.Games.AsSplitQuery()
			.Where(item => item.Id == gameId && !item.IsHidden && item.SourceReferences.Any(reference => reference.EvidenceClaims.Any()))
			.Include(item => item.CompatibilitySummary)
			.Include(item => item.SourceReferences).ThenInclude(reference => reference.SourceSystem)
			.Include(item => item.SourceReferences).ThenInclude(reference => reference.EvidenceClaims)
			.SingleOrDefaultAsync(cancellationToken);
		if (game is null) return null;
		List<GenerationEvidenceClaim> claims = game.SourceReferences.SelectMany(reference => reference.EvidenceClaims.Select(claim =>
			new GenerationEvidenceClaim(claim.Id, claim.ClaimType, claim.ClaimValue, claim.ClaimText, claim.ObservedAt,
				reference.SourceSystem.Type, reference.SourceSystem.Name, reference.SourceGameId, reference.Url))).ToList();
		return new Candidate(game, game.CompatibilitySummary, claims);
	}

	private sealed record Candidate(Game Game, GameCompatibilitySummary? Summary, List<GenerationEvidenceClaim> Claims);
}
