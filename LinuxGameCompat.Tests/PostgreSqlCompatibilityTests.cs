using LinuxGameCompat.Data;
using LinuxGameCompat.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
	public string ConnectionString { get; private set; } = string.Empty;

	public async Task InitializeAsync()
	{
		await _postgres.StartAsync();
		ConnectionString = _postgres.GetConnectionString();
		Options = new DbContextOptionsBuilder<CompatibilityDbContext>()
			.UseNpgsql(ConnectionString)
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

	[Fact]
	public async Task MagicLinkRequest_StoresHashedTokenAndDoesNotCreateMemberImmediately()
	{
		var emailSender = new CapturingAuthEmailSender();
		await using var scope = CreateAuthScope(emailSender);
		var service = scope.ServiceProvider.GetRequiredService<IMagicLinkService>();

		await service.RequestLoginLinkAsync(new MagicLinkRequestInput(
			"new-member@example.test",
			"/games/baldurs-gate-3",
			new Uri("https://example.test"),
			"127.0.0.1",
			"test-agent"));

		var dbContext = scope.ServiceProvider.GetRequiredService<CompatibilityDbContext>();
		var request = await dbContext.MagicLinkRequests.SingleAsync(request => request.NormalizedEmail == "NEW-MEMBER@EXAMPLE.TEST");
		var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

		Assert.NotEqual(ExtractToken(emailSender.LastLoginLink), request.TokenHash);
		Assert.Equal(64, request.TokenHash.Length);
		Assert.Null(request.ConsumedAt);
		Assert.Equal("/games/baldurs-gate-3", request.ReturnUrl);
		Assert.Equal("127.0.0.1", request.RequestIpAddress);
		Assert.Equal("test-agent", request.UserAgent);
		Assert.Null(await userManager.FindByEmailAsync("new-member@example.test"));
	}

	[Fact]
	public async Task MagicLinkConsumption_CreatesMemberAndMarksRequestConsumed()
	{
		var emailSender = new CapturingAuthEmailSender();
		await using var scope = CreateAuthScope(emailSender);
		var service = scope.ServiceProvider.GetRequiredService<IMagicLinkService>();
		await service.RequestLoginLinkAsync(new MagicLinkRequestInput(
			"consume-member@example.test",
			"/games/baldurs-gate-3",
			new Uri("https://example.test"),
			null,
			null));

		var result = await service.ConsumeLoginLinkAsync(ExtractToken(emailSender.LastLoginLink));

		var dbContext = scope.ServiceProvider.GetRequiredService<CompatibilityDbContext>();
		var request = await dbContext.MagicLinkRequests.SingleAsync(request => request.NormalizedEmail == "CONSUME-MEMBER@EXAMPLE.TEST");
		await dbContext.Entry(request).ReloadAsync();
		var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

		Assert.True(result.Succeeded);
		Assert.Equal("/games/baldurs-gate-3", result.RedirectUrl);
		Assert.NotNull(request.ConsumedAt);
		Assert.NotNull(await userManager.FindByEmailAsync("consume-member@example.test"));
	}

	[Fact]
	public async Task MagicLinkConsumption_RejectsConsumedToken()
	{
		var emailSender = new CapturingAuthEmailSender();
		await using var scope = CreateAuthScope(emailSender);
		var service = scope.ServiceProvider.GetRequiredService<IMagicLinkService>();
		await service.RequestLoginLinkAsync(new MagicLinkRequestInput(
			"replay-member@example.test",
			"/",
			new Uri("https://example.test"),
			null,
			null));
		var token = ExtractToken(emailSender.LastLoginLink);

		var firstResult = await service.ConsumeLoginLinkAsync(token);
		var replayResult = await service.ConsumeLoginLinkAsync(token);

		Assert.True(firstResult.Succeeded);
		Assert.False(replayResult.Succeeded);
	}

	[Fact]
	public async Task MagicLinkConsumption_RejectsExpiredAndInvalidTokens()
	{
		var emailSender = new CapturingAuthEmailSender();
		var timeProvider = new MutableTimeProvider(DateTimeOffset.UtcNow);
		await using var scope = CreateAuthScope(emailSender, timeProvider);
		var service = scope.ServiceProvider.GetRequiredService<IMagicLinkService>();
		await service.RequestLoginLinkAsync(new MagicLinkRequestInput(
			"expired-member@example.test",
			"/",
			new Uri("https://example.test"),
			null,
			null));
		var token = ExtractToken(emailSender.LastLoginLink);

		timeProvider.UtcNow = timeProvider.UtcNow.AddMinutes(16);
		var expiredResult = await service.ConsumeLoginLinkAsync(token);
		var invalidResult = await service.ConsumeLoginLinkAsync("not-a-real-token");

		var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
		Assert.False(expiredResult.Succeeded);
		Assert.Equal("/login?failed=1", expiredResult.RedirectUrl);
		Assert.False(invalidResult.Succeeded);
		Assert.Equal("/login?failed=1", invalidResult.RedirectUrl);
		Assert.Null(await userManager.FindByEmailAsync("expired-member@example.test"));
	}

	[Fact]
	public async Task MagicLinkConsumption_NormalizesNonLocalReturnUrlToRoot()
	{
		var emailSender = new CapturingAuthEmailSender();
		await using var scope = CreateAuthScope(emailSender);
		var service = scope.ServiceProvider.GetRequiredService<IMagicLinkService>();
		await service.RequestLoginLinkAsync(new MagicLinkRequestInput(
			"unsafe-return@example.test",
			"https://evil.example.test/capture",
			new Uri("https://example.test"),
			null,
			null));

		var result = await service.ConsumeLoginLinkAsync(ExtractToken(emailSender.LastLoginLink));

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

	private AsyncServiceScope CreateAuthScope(
		CapturingAuthEmailSender emailSender,
		TimeProvider? timeProvider = null)
	{
		var services = new ServiceCollection();
		services.AddLogging(builder => builder.AddConsole());
		services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
		services.AddDbContext<CompatibilityDbContext>(options => options.UseNpgsql(fixture.ConnectionString));
		services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
		services.AddIdentityCore<ApplicationUser>(options =>
			{
				options.User.RequireUniqueEmail = true;
			})
			.AddEntityFrameworkStores<CompatibilityDbContext>()
			.AddSignInManager()
			.AddDefaultTokenProviders();
		services.AddScoped<IMagicLinkService, MagicLinkService>();
		services.AddSingleton<IAuthEmailSender>(emailSender);
		services.AddSingleton(timeProvider ?? TimeProvider.System);

		var serviceProvider = services.BuildServiceProvider();
		var scope = serviceProvider.CreateAsyncScope();
		var httpContextAccessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
		httpContextAccessor.HttpContext = new DefaultHttpContext
		{
			RequestServices = scope.ServiceProvider
		};

		return scope;
	}

	private static string ExtractToken(Uri loginLink)
	{
		var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(loginLink.Query);
		return Assert.Single(query["token"]) ?? string.Empty;
	}

	private sealed class CapturingAuthEmailSender : IAuthEmailSender
	{
		public Uri LastLoginLink { get; private set; } = null!;

		public Task SendLoginLinkAsync(string email, Uri loginLink, CancellationToken cancellationToken = default)
		{
			LastLoginLink = loginLink;
			return Task.CompletedTask;
		}
	}

	private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
	{
		public DateTimeOffset UtcNow { get; set; } = utcNow;

		public override DateTimeOffset GetUtcNow()
		{
			return UtcNow;
		}
	}
}
