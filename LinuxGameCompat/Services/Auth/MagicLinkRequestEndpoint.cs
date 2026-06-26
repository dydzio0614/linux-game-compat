using Microsoft.AspNetCore.Mvc;

namespace LinuxGameCompat.Services;

internal static class MagicLinkRequestEndpoint
{
	public static async Task<IResult> HandleAsync(
		[FromForm] string email,
		[FromForm] string? returnUrl,
		HttpContext httpContext,
		IMagicLinkService magicLinkService,
		MagicLinkDisplayHandoff magicLinkDisplayHandoff,
		IConfiguration configuration,
		bool isDevelopment,
		CancellationToken cancellationToken)
	{
		magicLinkDisplayHandoff.Clear(httpContext);
		bool showMagicLinksInFrontend = configuration.GetValue<bool>("Auth:ShowMagicLinksInFrontend");
		Uri publicBaseUri = AuthPublicBaseUriResolver.Resolve(
			configuration,
			httpContext.Request,
			isDevelopment);
		MagicLinkRequestResult result = await magicLinkService.RequestLoginLinkAsync(
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
	}
}
