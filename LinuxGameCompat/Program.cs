using LinuxGameCompat.Components;
using LinuxGameCompat.Data;
using LinuxGameCompat.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LinuxGameCompat.Services.EvidenceGeneration;
using LinuxGameCompat.Services.SummaryGeneration;

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
builder.Services.AddDataProtection();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IGameCompatibilityReadService, GameCompatibilityReadService>();
builder.Services.AddScoped<IMemberFavoritesService, MemberFavoritesService>();
builder.Services.AddScoped<IMagicLinkService, MagicLinkService>();
builder.Services.AddSingleton<MagicLinkDisplayHandoff>();
builder.Services.AddScoped<ICurrentMemberAccessor, CurrentMemberAccessor>();
builder.Services.AddSingleton(TimeProvider.System);
var generationSettings = new GenerationOptions();
builder.Configuration.GetSection(GenerationOptions.SectionName).Bind(generationSettings);
builder.Services.AddSingleton(generationSettings);
builder.Services.AddSingleton<IGenerationTokenCounter, OpenAiTokenCounter>();
builder.Services.AddSingleton<EvidencePromptBuilder>();
builder.Services.AddScoped<CompatibilitySummaryGenerator>();
builder.Services.AddSingleton<ICompatibilitySummaryProvider>(_ => OpenAiCompatibilitySummaryProvider.Create(
	Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? string.Empty,
	generationSettings));
var evidenceSettings = new EvidenceGenerationOptions();
builder.Configuration.GetSection(EvidenceGenerationOptions.SectionName).Bind(evidenceSettings);
builder.Services.AddSingleton(evidenceSettings);
builder.Services.AddSingleton<IEvidenceClaimTokenCounter, OpenAiEvidenceClaimTokenCounter>();
builder.Services.AddSingleton<ISourceFetchTransport>(_ => new SourceFetchTransport(SourceFetchTransport.CreateHttpClient(evidenceSettings), evidenceSettings));
builder.Services.AddSingleton<ProtonDbSourceAdapter>();
builder.Services.AddSingleton<AreWeAntiCheatYetSourceAdapter>();
builder.Services.AddSingleton<IEvidenceSourceFactsProvider, EvidenceSourceFactsProvider>();
builder.Services.AddSingleton<EvidenceClaimPromptBuilder>();
builder.Services.AddSingleton<IEvidenceClaimProvider>(_ => OpenAiEvidenceClaimProvider.Create(
	Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? string.Empty,
	evidenceSettings));
builder.Services.AddScoped<EvidenceClaimMaterializer>();
builder.Services.AddScoped<EvidenceRefreshService>();
builder.Services.AddScoped<CompatibilityRefreshOrchestrator>();
if (builder.Environment.IsDevelopment())
{
	builder.Services.AddScoped<IAuthEmailSender, LoggingAuthEmailSender>();
}
else
{
	builder.Services.AddScoped<IAuthEmailSender, SmtpAuthEmailSender>();
}

bool generationCommandRequested = RefreshCompatibilityCommand.IsRequested(args);
bool fakeProviderRequested = generationCommandRequested && builder.Environment.IsDevelopment() &&
	string.Equals(Environment.GetEnvironmentVariable("COMPATIBILITY_REFRESH_USE_FAKE_PROVIDERS"), "true", StringComparison.OrdinalIgnoreCase);
if (fakeProviderRequested)
	{
	builder.Services.AddSingleton<ICompatibilitySummaryProvider, FakeCompatibilitySummaryProvider>();
	builder.Services.AddSingleton<IEvidenceClaimProvider, FakeEvidenceClaimProvider>();
	}
var app = builder.Build();

if (generationCommandRequested)
{
	IReadOnlyList<string> configurationErrors = generationSettings.Validate().Concat(evidenceSettings.Validate()).ToArray();
	if (!RefreshCompatibilityCommand.TryParse(args, evidenceSettings.MaximumGames, out CompatibilityRefreshOptions? command, out string? parseError) || configurationErrors.Count > 0)
	{
		Console.Error.WriteLine(parseError ?? string.Join(" ", configurationErrors));
		return 2;
	}
	string? apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
	if (!fakeProviderRequested && string.IsNullOrWhiteSpace(apiKey))
	{
		Console.Error.WriteLine("OPENAI_API_KEY is required in generation mode.");
		return 2;
	}
	await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
	using CancellationTokenSource shutdown = new();
	ConsoleCancelEventHandler cancelHandler = (_, eventArgs) => { eventArgs.Cancel = true; shutdown.Cancel(); };
	Console.CancelKeyPress += cancelHandler;
	try
	{
		CompatibilityRefreshRunResult result = await scope.ServiceProvider.GetRequiredService<CompatibilityRefreshOrchestrator>().RunAsync(command!, shutdown.Token);
		Console.WriteLine(RefreshCompatibilityCommand.FormatResult(result));
		return RefreshCompatibilityCommand.ExitCodeFor(result);
	}
	catch (OperationCanceledException)
	{
		return 130;
	}
	finally
	{
		Console.CancelKeyPress -= cancelHandler;
	}
}

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
	MagicLinkDisplayHandoff magicLinkDisplayHandoff,
	IConfiguration configuration,
	CancellationToken cancellationToken) =>
{
	return await MagicLinkRequestEndpoint.HandleAsync(
		email,
		returnUrl,
		httpContext,
		magicLinkService,
		magicLinkDisplayHandoff,
		configuration,
		app.Environment.IsDevelopment(),
		cancellationToken);
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

await app.RunAsync();
return 0;
