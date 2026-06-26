using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace LinuxGameCompat.Services;

public sealed class MagicLinkDisplayHandoff(IDataProtectionProvider dataProtectionProvider)
{
	private const string CookieName = "LinuxGameCompat.MagicLinkDisplay";
	private static readonly TimeSpan CookieLifetime = TimeSpan.FromMinutes(5);
	private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector(
		"LinuxGameCompat.Auth.MagicLinkDisplayHandoff.v1");

	public void Set(HttpContext httpContext, Uri loginLink)
	{
		var protectedValue = _protector.Protect(loginLink.ToString());
		httpContext.Response.Cookies.Append(CookieName, protectedValue, CreateCookieOptions(httpContext));
	}

	public bool TryConsume(HttpContext httpContext, out Uri? loginLink)
	{
		loginLink = null;
		if (!httpContext.Request.Cookies.TryGetValue(CookieName, out var protectedValue))
		{
			return false;
		}

		Clear(httpContext);
		try
		{
			var unprotectedValue = _protector.Unprotect(protectedValue);
			return Uri.TryCreate(unprotectedValue, UriKind.Absolute, out loginLink);
		}
		catch (CryptographicException)
		{
			return false;
		}
	}

	public void Clear(HttpContext httpContext)
	{
		httpContext.Response.Cookies.Delete(CookieName, CreateCookieOptions(httpContext));
	}

	private static CookieOptions CreateCookieOptions(HttpContext httpContext)
	{
		return new CookieOptions
		{
			HttpOnly = true,
			IsEssential = true,
			MaxAge = CookieLifetime,
			Path = "/login",
			SameSite = SameSiteMode.Lax,
			Secure = httpContext.Request.IsHttps
		};
	}
}
