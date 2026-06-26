using LinuxGameCompat.Components;
using LinuxGameCompat.Data;
using LinuxGameCompat.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
builder.Services.AddScoped<ICompatibilitySummaryGenerator, CompatibilitySummaryGenerator>();
builder.Services.AddSingleton<ICompatibilitySummaryProvider>(_ => OpenAiCompatibilitySummaryProvider.Create(
	Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? string.Empty,
	generationSettings));
if (builder.Environment.IsDevelopment())
{
	builder.Services.AddScoped<IAuthEmailSender, LoggingAuthEmailSender>();
}
else
{
	builder.Services.AddScoped<IAuthEmailSender, SmtpAuthEmailSender>();
}

bool generationCommandRequested = GenerateSummariesCommand.IsRequested(args);
bool fakeProviderRequested = generationCommandRequested && builder.Environment.IsDevelopment() &&
	string.Equals(Environment.GetEnvironmentVariable("SUMMARY_GENERATION_USE_FAKE_PROVIDER"), "true", StringComparison.OrdinalIgnoreCase);
if (fakeProviderRequested)
	builder.Services.AddSingleton<ICompatibilitySummaryProvider, FakeCompatibilitySummaryProvider>();
var app = builder.Build();

if (generationCommandRequested)
{
	IReadOnlyList<string> configurationErrors = generationSettings.Validate();
	if (!GenerateSummariesCommand.TryParse(args, generationSettings.MaximumGames, out GenerateSummariesCommandOptions? command, out string? parseError) || configurationErrors.Count > 0)
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
		SummaryGenerationRunResult result = await scope.ServiceProvider.GetRequiredService<ICompatibilitySummaryGenerator>().RunAsync(
			new SummaryGenerationRunOptions(command!.Limit, command.Slug, command.Force), shutdown.Token);
		Console.WriteLine(GenerateSummariesCommand.FormatResult(result));
		return GenerateSummariesCommand.ExitCodeFor(result);
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
	magicLinkDisplayHandoff.Clear(httpContext);
	var showMagicLinksInFrontend = configuration.GetValue<bool>("Auth:ShowMagicLinksInFrontend");
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
			httpContext.Request.Headers.UserAgent.ToString(),
			IncludeGeneratedLoginLink: showMagicLinksInFrontend),
		cancellationToken);
	if (result.Accepted && result.LoginLink is not null)
	{
		magicLinkDisplayHandoff.Set(httpContext, result.LoginLink);
	}
	else
	{
		magicLinkDisplayHandoff.Clear(httpContext);
	}

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

await app.RunAsync();
return 0;
