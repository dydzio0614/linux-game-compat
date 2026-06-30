using System.Data;
using LinuxGameCompat.Data;
using LinuxGameCompat.Services.SummaryGeneration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace LinuxGameCompat.Services.EvidenceGeneration;

public sealed record EvidenceRefreshResult(bool Succeeded, bool ClaimsChanged, bool SummaryRestored, int InputTokens, int OutputTokens, string? ErrorCode = null);

public sealed class EvidenceRefreshService(
	CompatibilityDbContext dbContext,
	IEvidenceSourceFactsProvider sourceFactsProvider,
	EvidenceClaimMaterializer materializer,
	TimeProvider timeProvider)
{
	public async Task<EvidenceRefreshResult> RefreshGameAsync(int gameId, bool force, CancellationToken cancellationToken)
	{
		Game? game = await LoadGameAsync(gameId, cancellationToken);
		if (game is null) return new EvidenceRefreshResult(false, false, false, 0, 0, "game_not_found");
		SourceReference[] supported = game.SourceReferences.Where(reference => IsSupported(reference.SourceSystem.Type)).OrderBy(reference => reference.Id).ToArray();
		if (supported.Length == 0) return new EvidenceRefreshResult(true, false, false, 0, 0);

		DateTimeOffset attemptedAt = timeProvider.GetUtcNow();
		List<PreparedReference> prepared = [];
		int inputTokens = 0;
		int outputTokens = 0;
		try
		{
			foreach (SourceReference reference in supported)
			{
				NormalizedSourceFacts facts = await sourceFactsProvider.FetchAsync(reference.SourceSystem.Type,
					new SourceReferenceInput(reference.SourceGameId, reference.Url), cancellationToken);
				string contractVersion = EvidenceClaimMaterializer.ContractVersion(facts);
				bool current = !force && reference.ImportState is not null &&
					string.Equals(reference.ImportState.ContentHash, facts.ContentHash, StringComparison.Ordinal) &&
					string.Equals(reference.ImportState.ContractVersion, contractVersion, StringComparison.Ordinal);
				MaterializedEvidenceClaims? generated = current ? null : await materializer.GenerateAsync(reference.SourceSystem.Name, facts, cancellationToken);
				inputTokens += generated?.InputTokens ?? 0;
				outputTokens += generated?.OutputTokens ?? 0;
				prepared.Add(new PreparedReference(reference.Id, reference.SourceSystem.Type, reference.SourceGameId, reference.Url, facts, generated));
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
		catch (Exception exception)
		{
			string code = exception switch
			{
				EvidenceSourceException source => source.Code,
				EvidenceClaimProviderException provider => $"provider_{provider.Code}",
				_ => "refresh_failed"
			};
			string message = exception switch
			{
				EvidenceSourceException => Sanitize(exception.Message),
				EvidenceClaimProviderException => Sanitize(exception.Message),
				_ => "Evidence refresh failed."
			};
			await RecordFailureAsync(gameId, attemptedAt, code, message, cancellationToken);
			return new EvidenceRefreshResult(false, false, false, inputTokens, outputTokens, code);
		}

		dbContext.ChangeTracker.Clear();
		IDbContextTransaction? transaction = dbContext.Database.CurrentTransaction;
		bool ownsTransaction = transaction is null;
		transaction ??= await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
		bool identityChanged = false;
		bool claimsChanged = false;
		bool restored = false;
		try
		{
			await dbContext.Database.ExecuteSqlRawAsync("LOCK TABLE \"SourceReferences\" IN SHARE MODE", cancellationToken);
			await dbContext.Database.ExecuteSqlRawAsync("LOCK TABLE \"EvidenceClaims\" IN SHARE ROW EXCLUSIVE MODE", cancellationToken);
			Game? trackedGame = await LoadGameAsync(gameId, cancellationToken);
			if (trackedGame is null || !IdentitiesMatch(trackedGame, prepared))
			{
				identityChanged = true;
				if (ownsTransaction) await transaction.RollbackAsync(cancellationToken);
			}
			else
			{
				foreach (PreparedReference item in prepared)
				{
					SourceReference reference = trackedGame.SourceReferences.Single(candidate => candidate.Id == item.ReferenceId);
					if (item.Generated is not null) claimsChanged |= Reconcile(reference, item.Generated.Claims, attemptedAt, dbContext);
					SourceReferenceImportState state = reference.ImportState ?? new SourceReferenceImportState { SourceReferenceId = reference.Id };
					if (reference.ImportState is null) dbContext.SourceReferenceImportStates.Add(state);
					state.ContentHash = item.Facts.ContentHash;
					state.ContractVersion = EvidenceClaimMaterializer.ContractVersion(item.Facts);
					state.LastAttemptedAt = attemptedAt;
					state.LastSucceededAt = attemptedAt;
					state.ETag = Bound(item.Facts.ETag, 512);
					state.LastModifiedAt = item.Facts.LastModifiedAt;
					state.ErrorCode = null;
					state.ErrorMessage = null;
				}

				GameCompatibilitySummary? summary = trackedGame.CompatibilitySummary;
				if (claimsChanged && summary is not null)
				{
					summary.IsStale = true;
					if (summary.State == SummaryState.Current) summary.State = SummaryState.Stale;
				}
				await dbContext.SaveChangesAsync(cancellationToken);

				if (!claimsChanged && summary?.State == SummaryState.Stale && !string.IsNullOrWhiteSpace(summary.SummaryText))
				{
					CanonicalEvidence evidence = EvidencePromptBuilder.Canonicalize(MapClaims(trackedGame));
					if (string.Equals(summary.EvidenceVersion, CanonicalEvidence.ContractVersion, StringComparison.Ordinal) &&
						string.Equals(summary.EvidenceHash, evidence.Hash, StringComparison.Ordinal))
					{
						summary.State = SummaryState.Current;
						summary.IsStale = false;
						summary.ErrorCode = null;
						summary.ErrorMessage = null;
						restored = true;
						await dbContext.SaveChangesAsync(cancellationToken);
					}
				}
				if (ownsTransaction) await transaction.CommitAsync(cancellationToken);
			}
		}
		finally
		{
			if (ownsTransaction) await transaction.DisposeAsync();
		}
		if (identityChanged)
		{
			await RecordFailureAsync(gameId, attemptedAt, "source_identity_changed", "Source identity changed during refresh.", cancellationToken);
			return new EvidenceRefreshResult(false, false, false, inputTokens, outputTokens, "source_identity_changed");
		}
		return new EvidenceRefreshResult(true, claimsChanged, restored, inputTokens, outputTokens);
	}

	private async Task RecordFailureAsync(int gameId, DateTimeOffset attemptedAt, string code, string message, CancellationToken cancellationToken)
	{
		dbContext.ChangeTracker.Clear();
		IDbContextTransaction? transaction = dbContext.Database.CurrentTransaction;
		bool ownsTransaction = transaction is null;
		transaction ??= await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
		try
		{
			await dbContext.Database.ExecuteSqlRawAsync("LOCK TABLE \"SourceReferences\" IN SHARE MODE", cancellationToken);
			SourceReference[] references = await dbContext.SourceReferences
				.Include(reference => reference.ImportState)
				.Where(reference => reference.GameId == gameId &&
					(reference.SourceSystem.Type == SourceSystemType.ProtonDb || reference.SourceSystem.Type == SourceSystemType.AreWeAntiCheatYet))
				.ToArrayAsync(cancellationToken);
			foreach (SourceReference reference in references)
			{
				SourceReferenceImportState state = reference.ImportState ?? new SourceReferenceImportState { SourceReferenceId = reference.Id };
				if (reference.ImportState is null) dbContext.SourceReferenceImportStates.Add(state);
				state.LastAttemptedAt = attemptedAt;
				state.ErrorCode = Bound(code, 80);
				state.ErrorMessage = Bound(message, 2000);
			}
			GameCompatibilitySummary? summary = await dbContext.GameCompatibilitySummaries.SingleOrDefaultAsync(item => item.GameId == gameId, cancellationToken);
			if (summary is not null)
			{
				summary.IsStale = true;
				if (summary.State == SummaryState.Current) summary.State = SummaryState.Stale;
			}
			await dbContext.SaveChangesAsync(cancellationToken);
			if (ownsTransaction) await transaction.CommitAsync(cancellationToken);
		}
		finally
		{
			if (ownsTransaction) await transaction.DisposeAsync();
		}
	}

	private Task<Game?> LoadGameAsync(int gameId, CancellationToken cancellationToken) => dbContext.Games.AsSplitQuery()
		.Include(game => game.CompatibilitySummary)
		.Include(game => game.SourceReferences).ThenInclude(reference => reference.SourceSystem)
		.Include(game => game.SourceReferences).ThenInclude(reference => reference.ImportState)
		.Include(game => game.SourceReferences).ThenInclude(reference => reference.EvidenceClaims)
		.SingleOrDefaultAsync(game => game.Id == gameId, cancellationToken);

	private static bool Reconcile(SourceReference reference, IReadOnlyList<GeneratedEvidenceClaim> generated, DateTimeOffset observedAt, CompatibilityDbContext dbContext)
	{
		List<EvidenceClaim> unmatched = reference.EvidenceClaims.ToList();
		List<GeneratedEvidenceClaim> additions = [];
		foreach (GeneratedEvidenceClaim candidate in generated)
		{
			EvidenceClaim? match = unmatched.FirstOrDefault(claim => claim.ClaimType == candidate.ClaimType &&
				claim.ClaimValue == candidate.ClaimValue && claim.ClaimText == candidate.ClaimText);
			if (match is null) additions.Add(candidate); else unmatched.Remove(match);
		}
		if (unmatched.Count == 0 && additions.Count == 0) return false;
		dbContext.EvidenceClaims.RemoveRange(unmatched);
		foreach (GeneratedEvidenceClaim addition in additions)
			reference.EvidenceClaims.Add(new EvidenceClaim { ClaimType = addition.ClaimType, ClaimValue = addition.ClaimValue, ClaimText = addition.ClaimText, ObservedAt = observedAt });
		return true;
	}

	private static bool IdentitiesMatch(Game game, IReadOnlyList<PreparedReference> prepared)
	{
		SourceReference[] current = game.SourceReferences.Where(reference => IsSupported(reference.SourceSystem.Type)).OrderBy(reference => reference.Id).ToArray();
		return current.Length == prepared.Count && current.Zip(prepared).All(pair => pair.First.Id == pair.Second.ReferenceId &&
			pair.First.SourceSystem.Type == pair.Second.SourceType && pair.First.SourceGameId == pair.Second.SourceGameId && pair.First.Url == pair.Second.Url);
	}

	private static IEnumerable<GenerationEvidenceClaim> MapClaims(Game game) => game.SourceReferences.SelectMany(reference => reference.EvidenceClaims.Select(claim =>
		new GenerationEvidenceClaim(claim.Id, claim.ClaimType, claim.ClaimValue, claim.ClaimText, claim.ObservedAt,
			reference.SourceSystem.Type, reference.SourceSystem.Name, reference.SourceGameId, reference.Url)));

	private static bool IsSupported(SourceSystemType type) => type is SourceSystemType.ProtonDb or SourceSystemType.AreWeAntiCheatYet;
	private static string Sanitize(string? message) => Bound(string.IsNullOrWhiteSpace(message) ? "Evidence refresh failed." : message.Trim(), 2000)!;
	private static string? Bound(string? value, int maximum) => value is null ? null : value[..Math.Min(value.Length, maximum)];
	private sealed record PreparedReference(int ReferenceId, SourceSystemType SourceType, string SourceGameId, string Url, NormalizedSourceFacts Facts, MaterializedEvidenceClaims? Generated);
}
