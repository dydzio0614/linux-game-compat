namespace LinuxGameCompat.Services;

internal static class AuthPublicBaseUriResolver
{
	public static Uri Resolve(IConfiguration configuration, HttpRequest request, bool isDevelopment)
	{
		var configuredBaseUrl = configuration["Auth:PublicBaseUrl"];
		if (Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var configuredUri))
		{
			if (isDevelopment || configuredUri.Scheme == Uri.UriSchemeHttps)
			{
				return configuredUri;
			}

			throw new InvalidOperationException("Auth:PublicBaseUrl must use HTTPS outside Development.");
		}

		if (isDevelopment)
		{
			return new Uri($"{request.Scheme}://{request.Host}");
		}

		throw new InvalidOperationException("Auth:PublicBaseUrl must be configured as an absolute HTTPS URL outside Development.");
	}
}
