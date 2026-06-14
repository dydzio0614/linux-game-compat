using LinuxGameCompat.Data;
using LinuxGameCompat.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LinuxGameCompat.Tests;

public sealed class AuthTestHarness : IAsyncDisposable
{
	private readonly ServiceProvider _serviceProvider;
	private readonly AsyncServiceScope _scope;

	private AuthTestHarness(ServiceProvider serviceProvider, AsyncServiceScope scope)
	{
		_serviceProvider = serviceProvider;
		_scope = scope;
	}

	public IServiceProvider ServiceProvider => _scope.ServiceProvider;

	public CompatibilityDbContext DbContext => ServiceProvider.GetRequiredService<CompatibilityDbContext>();

	public IMagicLinkService Service => ServiceProvider.GetRequiredService<IMagicLinkService>();

	public IMemberFavoritesService FavoritesService => ServiceProvider.GetRequiredService<IMemberFavoritesService>();

	public UserManager<ApplicationUser> UserManager => ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

	public static AuthTestHarness Create(
		PostgreSqlFixture fixture,
		IAuthEmailSender emailSender,
		TimeProvider? timeProvider = null,
		Action<ILoggingBuilder>? configureLogging = null)
	{
		var services = new ServiceCollection();
		services.AddLogging(configureLogging ?? (builder => builder.AddConsole()));
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
		services.AddScoped<IMemberFavoritesService, MemberFavoritesService>();
		services.AddScoped<ICurrentMemberAccessor, CurrentMemberAccessor>();
		services.AddSingleton<IAuthEmailSender>(emailSender);
		services.AddSingleton(timeProvider ?? TimeProvider.System);

		var serviceProvider = services.BuildServiceProvider();
		var scope = serviceProvider.CreateAsyncScope();
		var httpContextAccessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
		httpContextAccessor.HttpContext = new DefaultHttpContext
		{
			RequestServices = scope.ServiceProvider
		};

		return new AuthTestHarness(serviceProvider, scope);
	}

	public async Task<ApplicationUser> CreateAuthenticatedUserAsync(string email)
	{
		var user = new ApplicationUser
		{
			UserName = email,
			Email = email,
			EmailConfirmed = true
		};
		var result = await UserManager.CreateAsync(user);
		Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Description)));
		SetCurrentUser(user);
		return user;
	}

	public void SetCurrentUser(ApplicationUser user)
	{
		var claims = new[]
		{
			new Claim(ClaimTypes.NameIdentifier, user.Id),
			new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
			new Claim(ClaimTypes.Name, user.UserName ?? user.Email ?? string.Empty)
		};
		var identity = new ClaimsIdentity(claims, IdentityConstants.ApplicationScheme);
		var httpContext = ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext
			?? throw new InvalidOperationException("Test HTTP context was not initialized.");

		httpContext.User = new ClaimsPrincipal(identity);
	}

	public async ValueTask DisposeAsync()
	{
		await _scope.DisposeAsync();
		await _serviceProvider.DisposeAsync();
	}

	public static string ExtractToken(Uri loginLink)
	{
		var query = QueryHelpers.ParseQuery(loginLink.Query);
		return Assert.Single(query["token"]) ?? string.Empty;
	}

	public sealed class CapturingAuthEmailSender : IAuthEmailSender
	{
		public Uri LastLoginLink { get; private set; } = null!;

		public int SendCount { get; private set; }

		public Task SendLoginLinkAsync(string email, Uri loginLink, CancellationToken cancellationToken = default)
		{
			SendCount++;
			LastLoginLink = loginLink;
			return Task.CompletedTask;
		}
	}

	public sealed class ThrowingAuthEmailSender : IAuthEmailSender
	{
		public Uri LastLoginLink { get; private set; } = null!;

		public Task SendLoginLinkAsync(string email, Uri loginLink, CancellationToken cancellationToken = default)
		{
			LastLoginLink = loginLink;
			throw new InvalidOperationException($"SMTP unavailable for {loginLink}");
		}
	}

	public sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
	{
		public DateTimeOffset UtcNow { get; set; } = utcNow;

		public override DateTimeOffset GetUtcNow()
		{
			return UtcNow;
		}
	}
}
