using LinuxGameCompat.Data;
using LinuxGameCompat.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using LinuxGameCompat.Services.SummaryGeneration;

namespace LinuxGameCompat.Tests;

public sealed class PostgreSqlCompatibilityTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
	[Fact]
	public async Task Migration_AppliesAndLoadsSeedData()
	{
		await using var dbContext = CreateDbContext();

		Assert.Equal(5, await dbContext.Games.CountAsync());
		Assert.Equal(2, await dbContext.SourceSystems.CountAsync());
		Assert.Equal(4, await dbContext.SourceReferences.CountAsync());
		Assert.Equal(3, await dbContext.EvidenceClaims.CountAsync());
		Assert.Equal(2, await dbContext.GameCompatibilitySummaries.CountAsync());
		Assert.True(await dbContext.Games.AnyAsync(game => game.IsHidden));
	}

	[Fact]
	public async Task Migration_AddsNullableSummaryAttemptMetadataAndPreservesSeededSummaries()
	{
		await using var dbContext = CreateDbContext();

		Assert.True(await ColumnExistsAsync(dbContext, "GameCompatibilitySummaries", "LastAttemptedAt"));
		Assert.True(await ColumnExistsAsync(dbContext, "GameCompatibilitySummaries", "InputTokenCount"));
		Assert.True(await ColumnExistsAsync(dbContext, "GameCompatibilitySummaries", "OutputTokenCount"));
		Assert.Equal(2, await dbContext.GameCompatibilitySummaries.CountAsync());
		Assert.All(await dbContext.GameCompatibilitySummaries.ToListAsync(), summary => Assert.Null(summary.LastAttemptedAt));
	}

	[Fact]
	public async Task Generator_GeneratesTargetedSummaryAndUsesDeterministicStatus()
	{
		await using var dbContext = CreateDbContext();
		await using var transaction = await dbContext.Database.BeginTransactionAsync();
		var provider = new FakeSummaryProvider(new(CompatibilityStatus.Unsupported, "Generated evidence summary.", 20, 8));
		var generator = new CompatibilitySummaryGenerator(dbContext, provider,
			new EvidencePromptBuilder(new FixedTokenCounter()), SummaryGenerationOptionsHelper.FromAppSettings(), TimeProvider.System);

		SummaryGenerationResult result = await generator.GenerateGameAsync(1, true, CancellationToken.None);
		dbContext.ChangeTracker.Clear();
		Game game = await dbContext.Games.Include(item => item.CompatibilitySummary).SingleAsync(item => item.Slug == "baldurs-gate-3");

		Assert.Equal(SummaryGenerationOutcome.Generated, result.Outcome);
		Assert.Equal(1, provider.CallCount);
		Assert.Equal(CompatibilityStatus.Playable, game.CompatibilityStatus);
		Assert.Equal(CompatibilityStatus.Unsupported, game.CompatibilitySummary!.SummaryStatus);
		Assert.Equal(20, game.CompatibilitySummary.InputTokenCount);
		Assert.False(game.CompatibilitySummary.IsStale);
		await transaction.RollbackAsync();
	}

	[Fact]
	public async Task Generator_SkipsCurrentTargetUnlessForcedAndExcludesHiddenOrNoEvidence()
	{
		await using var dbContext = CreateDbContext();
		await using var transaction = await dbContext.Database.BeginTransactionAsync();
		Game game = await dbContext.Games.Include(item => item.CompatibilitySummary)
			.Include(item => item.SourceReferences).ThenInclude(reference => reference.SourceSystem)
			.Include(item => item.SourceReferences).ThenInclude(reference => reference.EvidenceClaims)
			.SingleAsync(item => item.Slug == "baldurs-gate-3");
		var claims = game.SourceReferences.SelectMany(reference => reference.EvidenceClaims.Select(claim =>
			new GenerationEvidenceClaim(claim.Id, claim.ClaimType, claim.ClaimValue, claim.ClaimText, claim.ObservedAt,
				reference.SourceSystem.Type, reference.SourceSystem.Name, reference.SourceGameId, reference.Url))).ToList();
		game.CompatibilitySummary!.EvidenceHash = EvidencePromptBuilder.Canonicalize(claims).Hash;
		game.CompatibilitySummary.EvidenceVersion = CanonicalEvidence.ContractVersion;
		await dbContext.SaveChangesAsync();
		var provider = new FakeSummaryProvider(new(CompatibilityStatus.Playable, "Unused.", 1, 1));
		var generator = new CompatibilitySummaryGenerator(dbContext, provider,
			new EvidencePromptBuilder(new FixedTokenCounter()), SummaryGenerationOptionsHelper.FromAppSettings(), TimeProvider.System);

		SummaryGenerationResult current = await generator.GenerateGameAsync(1, false, CancellationToken.None);
		SummaryGenerationResult hidden = await generator.GenerateGameAsync(5, true, CancellationToken.None);
		SummaryGenerationResult noEvidence = await generator.GenerateGameAsync(4, true, CancellationToken.None);

		Assert.Equal(SummaryGenerationOutcome.Skipped, current.Outcome);
		Assert.Equal(SummaryGenerationOutcome.Skipped, hidden.Outcome);
		Assert.Equal(SummaryGenerationOutcome.Skipped, noEvidence.Outcome);
		Assert.Equal(0, provider.CallCount);
		await transaction.RollbackAsync();
	}

	[Fact]
	public async Task Generator_FailedRefreshPreservesSuccessfulOutputAndPublicStatus()
	{
		await using CompatibilityDbContext dbContext = CreateDbContext();
		await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync();
		Game before = await dbContext.Games.Include(game => game.CompatibilitySummary)
			.SingleAsync(game => game.Slug == "baldurs-gate-3");
		string? originalText = before.CompatibilitySummary!.SummaryText;
		string? originalProvider = before.CompatibilitySummary.Provider;
		CompatibilityStatus originalStatus = before.CompatibilityStatus;
		CallbackSummaryProvider provider = new((_, _) => throw new HttpRequestException("temporary provider failure"));
		CompatibilitySummaryGenerator generator = CreateGenerator(dbContext, provider);

		SummaryGenerationResult result = await generator.GenerateGameAsync(before.Id, true, CancellationToken.None);
		dbContext.ChangeTracker.Clear();
		Game after = await dbContext.Games.Include(game => game.CompatibilitySummary)
			.SingleAsync(game => game.Slug == before.Slug);

		Assert.Equal(SummaryGenerationOutcome.Failed, result.Outcome);
		Assert.Equal(originalText, after.CompatibilitySummary!.SummaryText);
		Assert.Equal(originalProvider, after.CompatibilitySummary.Provider);
		Assert.Equal(originalStatus, after.CompatibilityStatus);
		Assert.Equal(SummaryState.Failed, after.CompatibilitySummary.State);
		Assert.True(after.CompatibilitySummary.IsStale);
		await transaction.RollbackAsync();
	}

	[Fact]
	public async Task Generator_DiscardsOutputWhenEvidenceChangesDuringProviderCall()
	{
		await using CompatibilityDbContext dbContext = CreateDbContext();
		await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync();
		CallbackSummaryProvider provider = new(async (_, cancellationToken) =>
		{
			EvidenceClaim claim = await dbContext.EvidenceClaims.FirstAsync(cancellationToken);
			claim.ClaimText += " changed";
			await dbContext.SaveChangesAsync(cancellationToken);
			return new CompatibilitySummaryProviderResult(CompatibilityStatus.Playable, "Stale output.", 10, 5);
		});
		CompatibilitySummaryGenerator generator = CreateGenerator(dbContext, provider);

		SummaryGenerationResult result = await generator.GenerateGameAsync(1, true, CancellationToken.None);
		dbContext.ChangeTracker.Clear();
		GameCompatibilitySummary summary = await dbContext.GameCompatibilitySummaries
			.SingleAsync(item => item.Game.Slug == "baldurs-gate-3");

		Assert.Equal(SummaryGenerationOutcome.Failed, result.Outcome);
		Assert.Equal("evidence_changed", summary.ErrorCode);
		Assert.NotEqual("Stale output.", summary.SummaryText);
		Assert.True(summary.IsStale);
		await transaction.RollbackAsync();
	}

	[Fact]
	public async Task Generator_PropagatesRequestedCancellation()
	{
		await using CompatibilityDbContext dbContext = CreateDbContext();
		using CancellationTokenSource cancellation = new();
		CallbackSummaryProvider provider = new((_, _) =>
		{
			cancellation.Cancel();
			throw new OperationCanceledException(cancellation.Token);
		});
		CompatibilitySummaryGenerator generator = CreateGenerator(dbContext, provider);

		await Assert.ThrowsAsync<OperationCanceledException>(() => generator.GenerateGameAsync(1, true, cancellation.Token));
	}

	[Fact]
	public async Task Generator_SelectsMissingStaleAndFailedSummariesAndUsesAiFallbackWithoutNativeStatus()
	{
		await using CompatibilityDbContext dbContext = CreateDbContext();
		await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync();
		FakeSummaryProvider provider = new(new(CompatibilityStatus.PlayableWithCaveats, "Generated summary.", 12, 4));
		CompatibilitySummaryGenerator generator = CreateGenerator(dbContext, provider);

		SummaryGenerationResult missing = await generator.GenerateGameAsync(3, false, CancellationToken.None);
		SummaryGenerationResult stale = await generator.GenerateGameAsync(2, false, CancellationToken.None);
		dbContext.ChangeTracker.Clear();
		Game helldivers = await dbContext.Games.Include(game => game.CompatibilitySummary)
			.SingleAsync(game => game.Slug == "helldivers-2");
		Assert.Equal(CompatibilityStatus.PlayableWithCaveats, helldivers.CompatibilityStatus);
		Assert.Equal(CompatibilityStatus.PlayableWithCaveats, helldivers.CompatibilitySummary!.SummaryStatus);

		helldivers.CompatibilitySummary.State = SummaryState.Failed;
		helldivers.CompatibilitySummary.IsStale = true;
		await dbContext.SaveChangesAsync();
		SummaryGenerationResult failed = await generator.GenerateGameAsync(2, false, CancellationToken.None);

		Assert.Equal(SummaryGenerationOutcome.Generated, missing.Outcome);
		Assert.Equal(SummaryGenerationOutcome.Generated, stale.Outcome);
		Assert.Equal(SummaryGenerationOutcome.Generated, failed.Outcome);
		Assert.Equal(3, provider.CallCount);
		await transaction.RollbackAsync();
	}

	private sealed class FixedTokenCounter : IGenerationTokenCounter { public int Count(string text) => 10; }
	private sealed class FakeSummaryProvider(CompatibilitySummaryProviderResult result) : ICompatibilitySummaryProvider
	{
		public int CallCount { get; private set; }
		public Task<CompatibilitySummaryProviderResult> GenerateAsync(CompatibilitySummaryProviderRequest request, CancellationToken cancellationToken)
		{
			CallCount++;
			return Task.FromResult(result);
		}
	}
	private sealed class CallbackSummaryProvider(
		Func<CompatibilitySummaryProviderRequest, CancellationToken, Task<CompatibilitySummaryProviderResult>> callback)
		: ICompatibilitySummaryProvider
	{
		public Task<CompatibilitySummaryProviderResult> GenerateAsync(
			CompatibilitySummaryProviderRequest request, CancellationToken cancellationToken) => callback(request, cancellationToken);
	}

	private static CompatibilitySummaryGenerator CreateGenerator(CompatibilityDbContext dbContext, ICompatibilitySummaryProvider provider) =>
		new(dbContext, provider, new EvidencePromptBuilder(new FixedTokenCounter()),
			SummaryGenerationOptionsHelper.FromAppSettings(), TimeProvider.System);

	[Fact]
	public async Task Migration_CreatesMemberAuthSchema()
	{
		await using var dbContext = CreateDbContext();

		Assert.True(await TableExistsAsync(dbContext, "AspNetUsers"));
		Assert.True(await TableExistsAsync(dbContext, "AspNetUserClaims"));
		Assert.True(await TableExistsAsync(dbContext, "AspNetUserLogins"));
		Assert.True(await TableExistsAsync(dbContext, "AspNetUserTokens"));
		Assert.True(await TableExistsAsync(dbContext, "MagicLinkRequests"));
		Assert.True(await ColumnExistsAsync(dbContext, "MagicLinkRequests", "NormalizedEmail"));
		Assert.True(await ColumnExistsAsync(dbContext, "MagicLinkRequests", "TokenHash"));
		Assert.True(await ColumnExistsAsync(dbContext, "MagicLinkRequests", "ExpiresAt"));
		Assert.True(await ColumnExistsAsync(dbContext, "MagicLinkRequests", "ConsumedAt"));
		Assert.True(await ColumnExistsAsync(dbContext, "MagicLinkRequests", "ReturnUrl"));
		Assert.True(await ColumnExistsAsync(dbContext, "MagicLinkRequests", "RequestIpAddress"));
		Assert.True(await ColumnExistsAsync(dbContext, "MagicLinkRequests", "UserAgent"));
		Assert.True(await IndexExistsAsync(dbContext, "EmailIndex"));
		Assert.True(await IndexExistsAsync(dbContext, "IX_MagicLinkRequests_TokenHash"));
		Assert.True(await IndexExistsAsync(dbContext, "IX_MagicLinkRequests_NormalizedEmail"));
		Assert.True(await IndexExistsAsync(dbContext, "IX_MagicLinkRequests_ExpiresAt"));
	}

	[Fact]
	public async Task Migration_CreatesMemberFavoritesSchema()
	{
		await using var dbContext = CreateDbContext();

		Assert.True(await TableExistsAsync(dbContext, "MemberFavorites"));
		Assert.True(await ColumnExistsAsync(dbContext, "MemberFavorites", "Id"));
		Assert.True(await ColumnExistsAsync(dbContext, "MemberFavorites", "MemberId"));
		Assert.True(await ColumnExistsAsync(dbContext, "MemberFavorites", "GameId"));
		Assert.True(await ColumnExistsAsync(dbContext, "MemberFavorites", "CreatedAt"));
		Assert.True(await IndexExistsAsync(dbContext, "IX_MemberFavorites_MemberId_GameId"));
		Assert.True(await IndexExistsAsync(dbContext, "IX_MemberFavorites_MemberId"));
		Assert.True(await IndexExistsAsync(dbContext, "IX_MemberFavorites_GameId"));
		Assert.True(await ForeignKeyExistsAsync(dbContext, "MemberFavorites", "AspNetUsers", "FK_MemberFavorites_AspNetUsers_MemberId"));
		Assert.True(await ForeignKeyExistsAsync(dbContext, "MemberFavorites", "Games", "FK_MemberFavorites_Games_GameId"));
	}

	[Fact]
	public async Task ReadService_ReturnsVisibleGamesAndExcludesHiddenGames()
	{
		await using var dbContext = CreateDbContext();
		var service = new GameCompatibilityReadService(dbContext);

		var games = await service.GetVisibleGamesAsync();

		Assert.Equal(4, games.Count);
		Assert.Contains(games, game => game.Slug == "unnamed-prototype" && game.CompatibilityStatus == CompatibilityStatus.Unknown);
		Assert.DoesNotContain(games, game => game.Slug == "suppressed-test-record");
	}

	[Fact]
	public async Task ReadService_ReturnsBoundedVisibleGames()
	{
		await using var dbContext = CreateDbContext();
		var service = new GameCompatibilityReadService(dbContext);

		var games = await service.GetVisibleGamesAsync(limit: 2, offset: 1);
		var noGames = await service.GetVisibleGamesAsync(limit: 0);
		var negativeOffsetGames = await service.GetVisibleGamesAsync(limit: 1, offset: -10);
		var cancellationTokenOnlyGames = await service.GetVisibleGamesAsync(CancellationToken.None);

		Assert.Equal(2, games.Count);
		Assert.Equal("destiny-2", games[0].Slug);
		Assert.Empty(noGames);
		Assert.Single(negativeOffsetGames);
		Assert.Equal("baldurs-gate-3", negativeOffsetGames[0].Slug);
		Assert.Equal(4, cancellationTokenOnlyGames.Count);
	}

	[Fact]
	public async Task ReadService_ReturnsSourceClaimsAndSummaryForVisibleGame()
	{
		await using var dbContext = CreateDbContext();
		var service = new GameCompatibilityReadService(dbContext);

		var detail = await service.GetVisibleGameBySlugAsync("baldurs-gate-3");

		Assert.NotNull(detail);
		Assert.Equal(CompatibilityStatus.Playable, detail.CompatibilityStatus);
		Assert.Contains(detail.SourceReferences, reference => reference.SourceType == SourceSystemType.ProtonDb);
		Assert.Contains(detail.EvidenceClaims, claim => claim.ClaimType == EvidenceClaimType.Status);
		Assert.NotNull(detail.Summary);
		Assert.Equal(SummaryState.Current, detail.Summary.State);
		Assert.False(detail.Summary.IsStale);
		Assert.False(detail.Summary.HasStatusDisagreement);
		Assert.False(detail.Summary.IsAiStatusFallback);
	}

	[Fact]
	public async Task ReadService_MapsSummaryTrustMetadataWithoutProviderErrors()
	{
		await using var dbContext = CreateDbContext();
		await using var transaction = await dbContext.Database.BeginTransactionAsync();
		var game = await dbContext.Games
			.Include(item => item.CompatibilitySummary)
			.SingleAsync(item => item.Slug == "baldurs-gate-3");
		game.CompatibilitySummary!.State = SummaryState.Failed;
		game.CompatibilitySummary.IsStale = true;
		game.CompatibilitySummary.SummaryStatus = CompatibilityStatus.Unsupported;
		game.CompatibilitySummary.ErrorCode = "secret-code";
		game.CompatibilitySummary.ErrorMessage = "secret provider detail";
		await dbContext.SaveChangesAsync();

		var service = new GameCompatibilityReadService(dbContext);
		var disagreement = await service.GetVisibleGameBySlugAsync("baldurs-gate-3");

		Assert.NotNull(disagreement?.Summary);
		Assert.Equal(SummaryState.Failed, disagreement.Summary.State);
		Assert.True(disagreement.Summary.IsStale);
		Assert.True(disagreement.Summary.HasStatusDisagreement);
		Assert.False(disagreement.Summary.IsAiStatusFallback);
		Assert.DoesNotContain(
			typeof(GameCompatibilitySummaryDetail).GetProperties(),
			property => property.Name is "ErrorCode" or "ErrorMessage");

		var noEvidenceGame = await dbContext.Games
			.Include(item => item.CompatibilitySummary)
			.SingleAsync(item => item.Slug == "unnamed-prototype");
		noEvidenceGame.CompatibilityStatus = CompatibilityStatus.Playable;
		noEvidenceGame.CompatibilitySummary = new GameCompatibilitySummary
		{
			State = SummaryState.Current,
			SummaryStatus = CompatibilityStatus.Playable,
			SummaryText = "Generated fallback.",
			GeneratedAt = DateTimeOffset.UtcNow
		};
		await dbContext.SaveChangesAsync();

		var fallback = await service.GetVisibleGameBySlugAsync("unnamed-prototype");

		Assert.NotNull(fallback?.Summary);
		Assert.True(fallback.Summary.IsAiStatusFallback);
		Assert.False(fallback.Summary.HasStatusDisagreement);
		await transaction.RollbackAsync();
	}

	[Fact]
	public async Task ReadService_ReturnsVisibleGameDetailWithoutSourceReferences()
	{
		await using var dbContext = CreateDbContext();
		var service = new GameCompatibilityReadService(dbContext);

		var detail = await service.GetVisibleGameBySlugAsync("unnamed-prototype");

		Assert.NotNull(detail);
		Assert.Equal(CompatibilityStatus.Unknown, detail.CompatibilityStatus);
		Assert.Empty(detail.SourceReferences);
		Assert.Empty(detail.EvidenceClaims);
		Assert.Null(detail.Summary);
	}

	[Fact]
	public async Task ReadService_DoesNotReturnHiddenGameDetail()
	{
		await using var dbContext = CreateDbContext();
		var service = new GameCompatibilityReadService(dbContext);

		var detail = await service.GetVisibleGameBySlugAsync("suppressed-test-record");

		Assert.Null(detail);
	}

	[Fact]
	public async Task SearchVisibleGamesByTitle_UsesPostgreSqlCaseInsensitiveSearch()
	{
		await using var dbContext = CreateDbContext();
		var service = new GameCompatibilityReadService(dbContext);

		var games = await service.SearchVisibleGamesByTitleAsync("gate");

		Assert.Single(games);
		Assert.Equal("baldurs-gate-3", games[0].Slug);
	}

	[Fact]
	public async Task SearchVisibleGamesByTitle_TreatsLikeWildcardsAsLiteralCharacters()
	{
		await using var dbContext = CreateDbContext();
		await using var transaction = await dbContext.Database.BeginTransactionAsync();
		var now = DateTimeOffset.UtcNow;
		dbContext.Games.AddRange(
			new Game
			{
				Title = "Literal 100% Save",
				Slug = "literal-percent-save",
				CompatibilityStatus = CompatibilityStatus.Unknown,
				CreatedAt = now,
				UpdatedAt = now
			},
			new Game
			{
				Title = "Literal Co_op Quest",
				Slug = "literal-underscore-quest",
				CompatibilityStatus = CompatibilityStatus.Unknown,
				CreatedAt = now,
				UpdatedAt = now
			},
			new Game
			{
				Title = @"Literal C:\Games Path",
				Slug = "literal-backslash-path",
				CompatibilityStatus = CompatibilityStatus.Unknown,
				CreatedAt = now,
				UpdatedAt = now
			},
			new Game
			{
				Title = "Visible Control Game",
				Slug = "literal-control-game",
				CompatibilityStatus = CompatibilityStatus.Unknown,
				CreatedAt = now,
				UpdatedAt = now
			},
			new Game
			{
				Title = "Hidden 100% Save",
				Slug = "hidden-literal-percent-save",
				CompatibilityStatus = CompatibilityStatus.Unknown,
				IsHidden = true,
				CreatedAt = now,
				UpdatedAt = now
			});
		await dbContext.SaveChangesAsync();

		var service = new GameCompatibilityReadService(dbContext);

		var percentGames = await service.SearchVisibleGamesByTitleAsync("%", limit: 100);
		var underscoreGames = await service.SearchVisibleGamesByTitleAsync("_", limit: 100);
		var backslashGames = await service.SearchVisibleGamesByTitleAsync(@"\", limit: 100);
		var normalGames = await service.SearchVisibleGamesByTitleAsync("GATE", limit: 100);

		Assert.Collection(percentGames, game => Assert.Equal("literal-percent-save", game.Slug));
		Assert.Collection(underscoreGames, game => Assert.Equal("literal-underscore-quest", game.Slug));
		Assert.Collection(backslashGames, game => Assert.Equal("literal-backslash-path", game.Slug));
		Assert.Collection(normalGames, game => Assert.Equal("baldurs-gate-3", game.Slug));
		Assert.DoesNotContain(percentGames, game => game.Slug == "hidden-literal-percent-save");

		await transaction.RollbackAsync();
	}

	[Fact]
	public async Task SearchVisibleGamesByTitle_CapsLargeLimit()
	{
		await using var dbContext = CreateDbContext();
		var service = new GameCompatibilityReadService(dbContext);

		var games = await service.SearchVisibleGamesByTitleAsync("e", limit: 10_000);

		Assert.Equal(4, games.Count);
		Assert.DoesNotContain(games, game => game.Slug == "suppressed-test-record");
	}

	[Fact]
	public async Task Model_EnforcesUniqueSlug()
	{
		await using var dbContext = CreateDbContext();
		dbContext.Games.Add(new Game
		{
			Title = "Duplicate Slug",
			Slug = "baldurs-gate-3",
			CompatibilityStatus = CompatibilityStatus.Unknown,
			CreatedAt = DateTimeOffset.UtcNow,
			UpdatedAt = DateTimeOffset.UtcNow
		});

		await Assert.ThrowsAnyAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
	}

	[Fact]
	public async Task Model_EnforcesUniqueSourceIdentityAcrossGames()
	{
		await using var dbContext = CreateDbContext();
		dbContext.SourceReferences.Add(new SourceReference
		{
			GameId = 2,
			SourceSystemId = 1,
			SourceGameId = "1086940",
			Url = "https://www.protondb.com/app/1086940-duplicate",
			CreatedAt = DateTimeOffset.UtcNow
		});

		await Assert.ThrowsAnyAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
	}

	[Fact]
	public async Task Model_EnforcesUniqueMemberGameFavorite()
	{
		var emailSender = new AuthTestHarness.CapturingAuthEmailSender();
		await using var harness = AuthTestHarness.Create(fixture, emailSender);
		var member = await harness.CreateAuthenticatedUserAsync(UniqueEmail("unique-favorite"));

		harness.DbContext.MemberFavorites.AddRange(
			new MemberFavorite
			{
				MemberId = member.Id,
				GameId = 1,
				CreatedAt = DateTimeOffset.UtcNow
			},
			new MemberFavorite
			{
				MemberId = member.Id,
				GameId = 1,
				CreatedAt = DateTimeOffset.UtcNow
			});

		await Assert.ThrowsAnyAsync<DbUpdateException>(() => harness.DbContext.SaveChangesAsync());
	}

	[Fact]
	public async Task MemberFavoritesService_AddsCurrentMemberFavoriteAndIsIdempotent()
	{
		var emailSender = new AuthTestHarness.CapturingAuthEmailSender();
		await using var harness = AuthTestHarness.Create(fixture, emailSender);
		var member = await harness.CreateAuthenticatedUserAsync(UniqueEmail("favorite-add"));

		var firstResult = await harness.FavoritesService.AddCurrentMemberFavoriteAsync(1);
		var secondResult = await harness.FavoritesService.AddCurrentMemberFavoriteAsync(1);
		var state = await harness.FavoritesService.GetFavoriteStateAsync(1);

		Assert.True(firstResult.Succeeded);
		Assert.True(secondResult.Succeeded);
		Assert.True(state.IsAuthenticated);
		Assert.True(state.IsVisibleGame);
		Assert.True(state.IsFavorite);
		Assert.Equal(1, await harness.DbContext.MemberFavorites.CountAsync(
			favorite => favorite.MemberId == member.Id && favorite.GameId == 1));
	}

	[Fact]
	public async Task MemberFavoritesService_DuplicateAddAfterExternalInsertIsIdempotent()
	{
		var emailSender = new AuthTestHarness.CapturingAuthEmailSender();
		await using var harness = AuthTestHarness.Create(fixture, emailSender);
		var member = await harness.CreateAuthenticatedUserAsync(UniqueEmail("favorite-add-race"));
		harness.DbContext.MemberFavorites.Add(new MemberFavorite
		{
			MemberId = member.Id,
			GameId = 1,
			CreatedAt = DateTimeOffset.UtcNow
		});
		await harness.DbContext.SaveChangesAsync();

		var result = await harness.FavoritesService.AddCurrentMemberFavoriteAsync(1);

		Assert.True(result.Succeeded);
		Assert.Equal(1, await harness.DbContext.MemberFavorites.CountAsync(
			favorite => favorite.MemberId == member.Id && favorite.GameId == 1));
	}

	[Fact]
	public async Task MemberFavoritesService_RemoveIsIdempotentAndOwnerIsolated()
	{
		var emailSender = new AuthTestHarness.CapturingAuthEmailSender();
		await using var harness = AuthTestHarness.Create(fixture, emailSender);
		var memberA = await harness.CreateAuthenticatedUserAsync(UniqueEmail("favorite-owner-a"));
		var memberB = await harness.CreateAuthenticatedUserAsync(UniqueEmail("favorite-owner-b"));
		await harness.FavoritesService.AddCurrentMemberFavoriteAsync(2);
		harness.SetCurrentUser(memberA);
		await harness.FavoritesService.AddCurrentMemberFavoriteAsync(1);

		var ownerBListBeforeRemove = await harness.FavoritesService.GetCurrentMemberFavoritesAsync();
		var removeOwnerBGame = await harness.FavoritesService.RemoveCurrentMemberFavoriteAsync(2);
		var removeAlreadyAbsent = await harness.FavoritesService.RemoveCurrentMemberFavoriteAsync(2);
		var ownerAListAfterRemove = await harness.FavoritesService.GetCurrentMemberFavoritesAsync();
		harness.SetCurrentUser(memberB);
		var ownerBListAfterRemove = await harness.FavoritesService.GetCurrentMemberFavoritesAsync();

		Assert.True(removeOwnerBGame.Succeeded);
		Assert.True(removeAlreadyAbsent.Succeeded);
		Assert.Collection(ownerBListBeforeRemove, game => Assert.Equal("Baldur's Gate 3", game.Title));
		Assert.Collection(ownerAListAfterRemove, game => Assert.Equal("Baldur's Gate 3", game.Title));
		Assert.Collection(ownerBListAfterRemove, game => Assert.Equal("Helldivers 2", game.Title));
	}

	[Fact]
	public async Task MemberFavoritesService_RejectsUnauthenticatedHiddenAndMissingFavorites()
	{
		var emailSender = new AuthTestHarness.CapturingAuthEmailSender();
		await using var harness = AuthTestHarness.Create(fixture, emailSender);

		var unauthenticatedResult = await harness.FavoritesService.AddCurrentMemberFavoriteAsync(1);
		var member = await harness.CreateAuthenticatedUserAsync(UniqueEmail("favorite-hidden"));
		var hiddenResult = await harness.FavoritesService.AddCurrentMemberFavoriteAsync(5);
		var missingResult = await harness.FavoritesService.AddCurrentMemberFavoriteAsync(999_999);

		Assert.Equal(MemberFavoriteMutationStatus.Unauthenticated, unauthenticatedResult.Status);
		Assert.Equal(MemberFavoriteMutationStatus.HiddenOrMissingGame, hiddenResult.Status);
		Assert.Equal(MemberFavoriteMutationStatus.HiddenOrMissingGame, missingResult.Status);
		Assert.False(await harness.DbContext.MemberFavorites.AnyAsync(
			favorite => favorite.MemberId == member.Id && favorite.GameId == 5));
	}

	[Fact]
	public async Task MemberFavoritesService_ListsVisibleFavoritesWithCurrentStatusInTitleOrder()
	{
		var emailSender = new AuthTestHarness.CapturingAuthEmailSender();
		await using var harness = AuthTestHarness.Create(fixture, emailSender);
		var member = await harness.CreateAuthenticatedUserAsync(UniqueEmail("favorite-list"));
		await harness.FavoritesService.AddCurrentMemberFavoriteAsync(3);
		await harness.FavoritesService.AddCurrentMemberFavoriteAsync(1);
		harness.DbContext.MemberFavorites.Add(new MemberFavorite
		{
			MemberId = member.Id,
			GameId = 5,
			CreatedAt = DateTimeOffset.UtcNow
		});
		await harness.DbContext.SaveChangesAsync();

		var favorites = await harness.FavoritesService.GetCurrentMemberFavoritesAsync();

		Assert.Collection(
			favorites,
			game =>
			{
				Assert.Equal("Baldur's Gate 3", game.Title);
				Assert.Equal(CompatibilityStatus.Playable, game.CompatibilityStatus);
			},
			game =>
			{
				Assert.Equal("Destiny 2", game.Title);
				Assert.Equal(CompatibilityStatus.Unsupported, game.CompatibilityStatus);
			});
		Assert.DoesNotContain(favorites, game => game.Slug == "suppressed-test-record");
	}

	[Fact]
	public void AuthPublicBaseUriResolver_RequiresHttpsConfigurationOutsideDevelopment()
	{
		var request = new DefaultHttpContext().Request;
		request.Scheme = "https";
		request.Host = new HostString("spoofed.example.test");
		var missingConfiguration = new ConfigurationBuilder().Build();
		var httpConfiguration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Auth:PublicBaseUrl"] = "http://example.test"
			})
			.Build();

		var missingException = Assert.Throws<InvalidOperationException>(() =>
			AuthPublicBaseUriResolver.Resolve(missingConfiguration, request, isDevelopment: false));
		var httpException = Assert.Throws<InvalidOperationException>(() =>
			AuthPublicBaseUriResolver.Resolve(httpConfiguration, request, isDevelopment: false));

		Assert.Contains("Auth:PublicBaseUrl", missingException.Message);
		Assert.Contains("HTTPS", httpException.Message);
	}

	[Fact]
	public void AuthPublicBaseUriResolver_UsesConfiguredHttpsUrlOutsideDevelopment()
	{
		var request = new DefaultHttpContext().Request;
		request.Scheme = "https";
		request.Host = new HostString("spoofed.example.test");
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Auth:PublicBaseUrl"] = "https://public.example.test"
			})
			.Build();

		var baseUri = AuthPublicBaseUriResolver.Resolve(configuration, request, isDevelopment: false);

		Assert.Equal(new Uri("https://public.example.test"), baseUri);
	}

	[Fact]
	public void AuthPublicBaseUriResolver_AllowsRequestFallbackInDevelopment()
	{
		var request = new DefaultHttpContext().Request;
		request.Scheme = "http";
		request.Host = new HostString("localhost:5000");
		var configuration = new ConfigurationBuilder().Build();

		var baseUri = AuthPublicBaseUriResolver.Resolve(configuration, request, isDevelopment: true);

		Assert.Equal(new Uri("http://localhost:5000"), baseUri);
	}

	[Fact]
	public async Task MagicLinkRequest_StoresHashedTokenAndDoesNotCreateMemberImmediately()
	{
		var emailSender = new AuthTestHarness.CapturingAuthEmailSender();
		await using var harness = AuthTestHarness.Create(fixture, emailSender);

		await harness.Service.RequestLoginLinkAsync(new MagicLinkRequestInput(
			"new-member@example.test",
			"/games/baldurs-gate-3",
			new Uri("https://example.test"),
			"127.0.0.1",
			"test-agent"));

		var request = await harness.DbContext.MagicLinkRequests.SingleAsync(request => request.NormalizedEmail == "NEW-MEMBER@EXAMPLE.TEST");

		Assert.NotEqual(AuthTestHarness.ExtractToken(emailSender.LastLoginLink), request.TokenHash);
		Assert.Equal(64, request.TokenHash.Length);
		Assert.Null(request.ConsumedAt);
		Assert.Equal("/games/baldurs-gate-3", request.ReturnUrl);
		Assert.Equal("127.0.0.1", request.RequestIpAddress);
		Assert.Equal("test-agent", request.UserAgent);
		Assert.Null(await harness.UserManager.FindByEmailAsync("new-member@example.test"));
	}

	[Fact]
	public async Task MagicLinkRequest_InvalidEmailDoesNotPersistOrSend()
	{
		var emailSender = new AuthTestHarness.CapturingAuthEmailSender();
		await using var harness = AuthTestHarness.Create(fixture, emailSender);
		var initialCount = await harness.DbContext.MagicLinkRequests.CountAsync();

		var result = await harness.Service.RequestLoginLinkAsync(new MagicLinkRequestInput(
			"not-an-email",
			"/",
			new Uri("https://example.test"),
			null,
			null));

		Assert.False(result.Accepted);
		Assert.Null(result.LoginLink);
		Assert.Equal(initialCount, await harness.DbContext.MagicLinkRequests.CountAsync());
		Assert.Equal(0, emailSender.SendCount);
	}

	[Fact]
	public async Task MagicLinkRequest_OverlongReturnUrlNormalizesToRoot()
	{
		var emailSender = new AuthTestHarness.CapturingAuthEmailSender();
		await using var harness = AuthTestHarness.Create(fixture, emailSender);

		await harness.Service.RequestLoginLinkAsync(new MagicLinkRequestInput(
			"overlong-return@example.test",
			$"/{new string('a', 2050)}",
			new Uri("https://example.test"),
			null,
			null));

		var request = await harness.DbContext.MagicLinkRequests.SingleAsync(request => request.NormalizedEmail == "OVERLONG-RETURN@EXAMPLE.TEST");

		Assert.Equal("/", request.ReturnUrl);
	}

	[Fact]
	public async Task MagicLinkRequest_EmailSendFailureRemovesSavedRequest()
	{
		var emailSender = new AuthTestHarness.ThrowingAuthEmailSender();
		await using var harness = AuthTestHarness.Create(fixture, emailSender);
		var initialCount = await harness.DbContext.MagicLinkRequests.CountAsync();

		var result = await harness.Service.RequestLoginLinkAsync(new MagicLinkRequestInput(
			"send-failure@example.test",
			"/",
			new Uri("https://example.test"),
			null,
			null));

		Assert.False(result.Accepted);
		Assert.Null(result.LoginLink);
		Assert.Equal(initialCount, await harness.DbContext.MagicLinkRequests.CountAsync());
		Assert.False(await harness.DbContext.MagicLinkRequests.AnyAsync(request => request.NormalizedEmail == "SEND-FAILURE@EXAMPLE.TEST"));
	}

	[Fact]
	public async Task MagicLinkConsumption_CreatesMemberAndMarksRequestConsumed()
	{
		var emailSender = new AuthTestHarness.CapturingAuthEmailSender();
		await using var harness = AuthTestHarness.Create(fixture, emailSender);
		await harness.Service.RequestLoginLinkAsync(new MagicLinkRequestInput(
			"consume-member@example.test",
			"/games/baldurs-gate-3",
			new Uri("https://example.test"),
			null,
			null));

		var result = await harness.Service.ConsumeLoginLinkAsync(AuthTestHarness.ExtractToken(emailSender.LastLoginLink));

		var request = await harness.DbContext.MagicLinkRequests.SingleAsync(request => request.NormalizedEmail == "CONSUME-MEMBER@EXAMPLE.TEST");
		await harness.DbContext.Entry(request).ReloadAsync();

		Assert.True(result.Succeeded);
		Assert.Equal("/games/baldurs-gate-3", result.RedirectUrl);
		Assert.NotNull(request.ConsumedAt);
		Assert.NotNull(await harness.UserManager.FindByEmailAsync("consume-member@example.test"));
	}

	[Fact]
	public async Task MagicLinkConsumption_RejectsConsumedToken()
	{
		var emailSender = new AuthTestHarness.CapturingAuthEmailSender();
		await using var harness = AuthTestHarness.Create(fixture, emailSender);
		await harness.Service.RequestLoginLinkAsync(new MagicLinkRequestInput(
			"replay-member@example.test",
			"/",
			new Uri("https://example.test"),
			null,
			null));
		var token = AuthTestHarness.ExtractToken(emailSender.LastLoginLink);

		var firstResult = await harness.Service.ConsumeLoginLinkAsync(token);
		var replayResult = await harness.Service.ConsumeLoginLinkAsync(token);

		Assert.True(firstResult.Succeeded);
		Assert.False(replayResult.Succeeded);
	}

	[Fact]
	public async Task MagicLinkConsumption_RejectsExpiredAndInvalidTokens()
	{
		var emailSender = new AuthTestHarness.CapturingAuthEmailSender();
		var timeProvider = new AuthTestHarness.MutableTimeProvider(DateTimeOffset.UtcNow);
		await using var harness = AuthTestHarness.Create(fixture, emailSender, timeProvider);
		await harness.Service.RequestLoginLinkAsync(new MagicLinkRequestInput(
			"expired-member@example.test",
			"/",
			new Uri("https://example.test"),
			null,
			null));
		var token = AuthTestHarness.ExtractToken(emailSender.LastLoginLink);

		timeProvider.UtcNow = timeProvider.UtcNow.AddMinutes(16);
		var expiredResult = await harness.Service.ConsumeLoginLinkAsync(token);
		var invalidResult = await harness.Service.ConsumeLoginLinkAsync("not-a-real-token");

		Assert.False(expiredResult.Succeeded);
		Assert.Equal("/login?failed=1", expiredResult.RedirectUrl);
		Assert.False(invalidResult.Succeeded);
		Assert.Equal("/login?failed=1", invalidResult.RedirectUrl);
		Assert.Null(await harness.UserManager.FindByEmailAsync("expired-member@example.test"));
	}

	[Fact]
	public async Task MagicLinkConsumption_NormalizesNonLocalReturnUrlToRoot()
	{
		var emailSender = new AuthTestHarness.CapturingAuthEmailSender();
		await using var harness = AuthTestHarness.Create(fixture, emailSender);
		await harness.Service.RequestLoginLinkAsync(new MagicLinkRequestInput(
			"unsafe-return@example.test",
			"https://evil.example.test/capture",
			new Uri("https://example.test"),
			null,
			null));

		var result = await harness.Service.ConsumeLoginLinkAsync(AuthTestHarness.ExtractToken(emailSender.LastLoginLink));

		Assert.True(result.Succeeded);
		Assert.Equal("/", result.RedirectUrl);
	}

	private CompatibilityDbContext CreateDbContext()
	{
		return fixture.CreateDbContext();
	}

	private static async Task<bool> TableExistsAsync(CompatibilityDbContext dbContext, string tableName)
	{
		var count = await dbContext.Database
			.SqlQueryRaw<int>(
				"""
				SELECT COUNT(*)::int AS "Value"
				FROM information_schema.tables
				WHERE table_schema = 'public' AND table_name = {0}
				""",
				tableName)
			.SingleAsync();

		return count == 1;
	}

	private static async Task<bool> ColumnExistsAsync(
		CompatibilityDbContext dbContext,
		string tableName,
		string columnName)
	{
		var count = await dbContext.Database
			.SqlQueryRaw<int>(
				"""
				SELECT COUNT(*)::int AS "Value"
				FROM information_schema.columns
				WHERE table_schema = 'public' AND table_name = {0} AND column_name = {1}
				""",
				tableName,
				columnName)
			.SingleAsync();

		return count == 1;
	}

	private static async Task<bool> IndexExistsAsync(CompatibilityDbContext dbContext, string indexName)
	{
		var count = await dbContext.Database
			.SqlQueryRaw<int>(
				"""
				SELECT COUNT(*)::int AS "Value"
				FROM pg_indexes
				WHERE schemaname = 'public' AND indexname = {0}
				""",
				indexName)
			.SingleAsync();

		return count == 1;
	}

	private static async Task<bool> ForeignKeyExistsAsync(
		CompatibilityDbContext dbContext,
		string tableName,
		string referencedTableName,
		string constraintName)
	{
		var count = await dbContext.Database
			.SqlQueryRaw<int>(
				"""
				SELECT COUNT(*)::int AS "Value"
				FROM information_schema.table_constraints tc
				JOIN information_schema.referential_constraints rc
					ON tc.constraint_name = rc.constraint_name
				JOIN information_schema.constraint_column_usage ccu
					ON rc.unique_constraint_name = ccu.constraint_name
				WHERE tc.table_schema = 'public'
					AND tc.table_name = {0}
					AND ccu.table_name = {1}
					AND tc.constraint_name = {2}
					AND tc.constraint_type = 'FOREIGN KEY'
				""",
				tableName,
				referencedTableName,
				constraintName)
			.SingleAsync();

		return count == 1;
	}

	private static string UniqueEmail(string prefix)
	{
		return $"{prefix}-{Guid.NewGuid():N}@example.test";
	}
}
