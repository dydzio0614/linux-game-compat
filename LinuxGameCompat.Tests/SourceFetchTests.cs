using System.Net;
using System.Net.Http.Headers;
using System.Text;
using LinuxGameCompat.Services.EvidenceGeneration;

namespace LinuxGameCompat.Tests;

public sealed class SourceFetchTests
{
	[Fact]
	public async Task Fetch_accepts_json_and_returns_cache_metadata()
	{
		SequenceHandler handler = new(Response(HttpStatusCode.OK, "application/json", "{\"ok\":true}", etag: "\"v1\""));
		SourceFetchTransport transport = CreateTransport(handler);

		using SourceFetchResult result = await transport.FetchAsync(Request("https://source.test/data.json"), CancellationToken.None);

		Assert.True(result.Document.RootElement.GetProperty("ok").GetBoolean());
		Assert.Equal("\"v1\"", result.ETag);
	}

	[Fact]
	public async Task Fetch_follows_at_most_two_revalidated_redirects()
	{
		SequenceHandler handler = new(
			Redirect("/second.json"),
			Redirect("https://source.test/final.json"),
			Response(HttpStatusCode.OK, "application/json", "{}"));
		SourceFetchTransport transport = CreateTransport(handler);
		SourceFetchRequest request = new(new Uri("https://source.test/start.json"), uri => uri.Host == "source.test");

		using SourceFetchResult result = await transport.FetchAsync(request, CancellationToken.None);

		Assert.Equal(3, handler.CallCount);
		Assert.Equal("https://source.test/final.json", handler.RequestUris[^1].AbsoluteUri);
	}

	[Fact]
	public async Task Fetch_rejects_redirect_outside_allowlist()
	{
		SequenceHandler handler = new(Redirect("https://attacker.test/data.json"));
		SourceFetchTransport transport = CreateTransport(handler);

		EvidenceSourceException exception = await Assert.ThrowsAsync<EvidenceSourceException>(() =>
			transport.FetchAsync(Request("https://source.test/data.json"), CancellationToken.None));

		Assert.Equal("invalid_redirect", exception.Code);
		Assert.Equal(1, handler.CallCount);
	}

	[Fact]
	public async Task Fetch_rejects_third_redirect()
	{
		SequenceHandler handler = new(Redirect("/2"), Redirect("/3"), Redirect("/4"));
		SourceFetchTransport transport = CreateTransport(handler);

		EvidenceSourceException exception = await Assert.ThrowsAsync<EvidenceSourceException>(() =>
			transport.FetchAsync(new SourceFetchRequest(new Uri("https://source.test/1"), uri => uri.Host == "source.test"), CancellationToken.None));

		Assert.Equal("too_many_redirects", exception.Code);
	}

	[Fact]
	public async Task Fetch_allows_plain_text_only_when_explicitly_requested()
	{
		SourceFetchTransport rejectedTransport = CreateTransport(new SequenceHandler(Response(HttpStatusCode.OK, "text/plain", "{}")));
		Assert.Equal("invalid_content_type", (await Assert.ThrowsAsync<EvidenceSourceException>(() =>
			rejectedTransport.FetchAsync(Request("https://source.test/data.json"), CancellationToken.None))).Code);

		SourceFetchTransport allowedTransport = CreateTransport(new SequenceHandler(Response(HttpStatusCode.OK, "text/plain", "{}")));
		using SourceFetchResult result = await allowedTransport.FetchAsync(
			new SourceFetchRequest(new Uri(AreWeAntiCheatYetSourceAdapter.DataUrl), _ => true, AllowPlainText: true), CancellationToken.None);
		Assert.Equal(System.Text.Json.JsonValueKind.Object, result.Document.RootElement.ValueKind);
	}

	[Fact]
	public async Task Fetch_rejects_plain_text_exception_for_any_other_target()
	{
		SourceFetchTransport transport = CreateTransport(new SequenceHandler(Response(HttpStatusCode.OK, "text/plain", "{}")));
		SourceFetchRequest request = new(new Uri("https://source.test/data.json"), _ => true, AllowPlainText: true);

		EvidenceSourceException exception = await Assert.ThrowsAsync<EvidenceSourceException>(() =>
			transport.FetchAsync(request, CancellationToken.None));

		Assert.Equal("invalid_content_type", exception.Code);
	}

	[Fact]
	public async Task Fetch_stops_when_decompressed_byte_limit_is_exceeded()
	{
		EvidenceGenerationOptions options = EvidenceSourceAdapterTests.ValidOptions();
		options.MaximumResponseBytes = 4;
		SourceFetchTransport transport = new(new HttpClient(new SequenceHandler(Response(HttpStatusCode.OK, "application/json", "{\"large\":true}"))), options);

		EvidenceSourceException exception = await Assert.ThrowsAsync<EvidenceSourceException>(() =>
			transport.FetchAsync(Request("https://source.test/data.json"), CancellationToken.None));

		Assert.Equal("response_too_large", exception.Code);
	}

	[Fact]
	public async Task Fetch_rejects_invalid_json_and_http_failures()
	{
		SourceFetchTransport invalidJson = CreateTransport(new SequenceHandler(Response(HttpStatusCode.OK, "application/json", "not json")));
		Assert.Equal("invalid_json", (await Assert.ThrowsAsync<EvidenceSourceException>(() =>
			invalidJson.FetchAsync(Request("https://source.test/data.json"), CancellationToken.None))).Code);

		SourceFetchTransport notFound = CreateTransport(new SequenceHandler(Response(HttpStatusCode.NotFound, "application/json", "{}")));
		Assert.Equal("not_found", (await Assert.ThrowsAsync<EvidenceSourceException>(() =>
			notFound.FetchAsync(Request("https://source.test/data.json"), CancellationToken.None))).Code);
	}

	[Fact]
	public async Task Fetch_timeout_bounds_a_stalled_response_body()
	{
		EvidenceGenerationOptions options = EvidenceSourceAdapterTests.ValidOptions();
		options.FetchTimeoutSeconds = 1;
		HttpResponseMessage response = new(HttpStatusCode.OK) { Content = new StreamContent(new StallingStream()) };
		response.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
		SourceFetchTransport transport = new(new HttpClient(new SequenceHandler(response)), options);

		EvidenceSourceException exception = await Assert.ThrowsAsync<EvidenceSourceException>(() =>
			transport.FetchAsync(Request("https://source.test/data.json"), CancellationToken.None));

		Assert.Equal("fetch_timeout", exception.Code);
	}

	private static SourceFetchTransport CreateTransport(HttpMessageHandler handler) =>
		new(new HttpClient(handler), EvidenceSourceAdapterTests.ValidOptions());

	private static SourceFetchRequest Request(string url) => new(new Uri(url), uri => uri.Host == "source.test");

	private static HttpResponseMessage Redirect(string location)
	{
		HttpResponseMessage response = new(HttpStatusCode.Found);
		response.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);
		return response;
	}

	private static HttpResponseMessage Response(HttpStatusCode status, string contentType, string body, string? etag = null)
	{
		HttpResponseMessage response = new(status)
		{
			Content = new ByteArrayContent(Encoding.UTF8.GetBytes(body))
		};
		response.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
		if (etag is not null) response.Headers.ETag = EntityTagHeaderValue.Parse(etag);
		return response;
	}

	private sealed class SequenceHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
	{
		private readonly Queue<HttpResponseMessage> _responses = new(responses);
		public int CallCount { get; private set; }
		public List<Uri> RequestUris { get; } = [];

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			CallCount++;
			RequestUris.Add(request.RequestUri!);
			return Task.FromResult(_responses.Dequeue());
		}
	}

	private sealed class StallingStream : Stream
	{
		public override bool CanRead => true;
		public override bool CanSeek => false;
		public override bool CanWrite => false;
		public override long Length => throw new NotSupportedException();
		public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
		public override void Flush() { }
		public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
		public override void SetLength(long value) => throw new NotSupportedException();
		public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
		public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
		{
			await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
			return 0;
		}
	}
}
