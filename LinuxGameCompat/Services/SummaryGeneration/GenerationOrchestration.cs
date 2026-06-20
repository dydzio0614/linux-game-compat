using System.Diagnostics;
using System.Data;
using LinuxGameCompat.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace LinuxGameCompat.Services.SummaryGeneration;

public sealed record SummaryGenerationRunOptions(int Limit, string? Slug = null, bool Force = false);
public sealed record SummaryGenerationRunResult(int Selected, int Succeeded, int Failed, int Skipped,
	TimeSpan Duration, int InputTokens, int OutputTokens, bool LockContended = false);

public interface ICompatibilitySummaryGenerator
{
	Task<SummaryGenerationRunResult> RunAsync(SummaryGenerationRunOptions options, CancellationToken cancellationToken);
}

public sealed class CompatibilitySummaryGenerator(
	CompatibilityDbContext dbContext,
	ICompatibilitySummaryProvider provider,
	EvidencePromptBuilder promptBuilder,
	GenerationOptions settings,
	TimeProvider timeProvider) : ICompatibilitySummaryGenerator
{
	private const long AdvisoryLockKey = 0x4C474353554D4D41;

	public async Task<SummaryGenerationRunResult> RunAsync(SummaryGenerationRunOptions options, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(options);
		if (options.Limit is < 1 or > 10) throw new ArgumentOutOfRangeException(nameof(options), "Limit must be between 1 and 10.");
		Stopwatch stopwatch = Stopwatch.StartNew();
		await dbContext.Database.OpenConnectionAsync(cancellationToken);
		bool acquired = false;
		try
		{
			acquired = await dbContext.Database.SqlQueryRaw<bool>($"SELECT pg_try_advisory_lock({AdvisoryLockKey}) AS \"Value\"")
				.SingleAsync(cancellationToken);
			if (!acquired) return new SummaryGenerationRunResult(0, 0, 0, 0, stopwatch.Elapsed, 0, 0, true);

			List<Candidate> candidates = await LoadCandidatesAsync(options.Slug, cancellationToken);
			int skipped = 0;
			List<Candidate> eligible = [];
			foreach (Candidate candidate in candidates)
			{
				CanonicalEvidence evidence = EvidencePromptBuilder.Canonicalize(candidate.Claims);
				bool mismatch = candidate.Summary is not null &&
					(!string.Equals(candidate.Summary.EvidenceHash, evidence.Hash, StringComparison.Ordinal) ||
					 !string.Equals(candidate.Summary.EvidenceVersion, CanonicalEvidence.ContractVersion, StringComparison.Ordinal));
				if (mismatch)
				{
					candidate.Summary!.IsStale = true;
					if (candidate.Summary.State == SummaryState.Current) candidate.Summary.State = SummaryState.Stale;
				}
				bool shouldGenerate = options.Force || candidate.Summary is null || mismatch || candidate.Summary.IsStale ||
					candidate.Summary.State is SummaryState.Failed or SummaryState.Stale or SummaryState.NotGenerated;
				if (shouldGenerate) eligible.Add(candidate with { Evidence = evidence }); else skipped++;
			}
			await dbContext.SaveChangesAsync(cancellationToken);
			eligible = eligible.OrderBy(candidate => candidate.Summary?.LastAttemptedAt.HasValue == true)
				.ThenBy(candidate => candidate.Summary?.LastAttemptedAt)
				.ThenBy(candidate => candidate.Game.Id)
				.Take(Math.Min(options.Limit, settings.MaximumGames)).ToList();

			int succeeded = 0, failed = 0, inputTokens = 0, outputTokens = 0;
			foreach (Candidate candidate in eligible)
			{
				cancellationToken.ThrowIfCancellationRequested();
				DateTimeOffset attemptedAt = timeProvider.GetUtcNow();
				GameCompatibilitySummary summary = candidate.Summary ?? new GameCompatibilitySummary { GameId = candidate.Game.Id };
				if (candidate.Summary is null) dbContext.GameCompatibilitySummaries.Add(summary);
				summary.LastAttemptedAt = attemptedAt;
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
						// These short-lived locks close the final recheck/write race without spanning the provider call.
						await dbContext.Database.ExecuteSqlRawAsync("LOCK TABLE \"SourceSystems\" IN SHARE MODE", cancellationToken);
						await dbContext.Database.ExecuteSqlRawAsync("LOCK TABLE \"SourceReferences\" IN SHARE MODE", cancellationToken);
						await dbContext.Database.ExecuteSqlRawAsync("LOCK TABLE \"EvidenceClaims\" IN SHARE MODE", cancellationToken);
						Candidate? refreshed = (await LoadCandidatesAsync(candidate.Game.Slug, cancellationToken)).SingleOrDefault();
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
						await MarkFailureAsync(candidate.Game.Id, attemptedAt, "evidence_changed", "Evidence changed during generation; output was discarded.", cancellationToken);
						failed++;
						continue;
					}
					succeeded++; inputTokens += result.InputTokens; outputTokens += result.OutputTokens;
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
				catch (Exception exception)
				{
					dbContext.ChangeTracker.Clear();
					await MarkFailureAsync(candidate.Game.Id, attemptedAt, ErrorCode(exception), Sanitize(exception.Message), cancellationToken);
					failed++;
				}
			}
			return new SummaryGenerationRunResult(eligible.Count, succeeded, failed, skipped, stopwatch.Elapsed, inputTokens, outputTokens);
		}
		finally
		{
			if (acquired)
				await dbContext.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_unlock({AdvisoryLockKey})", CancellationToken.None);
			await dbContext.Database.CloseConnectionAsync();
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

	private async Task<List<Candidate>> LoadCandidatesAsync(string? slug, CancellationToken cancellationToken)
	{
		IQueryable<Game> query = dbContext.Games.AsSplitQuery().Where(game => !game.IsHidden && game.SourceReferences.Any(reference => reference.EvidenceClaims.Any()));
		if (!string.IsNullOrWhiteSpace(slug)) query = query.Where(game => game.Slug == slug);
		return (await query.Include(game => game.CompatibilitySummary)
			.Include(game => game.SourceReferences).ThenInclude(reference => reference.SourceSystem)
			.Include(game => game.SourceReferences).ThenInclude(reference => reference.EvidenceClaims)
			.ToListAsync(cancellationToken)).Select(game => new Candidate(game, game.CompatibilitySummary, ToClaims(game), null)).ToList();
	}

	private static List<GenerationEvidenceClaim> ToClaims(Game game) => game.SourceReferences.SelectMany(reference => reference.EvidenceClaims.Select(claim =>
		new GenerationEvidenceClaim(claim.Id, claim.ClaimType, claim.ClaimValue, claim.ClaimText, claim.ObservedAt,
			reference.SourceSystem.Type, reference.SourceSystem.Name, reference.SourceGameId, reference.Url))).ToList();
	private static string ErrorCode(Exception exception) => exception is CompatibilitySummaryProviderException provider ? $"provider_{provider.Kind.ToString().ToLowerInvariant()}" : "generation_failed";
	private static string Sanitize(string message) => string.IsNullOrWhiteSpace(message) ? "Summary generation failed." : message.Trim()[..Math.Min(message.Trim().Length, 2000)];
	private sealed record Candidate(Game Game, GameCompatibilitySummary? Summary, List<GenerationEvidenceClaim> Claims, CanonicalEvidence? Evidence);
}
