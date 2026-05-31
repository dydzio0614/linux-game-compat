using LinuxGameCompat.Data;
using LinuxGameCompat.Services;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace LinuxGameCompat.Tests;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
	private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
		.WithDatabase("linux_game_compat_tests")
		.WithUsername("linux_game_compat")
		.WithPassword("linux_game_compat_dev")
		.Build();

	public DbContextOptions<CompatibilityDbContext> Options { get; private set; } = null!;

	public async Task InitializeAsync()
	{
		await _postgres.StartAsync();
		Options = new DbContextOptionsBuilder<CompatibilityDbContext>()
			.UseNpgsql(_postgres.GetConnectionString())
			.Options;

		await using var dbContext = CreateDbContext();
		await dbContext.Database.MigrateAsync();
	}

	public async Task DisposeAsync()
	{
		await _postgres.DisposeAsync();
	}

	public CompatibilityDbContext CreateDbContext()
	{
		return new CompatibilityDbContext(Options);
	}
}

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
		Assert.Equal("placeholder", detail.Summary.Provider);
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

	private CompatibilityDbContext CreateDbContext()
	{
		return fixture.CreateDbContext();
	}
}
