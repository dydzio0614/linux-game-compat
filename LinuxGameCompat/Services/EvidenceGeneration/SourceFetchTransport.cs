using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace LinuxGameCompat.Services.EvidenceGeneration;

public sealed record SourceFetchRequest(
	Uri Uri,
	Func<Uri, bool> IsAllowedUri,
	bool AllowPlainText = false);

public sealed record SourceFetchResult(JsonDocument Document, string? ETag, DateTimeOffset? LastModifiedAt) : IDisposable
{
	public void Dispose() => Document.Dispose();
}

public interface ISourceFetchTransport
{
	Task<SourceFetchResult> FetchAsync(SourceFetchRequest request, CancellationToken cancellationToken);
}

public sealed class SourceFetchTransport(HttpClient httpClient, EvidenceGenerationOptions options) : ISourceFetchTransport
{
	private const int MaximumRedirects = 2;

	public async Task<SourceFetchResult> FetchAsync(SourceFetchRequest request, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		if (!request.IsAllowedUri(request.Uri)) throw new EvidenceSourceException("invalid_url", "The source fetch URL is not allowed.");

		Uri current = request.Uri;
		for (int redirects = 0; ; redirects++)
		{
			using HttpRequestMessage message = new(HttpMethod.Get, current);
			using HttpResponseMessage response = await httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
			if (IsRedirect(response.StatusCode))
			{
				if (redirects >= MaximumRedirects) throw new EvidenceSourceException("too_many_redirects", "The source returned too many redirects.");
				Uri? location = response.Headers.Location;
				if (location is null) throw new EvidenceSourceException("invalid_redirect", "The source returned a redirect without a location.");
				Uri target = location.IsAbsoluteUri ? location : new Uri(current, location);
				if (!request.IsAllowedUri(target)) throw new EvidenceSourceException("invalid_redirect", "The source redirected to a URL outside its allowlist.");
				current = target;
				continue;
			}

			if (response.StatusCode == HttpStatusCode.NotFound) throw new EvidenceSourceException("not_found", "The source record was not found.");
			if (!response.IsSuccessStatusCode) throw new EvidenceSourceException("http_error", $"The source returned HTTP {(int)response.StatusCode}.");
			bool allowPlainText = request.AllowPlainText &&
				string.Equals(current.AbsoluteUri, AreWeAntiCheatYetSourceAdapter.DataUrl, StringComparison.Ordinal);
			ValidateContentType(response.Content.Headers.ContentType, allowPlainText);

			await using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken);
			using MemoryStream bounded = new();
			byte[] buffer = new byte[16 * 1024];
			while (true)
			{
				int read = await source.ReadAsync(buffer, cancellationToken);
				if (read == 0) break;
				if (bounded.Length + read > options.MaximumResponseBytes)
					throw new EvidenceSourceException("response_too_large", "The decompressed source response exceeded the configured byte limit.");
				bounded.Write(buffer, 0, read);
			}
			bounded.Position = 0;
			try
			{
				JsonDocument document = await JsonDocument.ParseAsync(bounded, cancellationToken: cancellationToken);
				return new SourceFetchResult(document, response.Headers.ETag?.ToString(), response.Content.Headers.LastModified);
			}
			catch (JsonException exception)
			{
				throw new EvidenceSourceException("invalid_json", "The source response was not valid JSON.", exception);
			}
		}
	}

	public static HttpClient CreateHttpClient(EvidenceGenerationOptions options)
	{
		HttpClientHandler handler = new()
		{
			AllowAutoRedirect = false,
			AutomaticDecompression = DecompressionMethods.All
		};
		return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(options.FetchTimeoutSeconds) };
	}

	private static bool IsRedirect(HttpStatusCode statusCode) => statusCode is
		HttpStatusCode.Moved or HttpStatusCode.Redirect or HttpStatusCode.RedirectMethod or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;

	private static void ValidateContentType(MediaTypeHeaderValue? contentType, bool allowPlainText)
	{
		string? mediaType = contentType?.MediaType;
		if (string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase)) return;
		if (allowPlainText && string.Equals(mediaType, "text/plain", StringComparison.OrdinalIgnoreCase)) return;
		throw new EvidenceSourceException("invalid_content_type", "The source response content type is not allowed.");
	}
}
