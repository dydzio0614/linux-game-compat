using LinuxGameCompat.Data;
using LinuxGameCompat.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
	public async Task MagicLinkRequest_AcceptedRequestDoesNotReturnLoginLinkByDefault()
	{
		var emailSender = new AuthTestHarness.CapturingAuthEmailSender();
		await using var harness = AuthTestHarness.Create(fixture, emailSender);

		var result = await harness.Service.RequestLoginLinkAsync(new MagicLinkRequestInput(
			"default-result@example.test",
			"/",
			new Uri("https://example.test"),
			null,
			null));

		Assert.True(result.Accepted);
		Assert.Null(result.LoginLink);
		Assert.NotNull(emailSender.LastLoginLink);
	}

	[Fact]
	public async Task MagicLinkRequest_InvalidOptInRequestDoesNotReturnLoginLink()
	{
		var emailSender = new AuthTestHarness.CapturingAuthEmailSender();
		await using var harness = AuthTestHarness.Create(fixture, emailSender);

		var result = await harness.Service.RequestLoginLinkAsync(new MagicLinkRequestInput(
			"not-an-email",
			"/",
			new Uri("https://example.test"),
			null,
			null,
			IncludeGeneratedLoginLink: true));

		Assert.False(result.Accepted);
		Assert.Null(result.LoginLink);
		Assert.Equal(0, emailSender.SendCount);
	}

	[Fact]
	public async Task MagicLinkRequest_OptInReturnsSameLoginLinkSentByEmailSender()
	{
		var emailSender = new AuthTestHarness.CapturingAuthEmailSender();
		await using var harness = AuthTestHarness.Create(fixture, emailSender);

		var result = await harness.Service.RequestLoginLinkAsync(new MagicLinkRequestInput(
			"frontend-shortcut@example.test",
			"/",
			new Uri("https://example.test"),
			null,
			null,
			IncludeGeneratedLoginLink: true));

		Assert.True(result.Accepted);
		Assert.Equal(emailSender.LastLoginLink, result.LoginLink);
	}

	[Fact]
	public async Task MagicLinkRequest_OptInSendFailureKeepsSavedRequestAndReturnsLoginLink()
	{
		var emailSender = new AuthTestHarness.ThrowingAuthEmailSender();
		var logProvider = new CapturingLoggerProvider();
		await using var harness = AuthTestHarness.Create(
			fixture,
			emailSender,
			configureLogging: builder =>
			{
				builder.ClearProviders();
				builder.AddProvider(logProvider);
			});

		var result = await harness.Service.RequestLoginLinkAsync(new MagicLinkRequestInput(
			"frontend-send-failure@example.test",
			"/",
			new Uri("https://example.test"),
			null,
			null,
			IncludeGeneratedLoginLink: true));

		var rawToken = AuthTestHarness.ExtractToken(emailSender.LastLoginLink);
		var warning = Assert.Single(logProvider.Entries, entry => entry.Level == LogLevel.Warning);
		var renderedWarning = $"{warning.Message}\n{warning.ExceptionText}";

		Assert.True(result.Accepted);
		Assert.Equal(emailSender.LastLoginLink, result.LoginLink);
		Assert.True(await harness.DbContext.MagicLinkRequests.AnyAsync(request => request.NormalizedEmail == "FRONTEND-SEND-FAILURE@EXAMPLE.TEST"));
		Assert.DoesNotContain(rawToken, renderedWarning, StringComparison.Ordinal);
		Assert.DoesNotContain("token=", renderedWarning, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain(emailSender.LastLoginLink.ToString(), renderedWarning, StringComparison.Ordinal);
	}

	[Fact]
	public async Task MagicLinkDisplayHandoff_SetConsumesOnceAndDeletesCookie()
	{
		var emailSender = new AuthTestHarness.CapturingAuthEmailSender();
		await using var harness = AuthTestHarness.Create(fixture, emailSender);
		var loginLink = new Uri("https://example.test/auth/magic-link/consume?token=frontend-token");
		var setContext = CreateHttpsContext();

		harness.MagicLinkDisplayHandoff.Set(setContext, loginLink);
		var cookieHeader = Assert.Single(setContext.Response.Headers.SetCookie)?.ToString()
			?? throw new InvalidOperationException("Expected a magic-link display cookie.");
		var consumeContext = CreateHttpsContext(cookieHeader.Split(';', 2)[0]);

		var consumed = harness.MagicLinkDisplayHandoff.TryConsume(consumeContext, out Uri? consumedLink);

		Assert.True(consumed);
		Assert.Equal(loginLink, consumedLink);
		Assert.Contains(
			consumeContext.Response.Headers.SetCookie,
			IsMagicLinkDisplayDeleteCookie);
	}

	[Fact]
	public async Task MagicLinkDisplayHandoff_ClearRemovesStaleDisplayCookie()
	{
		var emailSender = new AuthTestHarness.CapturingAuthEmailSender();
		await using var harness = AuthTestHarness.Create(fixture, emailSender);
		var context = CreateHttpsContext("LinuxGameCompat.MagicLinkDisplay=stale-value");

		harness.MagicLinkDisplayHandoff.Clear(context);

		Assert.Contains(
			context.Response.Headers.SetCookie,
			IsMagicLinkDisplayDeleteCookie);
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

	[Fact]
	public async Task MagicLinkRequest_StoresTokenHashWithoutRawToken()
	{
		var emailSender = new AuthTestHarness.CapturingAuthEmailSender();
		await using var harness = AuthTestHarness.Create(fixture, emailSender);

		await harness.Service.RequestLoginLinkAsync(new MagicLinkRequestInput(
			"token-persistence@example.test",
			"/",
			new Uri("https://example.test"),
			null,
			null));

		var rawToken = AuthTestHarness.ExtractToken(emailSender.LastLoginLink);
		var request = await harness.DbContext.MagicLinkRequests.SingleAsync(request => request.NormalizedEmail == "TOKEN-PERSISTENCE@EXAMPLE.TEST");

		Assert.NotEqual(rawToken, request.TokenHash);
		Assert.DoesNotContain(rawToken, request.TokenHash, StringComparison.Ordinal);
		Assert.Matches("^[A-F0-9]{64}$", request.TokenHash);
	}

	[Fact]
	public async Task MagicLinkRequest_SendFailureLogsNoRawTokenOrLoginLinkAndRemovesSavedRequest()
	{
		var emailSender = new AuthTestHarness.ThrowingAuthEmailSender();
		var logProvider = new CapturingLoggerProvider();
		await using var harness = AuthTestHarness.Create(
			fixture,
			emailSender,
			configureLogging: builder =>
			{
				builder.ClearProviders();
				builder.AddProvider(logProvider);
			});

		var result = await harness.Service.RequestLoginLinkAsync(new MagicLinkRequestInput(
			"send-failure@example.test",
			"/",
			new Uri("https://example.test"),
			null,
			null));

		var rawToken = AuthTestHarness.ExtractToken(emailSender.LastLoginLink);
		var warning = Assert.Single(logProvider.Entries, entry => entry.Level == LogLevel.Warning);
		var renderedWarning = $"{warning.Message}\n{warning.ExceptionText}";

		Assert.False(result.Accepted);
		Assert.Empty(await harness.DbContext.MagicLinkRequests.Where(request => request.NormalizedEmail == "SEND-FAILURE@EXAMPLE.TEST").ToArrayAsync());
		Assert.Contains("SEND-FAILURE@EXAMPLE.TEST", warning.Message, StringComparison.Ordinal);
		Assert.DoesNotContain(rawToken, renderedWarning, StringComparison.Ordinal);
		Assert.DoesNotContain("token=", renderedWarning, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain(emailSender.LastLoginLink.ToString(), renderedWarning, StringComparison.Ordinal);
	}

	[Fact]
	public void SmtpAuthEmailSender_ComposesProductionPlainTextLoginMessage()
	{
		var loginLink = new Uri("https://example.test/auth/magic-link/consume?token=abc123");

		var message = SmtpAuthEmailSender.ComposeLoginLinkMessage(
			"login@example.test",
			"member@example.test",
			loginLink);

		Assert.Equal("login@example.test", message.Sender);
		Assert.Equal("member@example.test", message.Recipient);
		Assert.Equal("Your LinuxGameCompat login link", message.Subject);
		Assert.False(message.IsBodyHtml);
		Assert.Equal($"Use this link to sign in: {loginLink}", message.Body);
		Assert.DoesNotContain("abc123", message.Subject, StringComparison.Ordinal);
		Assert.DoesNotContain("token=", message.Subject, StringComparison.OrdinalIgnoreCase);
		Assert.Equal(1, CountOccurrences(message.Body, loginLink.ToString()));
		Assert.Equal(1, CountOccurrences(message.Body, "token="));
	}

	[Fact]
	public async Task LoggingAuthEmailSender_LogsFullLinkAsDevelopmentOnlyException()
	{
		var logProvider = new CapturingLoggerProvider();
		using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logProvider));
		var sender = new LoggingAuthEmailSender(loggerFactory.CreateLogger<LoggingAuthEmailSender>());
		var loginLink = new Uri("https://localhost/auth/magic-link/consume?token=development-smoke-token");

		await sender.SendLoginLinkAsync("member@example.test", loginLink);

		var entry = Assert.Single(logProvider.Entries, entry => entry.Category == typeof(LoggingAuthEmailSender).FullName);
		Assert.Equal(LogLevel.Information, entry.Level);
		Assert.Contains(loginLink.ToString(), entry.Message, StringComparison.Ordinal);
		Assert.Contains("development-smoke-token", entry.Message, StringComparison.Ordinal);
		Assert.Contains("member@example.test", entry.Message, StringComparison.Ordinal);
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

	private static int CountOccurrences(string value, string search)
	{
		var count = 0;
		var index = 0;

		while ((index = value.IndexOf(search, index, StringComparison.Ordinal)) >= 0)
		{
			count++;
			index += search.Length;
		}

		return count;
	}

	private static DefaultHttpContext CreateHttpsContext(string? cookieHeader = null)
	{
		var context = new DefaultHttpContext();
		context.Request.Scheme = "https";
		context.Request.Host = new HostString("example.test");
		if (!string.IsNullOrWhiteSpace(cookieHeader))
		{
			context.Request.Headers.Cookie = cookieHeader;
		}

		return context;
	}

	private static bool IsMagicLinkDisplayDeleteCookie(string? header)
	{
		return header is not null &&
			header.StartsWith("LinuxGameCompat.MagicLinkDisplay=", StringComparison.Ordinal) &&
			header.Contains("expires=", StringComparison.OrdinalIgnoreCase);
	}

	private sealed class CapturingLoggerProvider : ILoggerProvider
	{
		private readonly List<LogEntry> _entries = [];

		public IReadOnlyList<LogEntry> Entries => _entries;

		public ILogger CreateLogger(string categoryName)
		{
			return new CapturingLogger(categoryName, _entries);
		}

		public void Dispose()
		{
		}
	}

	private sealed class CapturingLogger(string category, List<LogEntry> entries) : ILogger
	{
		public IDisposable? BeginScope<TState>(TState state)
			where TState : notnull
		{
			return null;
		}

		public bool IsEnabled(LogLevel logLevel)
		{
			return true;
		}

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter)
		{
			entries.Add(new LogEntry(
				category,
				logLevel,
				formatter(state, exception),
				exception?.ToString() ?? string.Empty));
		}
	}

	private sealed record LogEntry(string Category, LogLevel Level, string Message, string ExceptionText);
}
