using LinuxGameCompat.Data;
using LinuxGameCompat.Services;
using Microsoft.EntityFrameworkCore;

namespace LinuxGameCompat.Tests;

public sealed class AuthPrivacyRegressionTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
	[Theory]
	[InlineData(null, "/")]
	[InlineData("", "/")]
	[InlineData("   ", "/")]
	[InlineData("https://evil.example/capture", "/")]
	[InlineData("//evil.example/capture", "/")]
	[InlineData(@"/\evil.example", "/")]
	[InlineData(@"/games\evil.example", "/")]
	[InlineData("/%5Cevil.example", "/")]
	[InlineData("/%5cevil.example", "/")]
	[InlineData("/%2Fevil.example", "/")]
	[InlineData("/%2fevil.example", "/")]
	[InlineData("/games/baldurs-gate-3", "/games/baldurs-gate-3")]
	public void LocalReturnUrlNormalizer_NormalizesLocalRedirectBoundaries(string? returnUrl, string expected)
	{
		Assert.Equal(expected, LocalReturnUrlNormalizer.Normalize(returnUrl));
	}

	[Fact]
	public void LocalReturnUrlNormalizer_RejectsOverlongReturnUrls()
	{
		var overlongReturnUrl = $"/{new string('a', 2050)}";

		Assert.Equal("/", LocalReturnUrlNormalizer.Normalize(overlongReturnUrl));
	}

	[Fact]
	public async Task MagicLinkRequest_ExistingAndNewEmailsHaveEquivalentAcceptedOutcomes()
	{
		var emailSender = new AuthTestHarness.CapturingAuthEmailSender();
		await using var harness = AuthTestHarness.Create(fixture, emailSender);
		await CreateMemberAsync(harness, "existing-equivalent@example.test");

		var existingResult = await harness.Service.RequestLoginLinkAsync(new MagicLinkRequestInput(
			"existing-equivalent@example.test",
			"/",
			new Uri("https://example.test"),
			null,
			null));
		var existingSendCount = emailSender.SendCount;

		var newResult = await harness.Service.RequestLoginLinkAsync(new MagicLinkRequestInput(
			"new-equivalent@example.test",
			"/",
			new Uri("https://example.test"),
			null,
			null));

		Assert.True(existingResult.Accepted);
		Assert.True(newResult.Accepted);
		Assert.Equal(1, existingSendCount);
		Assert.Equal(2, emailSender.SendCount);
		Assert.True(await harness.DbContext.MagicLinkRequests.AnyAsync(request => request.NormalizedEmail == "EXISTING-EQUIVALENT@EXAMPLE.TEST"));
		Assert.True(await harness.DbContext.MagicLinkRequests.AnyAsync(request => request.NormalizedEmail == "NEW-EQUIVALENT@EXAMPLE.TEST"));
	}

	[Fact]
	public async Task MagicLinkConsumption_InvalidExpiredAndConsumedTokensDoNotCreateOrAdvanceMembers()
	{
		var emailSender = new AuthTestHarness.CapturingAuthEmailSender();
		var timeProvider = new AuthTestHarness.MutableTimeProvider(DateTimeOffset.UtcNow);
		await using var harness = AuthTestHarness.Create(fixture, emailSender, timeProvider);

		await harness.Service.RequestLoginLinkAsync(new MagicLinkRequestInput(
			"expired-regression@example.test",
			"/",
			new Uri("https://example.test"),
			null,
			null));
		var expiredToken = AuthTestHarness.ExtractToken(emailSender.LastLoginLink);
		timeProvider.UtcNow = timeProvider.UtcNow.AddMinutes(16);

		var expiredResult = await harness.Service.ConsumeLoginLinkAsync(expiredToken);
		var invalidResult = await harness.Service.ConsumeLoginLinkAsync("not-a-real-token");

		timeProvider.UtcNow = DateTimeOffset.UtcNow;
		await harness.Service.RequestLoginLinkAsync(new MagicLinkRequestInput(
			"replay-regression@example.test",
			"/",
			new Uri("https://example.test"),
			null,
			null));
		var replayToken = AuthTestHarness.ExtractToken(emailSender.LastLoginLink);

		var firstResult = await harness.Service.ConsumeLoginLinkAsync(replayToken);
		var replayResult = await harness.Service.ConsumeLoginLinkAsync(replayToken);

		Assert.False(expiredResult.Succeeded);
		Assert.Equal("/login?failed=1", expiredResult.RedirectUrl);
		Assert.False(invalidResult.Succeeded);
		Assert.Equal("/login?failed=1", invalidResult.RedirectUrl);
		Assert.True(firstResult.Succeeded);
		Assert.False(replayResult.Succeeded);
		Assert.Equal("/login?failed=1", replayResult.RedirectUrl);
		Assert.Null(await harness.UserManager.FindByEmailAsync("expired-regression@example.test"));
		Assert.Null(await harness.DbContext.Users.SingleOrDefaultAsync(user => user.Email == "not-a-real-token"));
		Assert.NotNull(await harness.UserManager.FindByEmailAsync("replay-regression@example.test"));
	}

	[Theory]
	[InlineData("https://evil.example/capture")]
	[InlineData("//evil.example/capture")]
	[InlineData(@"/\evil.example")]
	[InlineData("/%5Cevil.example")]
	[InlineData("/%2Fevil.example")]
	public async Task MagicLinkConsumption_UnsafeReturnUrlsNormalizeToRoot(string returnUrl)
	{
		var emailSender = new AuthTestHarness.CapturingAuthEmailSender();
		await using var harness = AuthTestHarness.Create(fixture, emailSender);
		var email = $"{Guid.NewGuid():N}@example.test";
		var normalizedEmail = email.ToUpperInvariant();

		await harness.Service.RequestLoginLinkAsync(new MagicLinkRequestInput(
			email,
			returnUrl,
			new Uri("https://example.test"),
			null,
			null));

		var request = await harness.DbContext.MagicLinkRequests.SingleAsync(request => request.NormalizedEmail == normalizedEmail);
		var result = await harness.Service.ConsumeLoginLinkAsync(AuthTestHarness.ExtractToken(emailSender.LastLoginLink));

		Assert.Equal("/", request.ReturnUrl);
		Assert.True(result.Succeeded);
		Assert.Equal("/", result.RedirectUrl);
	}

	[Fact]
	public async Task MagicLinkConsumption_OrdinaryLocalReturnUrlRoundTrips()
	{
		var emailSender = new AuthTestHarness.CapturingAuthEmailSender();
		await using var harness = AuthTestHarness.Create(fixture, emailSender);

		await harness.Service.RequestLoginLinkAsync(new MagicLinkRequestInput(
			"ordinary-return@example.test",
			"/games/baldurs-gate-3",
			new Uri("https://example.test"),
			null,
			null));

		var request = await harness.DbContext.MagicLinkRequests.SingleAsync(request => request.NormalizedEmail == "ORDINARY-RETURN@EXAMPLE.TEST");
		var result = await harness.Service.ConsumeLoginLinkAsync(AuthTestHarness.ExtractToken(emailSender.LastLoginLink));

		Assert.Equal("/games/baldurs-gate-3", request.ReturnUrl);
		Assert.True(result.Succeeded);
		Assert.Equal("/games/baldurs-gate-3", result.RedirectUrl);
	}

	private static async Task CreateMemberAsync(AuthTestHarness harness, string email)
	{
		var user = new ApplicationUser
		{
			UserName = email.ToUpperInvariant(),
			Email = email.ToUpperInvariant(),
			EmailConfirmed = true
		};

		var result = await harness.UserManager.CreateAsync(user);
		Assert.True(result.Succeeded);
	}
}
