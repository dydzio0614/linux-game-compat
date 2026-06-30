using LinuxGameCompat.Data;
using LinuxGameCompat.Services.EvidenceGeneration;
using LinuxGameCompat.Services.SummaryGeneration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace LinuxGameCompat.Tests;

public sealed class PostgreSqlCompatibilityRefreshTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
	[Fact]
	public async Task Changed_claims_are_committed_before_summary_success()
	{
		await using CompatibilityDbContext dbContext = fixture.CreateDbContext();
		await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync();
		CountingClaimProvider claims = new();
		CountingSummaryProvider summaries = new();
		CompatibilityRefreshOrchestrator orchestrator = CreateOrchestrator(dbContext, new FixtureFactsProvider(), claims, summaries);

		CompatibilityRefreshRunResult result = await orchestrator.RunAsync(
			new CompatibilityRefreshOptions(1, "baldurs-gate-3"), CancellationToken.None);
		dbContext.ChangeTracker.Clear();
		Game game = await dbContext.Games.Include(item => item.CompatibilitySummary)
			.Include(item => item.SourceReferences).ThenInclude(reference => reference.EvidenceClaims)
			.SingleAsync(item => item.Id == 1);

		Assert.Equal(1, result.Selected);
		Assert.True(result.Succeeded == 1, $"succeeded={result.Succeeded} failed={result.Failed} summary={game.CompatibilitySummary?.ErrorCode}:{game.CompatibilitySummary?.ErrorMessage}");
		Assert.Equal(1, result.ChangedClaimGames);
		Assert.Equal(1, result.GeneratedSummaries);
		Assert.Equal(1, claims.CallCount);
		Assert.Equal(1, summaries.CallCount);
		Assert.Contains(game.SourceReferences.SelectMany(reference => reference.EvidenceClaims), claim => claim.ClaimValue == "Gold");
		Assert.Equal(SummaryState.Current, game.CompatibilitySummary!.State);
		await transaction.RollbackAsync();
	}

	[Fact]
	public async Task Summary_failure_retains_fresh_claims_and_last_good_prose()
	{
		await using CompatibilityDbContext dbContext = fixture.CreateDbContext();
		await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync();
		string? original = (await dbContext.GameCompatibilitySummaries.SingleAsync(item => item.GameId == 1)).SummaryText;
		CompatibilityRefreshOrchestrator orchestrator = CreateOrchestrator(
			dbContext, new FixtureFactsProvider(), new CountingClaimProvider(), new ThrowingSummaryProvider());

		CompatibilityRefreshRunResult result = await orchestrator.RunAsync(
			new CompatibilityRefreshOptions(1, "baldurs-gate-3"), CancellationToken.None);
		dbContext.ChangeTracker.Clear();
		GameCompatibilitySummary summary = await dbContext.GameCompatibilitySummaries.SingleAsync(item => item.GameId == 1);
		EvidenceClaim status = await dbContext.EvidenceClaims.SingleAsync(claim => claim.SourceReference.GameId == 1 && claim.ClaimType == EvidenceClaimType.Status);

		Assert.Equal(1, result.Failed);
		Assert.Equal("Gold", status.ClaimValue);
		Assert.Equal(original, summary.SummaryText);
		Assert.Equal(SummaryState.Failed, summary.State);
		Assert.True(summary.IsStale);
		await transaction.RollbackAsync();
	}

	[Fact]
	public async Task Unchanged_rerun_is_a_provider_noop_and_force_bypasses_freshness()
	{
		await using CompatibilityDbContext dbContext = fixture.CreateDbContext();
		await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync();
		CountingClaimProvider claims = new();
		CountingSummaryProvider summaries = new();
		CompatibilityRefreshOrchestrator orchestrator = CreateOrchestrator(dbContext, new FixtureFactsProvider(), claims, summaries);

		await orchestrator.RunAsync(new CompatibilityRefreshOptions(1, "baldurs-gate-3"), CancellationToken.None);
		int claimCalls = claims.CallCount;
		int summaryCalls = summaries.CallCount;
		CompatibilityRefreshRunResult unchanged = await orchestrator.RunAsync(
			new CompatibilityRefreshOptions(1, "baldurs-gate-3"), CancellationToken.None);
		CompatibilityRefreshRunResult forced = await orchestrator.RunAsync(
			new CompatibilityRefreshOptions(1, "baldurs-gate-3", true), CancellationToken.None);

		Assert.Equal(claimCalls, claims.CallCount - 1);
		Assert.Equal(summaryCalls, summaries.CallCount - 1);
		Assert.Equal(0, unchanged.ChangedClaimGames);
		Assert.Equal(0, unchanged.GeneratedSummaries);
		Assert.Equal(1, unchanged.Skipped);
		Assert.Equal(1, forced.GeneratedSummaries);
		await transaction.RollbackAsync();
	}

	[Fact]
	public async Task Refresh_lock_contention_returns_successful_no_work()
	{
		await using CompatibilityDbContext lockContext = fixture.CreateDbContext();
		await lockContext.Database.OpenConnectionAsync();
		await lockContext.Database.ExecuteSqlRawAsync("SELECT pg_advisory_lock(5496435826651121234)");
		try
		{
			await using CompatibilityDbContext dbContext = fixture.CreateDbContext();
			CompatibilityRefreshOrchestrator orchestrator = CreateOrchestrator(
				dbContext, new FixtureFactsProvider(), new CountingClaimProvider(), new CountingSummaryProvider());
			CompatibilityRefreshRunResult result = await orchestrator.RunAsync(new CompatibilityRefreshOptions(10), CancellationToken.None);

			Assert.True(result.LockContended);
			Assert.Equal(0, result.Selected);
			Assert.Equal(0, RefreshCompatibilityCommand.ExitCodeFor(result));
		}
		finally
		{
			await lockContext.Database.ExecuteSqlRawAsync("SELECT pg_advisory_unlock(5496435826651121234)");
		}
	}

	[Fact]
	public async Task Refresh_propagates_requested_cancellation()
	{
		await using CompatibilityDbContext dbContext = fixture.CreateDbContext();
		CompatibilityRefreshOrchestrator orchestrator = CreateOrchestrator(
			dbContext, new FixtureFactsProvider(), new CountingClaimProvider(), new CountingSummaryProvider());
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
			orchestrator.RunAsync(new CompatibilityRefreshOptions(1, "baldurs-gate-3"), cancellation.Token));
	}

	private static CompatibilityRefreshOrchestrator CreateOrchestrator(
		CompatibilityDbContext dbContext,
		IEvidenceSourceFactsProvider facts,
		IEvidenceClaimProvider claimProvider,
		ICompatibilitySummaryProvider summaryProvider)
	{
		EvidenceGenerationOptions evidenceOptions = EvidenceSourceAdapterTests.ValidOptions();
		EvidenceClaimMaterializer materializer = new(claimProvider, new EvidenceClaimPromptBuilder(new ZeroEvidenceTokenCounter()), evidenceOptions);
		EvidenceRefreshService evidenceRefresh = new(dbContext, facts, materializer, TimeProvider.System);
		CompatibilitySummaryGenerator summaryGenerator = new(dbContext, summaryProvider,
			new EvidencePromptBuilder(new ZeroSummaryTokenCounter()), SummaryGenerationOptionsHelper.FromAppSettings(), TimeProvider.System);
		return new CompatibilityRefreshOrchestrator(dbContext, evidenceRefresh, summaryGenerator, evidenceOptions);
	}

	private sealed class ZeroEvidenceTokenCounter : IEvidenceClaimTokenCounter { public int Count(string text) => 0; }
	private sealed class ZeroSummaryTokenCounter : IGenerationTokenCounter { public int Count(string text) => 0; }
	private sealed class FixtureFactsProvider : IEvidenceSourceFactsProvider
	{
		public Task<NormalizedSourceFacts> FetchAsync(SourceSystemType sourceType, SourceReferenceInput source, CancellationToken cancellationToken)
		{
			string status = sourceType == SourceSystemType.ProtonDb ? "Gold" : "Running";
			return Task.FromResult(new NormalizedSourceFacts(source.SourceGameId, status, "fixture-v1", $"REFRESH-{source.SourceGameId}", $"{{\"status\":\"{status}\"}}"));
		}
	}
	private sealed class CountingClaimProvider : IEvidenceClaimProvider
	{
		public int CallCount { get; private set; }
		public Task<EvidenceClaimProviderResult> GenerateAsync(EvidenceClaimProviderRequest request, CancellationToken cancellationToken)
		{
			CallCount++;
			return Task.FromResult(new EvidenceClaimProviderResult([new(EvidenceClaimType.Note, "Fixture", "Grounded fixture note.")], 3, 2));
		}
	}
	private sealed class CountingSummaryProvider : ICompatibilitySummaryProvider
	{
		public int CallCount { get; private set; }
		public Task<CompatibilitySummaryProviderResult> GenerateAsync(CompatibilitySummaryProviderRequest request, CancellationToken cancellationToken)
		{
			CallCount++;
			return Task.FromResult(new CompatibilitySummaryProviderResult(CompatibilityStatus.Playable, "Fresh summary.", 5, 3));
		}
	}
	private sealed class ThrowingSummaryProvider : ICompatibilitySummaryProvider
	{
		public Task<CompatibilitySummaryProviderResult> GenerateAsync(CompatibilitySummaryProviderRequest request, CancellationToken cancellationToken) =>
			throw new HttpRequestException("temporary failure");
	}
}
