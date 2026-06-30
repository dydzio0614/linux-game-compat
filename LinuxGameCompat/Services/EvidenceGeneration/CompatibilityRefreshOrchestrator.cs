using System.Diagnostics;
using LinuxGameCompat.Data;
using LinuxGameCompat.Services.SummaryGeneration;
using Microsoft.EntityFrameworkCore;

namespace LinuxGameCompat.Services.EvidenceGeneration;

public sealed record CompatibilityRefreshRunResult(
	int Selected,
	int Succeeded,
	int Failed,
	int Skipped,
	int ChangedClaimGames,
	int GeneratedSummaries,
	TimeSpan Duration,
	int InputTokens,
	int OutputTokens,
	bool LockContended = false);

public sealed class CompatibilityRefreshOrchestrator(
	CompatibilityDbContext dbContext,
	EvidenceRefreshService evidenceRefresh,
	CompatibilitySummaryGenerator summaryGenerator,
	EvidenceGenerationOptions settings)
{
	private const long AdvisoryLockKey = 0x4C47434352454652;

	public async Task<CompatibilityRefreshRunResult> RunAsync(CompatibilityRefreshOptions options, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(options);
		if (options.Limit < 1 || options.Limit > settings.MaximumGames)
			throw new ArgumentOutOfRangeException(nameof(options), $"Limit must be between 1 and {settings.MaximumGames}.");

		Stopwatch stopwatch = Stopwatch.StartNew();
		await dbContext.Database.OpenConnectionAsync(cancellationToken);
		bool acquired = false;
		try
		{
			acquired = await dbContext.Database.SqlQueryRaw<bool>($"SELECT pg_try_advisory_lock({AdvisoryLockKey}) AS \"Value\"")
				.SingleAsync(cancellationToken);
			if (!acquired) return Empty(stopwatch.Elapsed, lockContended: true);

			List<RefreshCandidate> candidates = await LoadCandidatesAsync(options, cancellationToken);
			int succeeded = 0;
			int failed = 0;
			int skipped = 0;
			int changedClaimGames = 0;
			int generatedSummaries = 0;
			int inputTokens = 0;
			int outputTokens = 0;

			foreach (RefreshCandidate candidate in candidates)
			{
				cancellationToken.ThrowIfCancellationRequested();
				EvidenceRefreshResult evidence = candidate.HasSupportedSources
					? await evidenceRefresh.RefreshGameAsync(candidate.GameId, options.Force, cancellationToken)
					: new EvidenceRefreshResult(true, false, false, 0, 0);
				inputTokens += evidence.InputTokens;
				outputTokens += evidence.OutputTokens;
				if (!evidence.Succeeded) { failed++; continue; }
				if (evidence.ClaimsChanged) changedClaimGames++;

				bool needsSummary = options.Force || evidence.ClaimsChanged || candidate.SummaryNeedsWork;
				if (evidence.SummaryRestored && !options.Force) needsSummary = false;
				if (!needsSummary) { skipped++; succeeded++; continue; }
				SummaryGenerationResult summary = await summaryGenerator.GenerateGameAsync(candidate.GameId, options.Force, cancellationToken);
				inputTokens += summary.InputTokens;
				outputTokens += summary.OutputTokens;
				switch (summary.Outcome)
				{
					case SummaryGenerationOutcome.Generated: generatedSummaries++; succeeded++; break;
					case SummaryGenerationOutcome.Skipped: skipped++; succeeded++; break;
					case SummaryGenerationOutcome.Failed: failed++; break;
				}
			}

			return new CompatibilityRefreshRunResult(candidates.Count, succeeded, failed, skipped, changedClaimGames,
				generatedSummaries, stopwatch.Elapsed, inputTokens, outputTokens);
		}
		finally
		{
			if (acquired)
				await dbContext.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_unlock({AdvisoryLockKey})", CancellationToken.None);
			await dbContext.Database.CloseConnectionAsync();
		}
	}

	private async Task<List<RefreshCandidate>> LoadCandidatesAsync(CompatibilityRefreshOptions options, CancellationToken cancellationToken)
	{
		IQueryable<Game> query = dbContext.Games.AsNoTracking().AsSplitQuery().Where(game => !game.IsHidden);
		if (!string.IsNullOrWhiteSpace(options.Slug)) query = query.Where(game => game.Slug == options.Slug);
		List<Game> games = await query
			.Include(game => game.CompatibilitySummary)
			.Include(game => game.SourceReferences).ThenInclude(reference => reference.SourceSystem)
			.Include(game => game.SourceReferences).ThenInclude(reference => reference.ImportState)
			.Include(game => game.SourceReferences).ThenInclude(reference => reference.EvidenceClaims)
			.ToListAsync(cancellationToken);

		return games.Select(game =>
			{
				SourceReference[] supported = game.SourceReferences.Where(reference => IsSupported(reference.SourceSystem.Type)).ToArray();
				bool manualOnly = supported.Length == 0 && game.SourceReferences.Any() &&
					game.SourceReferences.All(reference => reference.SourceSystem.Type == SourceSystemType.Manual) &&
					game.SourceReferences.Any(reference => reference.EvidenceClaims.Any());
				bool summaryNeedsWork = game.CompatibilitySummary is null || game.CompatibilitySummary.IsStale ||
					game.CompatibilitySummary.State is SummaryState.Failed or SummaryState.Stale or SummaryState.NotGenerated;
				if (supported.Length == 0 && (!manualOnly || (!options.Force && !summaryNeedsWork))) return null;
				bool neverAttempted = supported.Length > 0
					? supported.Any(reference => reference.ImportState?.LastAttemptedAt is null)
					: game.CompatibilitySummary?.LastAttemptedAt is null;
				DateTimeOffset? workAge = supported.Length > 0
					? supported.Select(reference => reference.ImportState?.LastAttemptedAt).Min()
					: game.CompatibilitySummary?.LastAttemptedAt;
				return new RefreshCandidate(game.Id, supported.Length > 0, summaryNeedsWork, neverAttempted, workAge);
			})
			.Where(candidate => candidate is not null)
			.Cast<RefreshCandidate>()
			.OrderByDescending(candidate => candidate.NeverAttempted)
			.ThenBy(candidate => candidate.WorkAge)
			.ThenBy(candidate => candidate.GameId)
			.Take(options.Limit)
			.ToList();
	}

	private static bool IsSupported(SourceSystemType type) => type is SourceSystemType.ProtonDb or SourceSystemType.AreWeAntiCheatYet;
	private static CompatibilityRefreshRunResult Empty(TimeSpan duration, bool lockContended = false) =>
		new(0, 0, 0, 0, 0, 0, duration, 0, 0, lockContended);
	private sealed record RefreshCandidate(int GameId, bool HasSupportedSources, bool SummaryNeedsWork, bool NeverAttempted, DateTimeOffset? WorkAge);
}
