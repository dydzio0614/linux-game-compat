using LinuxGameCompat.Components;
using LinuxGameCompat.Data;
using LinuxGameCompat.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
	.AddInteractiveServerComponents();

builder.Services.AddDbContext<CompatibilityDbContext>(options =>
	options.UseNpgsql(CompatibilityDbContextOptions.GetConnectionString(builder.Configuration)));
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
	.AddIdentityCookies();
builder.Services.AddIdentityCore<ApplicationUser>(options =>
	{
		options.User.RequireUniqueEmail = true;
		options.SignIn.RequireConfirmedAccount = false;
		options.SignIn.RequireConfirmedEmail = false;
	})
	.AddEntityFrameworkStores<CompatibilityDbContext>()
	.AddSignInManager()
	.AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options =>
{
	options.ExpireTimeSpan = TimeSpan.FromDays(30);
	options.SlidingExpiration = true;
	options.LoginPath = "/login";
	options.LogoutPath = "/logout";
	options.AccessDeniedPath = "/";
});
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IGameCompatibilityReadService, GameCompatibilityReadService>();
builder.Services.AddScoped<IMemberFavoritesService, MemberFavoritesService>();
builder.Services.AddScoped<IMagicLinkService, MagicLinkService>();
builder.Services.AddScoped<ICurrentMemberAccessor, CurrentMemberAccessor>();
builder.Services.AddSingleton(TimeProvider.System);
if (builder.Environment.IsDevelopment())
{
	builder.Services.AddScoped<IAuthEmailSender, LoggingAuthEmailSender>();
}
else
{
	builder.Services.AddScoped<IAuthEmailSender, SmtpAuthEmailSender>();
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Error", createScopeForErrors: true);
	// The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
	app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapPost("/auth/magic-link/request", async (
	[FromForm] string email,
	[FromForm] string? returnUrl,
	HttpContext httpContext,
	IMagicLinkService magicLinkService,
	IConfiguration configuration,
	CancellationToken cancellationToken) =>
{
	var publicBaseUri = AuthPublicBaseUriResolver.Resolve(
		configuration,
		httpContext.Request,
		app.Environment.IsDevelopment());
	var result = await magicLinkService.RequestLoginLinkAsync(
		new MagicLinkRequestInput(
			email,
			returnUrl,
			publicBaseUri,
			httpContext.Connection.RemoteIpAddress?.ToString(),
			httpContext.Request.Headers.UserAgent.ToString()),
		cancellationToken);

	return Results.Redirect(result.Accepted ? "/login?sent=1" : "/login?requestFailed=1");
}).DisableAntiforgery();
app.MapGet("/auth/magic-link/consume", async (
	string? token,
	IMagicLinkService magicLinkService,
	CancellationToken cancellationToken) =>
{
	var result = await magicLinkService.ConsumeLoginLinkAsync(token ?? string.Empty, cancellationToken);
	return Results.Redirect(result.RedirectUrl);
});
app.MapPost("/logout", async (HttpContext httpContext) =>
{
	await httpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
	return Results.Redirect("/");
}).RequireAuthorization()
	.WithMetadata(new RequireAntiforgeryTokenAttribute(required: true));
app.MapRazorComponents<App>()
	.AddInteractiveServerRenderMode();

app.Run();
