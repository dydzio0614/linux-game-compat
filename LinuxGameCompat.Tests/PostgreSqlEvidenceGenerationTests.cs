using LinuxGameCompat.Data;
using LinuxGameCompat.Services.EvidenceGeneration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace LinuxGameCompat.Tests;

public sealed class PostgreSqlEvidenceGenerationTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
	[Fact]
	public async Task Unchanged_refresh_skips_provider_and_preserves_claim_ids_and_timestamps()
	{
		await using CompatibilityDbContext dbContext = fixture.CreateDbContext();
		await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync();
		FakeFactsProvider facts = new();
		CountingClaimProvider provider = new();
		EvidenceRefreshService service = CreateService(dbContext, facts, provider);

		EvidenceRefreshResult first = await service.RefreshGameAsync(1, false, CancellationToken.None);
		dbContext.ChangeTracker.Clear();
		EvidenceClaim[] before = await dbContext.EvidenceClaims.Where(claim => claim.SourceReference.GameId == 1).OrderBy(claim => claim.Id).ToArrayAsync();
		EvidenceRefreshResult second = await service.RefreshGameAsync(1, false, CancellationToken.None);
		dbContext.ChangeTracker.Clear();
		EvidenceClaim[] after = await dbContext.EvidenceClaims.Where(claim => claim.SourceReference.GameId == 1).OrderBy(claim => claim.Id).ToArrayAsync();

		Assert.True(first.ClaimsChanged);
		Assert.False(second.ClaimsChanged);
		Assert.Equal(1, provider.CallCount);
		Assert.Equal(before.Select(claim => (claim.Id, claim.ObservedAt)), after.Select(claim => (claim.Id, claim.ObservedAt)));
		await transaction.RollbackAsync();
	}

	[Fact]
	public async Task Multi_reference_failure_preserves_all_claims_and_marks_current_summary_stale()
	{
		await using CompatibilityDbContext dbContext = fixture.CreateDbContext();
		await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync();
		GameCompatibilitySummary existingSummary = await dbContext.GameCompatibilitySummaries.SingleAsync(item => item.GameId == 2);
		existingSummary.State = SummaryState.Current;
		existingSummary.IsStale = false;
		await dbContext.SaveChangesAsync();
		EvidenceClaim[] before = await dbContext.EvidenceClaims.Where(claim => claim.SourceReference.GameId == 2).OrderBy(claim => claim.Id).AsNoTracking().ToArrayAsync();
		FakeFactsProvider facts = new() { FailingSource = SourceSystemType.AreWeAntiCheatYet };
		EvidenceRefreshService service = CreateService(dbContext, facts, new CountingClaimProvider());

		EvidenceRefreshResult result = await service.RefreshGameAsync(2, false, CancellationToken.None);
		dbContext.ChangeTracker.Clear();
		EvidenceClaim[] after = await dbContext.EvidenceClaims.Where(claim => claim.SourceReference.GameId == 2).OrderBy(claim => claim.Id).AsNoTracking().ToArrayAsync();
		GameCompatibilitySummary summary = await dbContext.GameCompatibilitySummaries.SingleAsync(item => item.GameId == 2);
		SourceReferenceImportState[] states = await dbContext.SourceReferenceImportStates.Where(item => item.SourceReference.GameId == 2).ToArrayAsync();

		Assert.False(result.Succeeded);
		Assert.Equal(before.Select(claim => (claim.Id, claim.ClaimText)), after.Select(claim => (claim.Id, claim.ClaimText)));
		Assert.Equal(SummaryState.Stale, summary.State);
		Assert.True(summary.IsStale);
		Assert.All(states, state => { Assert.Equal("fixture_failure", state.ErrorCode); Assert.True(state.ErrorMessage!.Length <= 2000); });
		await transaction.RollbackAsync();
	}

	[Fact]
	public async Task Changed_refresh_reconciles_supported_claims_and_preserves_semantic_matches()
	{
		await using CompatibilityDbContext dbContext = fixture.CreateDbContext();
		await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync();
		CountingClaimProvider provider = new();
		EvidenceRefreshService service = CreateService(dbContext, new FakeFactsProvider(), provider);

		await service.RefreshGameAsync(1, false, CancellationToken.None);
		dbContext.ChangeTracker.Clear();
		EvidenceClaim statusBefore = await dbContext.EvidenceClaims.SingleAsync(claim => claim.SourceReference.GameId == 1 && claim.ClaimType == EvidenceClaimType.Status);
		EvidenceRefreshResult forced = await service.RefreshGameAsync(1, true, CancellationToken.None);
		dbContext.ChangeTracker.Clear();
		EvidenceClaim statusAfter = await dbContext.EvidenceClaims.SingleAsync(claim => claim.SourceReference.GameId == 1 && claim.ClaimType == EvidenceClaimType.Status);

		Assert.False(forced.ClaimsChanged);
		Assert.Equal(statusBefore.Id, statusAfter.Id);
		Assert.Equal(statusBefore.ObservedAt, statusAfter.ObservedAt);
		Assert.Equal(2, provider.CallCount);
		await transaction.RollbackAsync();
	}

	private static EvidenceRefreshService CreateService(CompatibilityDbContext dbContext, IEvidenceSourceFactsProvider facts, IEvidenceClaimProvider provider)
	{
		EvidenceGenerationOptions options = EvidenceSourceAdapterTests.ValidOptions();
		EvidenceClaimMaterializer materializer = new(provider, new EvidenceClaimPromptBuilder(new ZeroTokenCounter()), options);
		return new EvidenceRefreshService(dbContext, facts, materializer, TimeProvider.System);
	}

	private sealed class ZeroTokenCounter : IEvidenceClaimTokenCounter { public int Count(string text) => 0; }
	private sealed class CountingClaimProvider : IEvidenceClaimProvider
	{
		public int CallCount { get; private set; }
		public Task<EvidenceClaimProviderResult> GenerateAsync(EvidenceClaimProviderRequest request, CancellationToken cancellationToken)
		{
			CallCount++;
			IReadOnlyList<GeneratedEvidenceClaim> claims = [new(EvidenceClaimType.Note, "Fixture", "Fixture-grounded note.")];
			return Task.FromResult(new EvidenceClaimProviderResult(claims, 3, 2));
		}
	}
	private sealed class FakeFactsProvider : IEvidenceSourceFactsProvider
	{
		public SourceSystemType? FailingSource { get; init; }
		public Task<NormalizedSourceFacts> FetchAsync(SourceSystemType sourceType, SourceReferenceInput source, CancellationToken cancellationToken)
		{
			if (sourceType == FailingSource) throw new EvidenceSourceException("fixture_failure", $"Fixture failure without raw payload for {source.SourceGameId}.");
			string status = sourceType == SourceSystemType.ProtonDb ? "Gold" : "Running";
			return Task.FromResult(new NormalizedSourceFacts(source.SourceGameId, status, "fixture-v1", $"HASH-{source.SourceGameId}", $"{{\"status\":\"{status}\"}}"));
		}
	}
}
