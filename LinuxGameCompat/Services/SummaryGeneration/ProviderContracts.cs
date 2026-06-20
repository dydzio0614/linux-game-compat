using System.Text.Json;
using System.ClientModel;
using LinuxGameCompat.Data;

namespace LinuxGameCompat.Services.SummaryGeneration;

public interface ICompatibilitySummaryProvider
{
	Task<CompatibilitySummaryProviderResult> GenerateAsync(CompatibilitySummaryProviderRequest request, CancellationToken cancellationToken);
}

public sealed record CompatibilitySummaryProviderRequest(string Model, string Prompt, int MaximumOutputTokens);
public sealed record CompatibilitySummaryProviderResult(CompatibilityStatus Status, string Summary, int InputTokens, int OutputTokens);
public enum ProviderFailureKind { Transient, Permanent, Cancelled }

public static class CompatibilitySummaryPromptContract
{
	public const string Instructions = "Use only supplied evidence. Do not invent fixes, performance claims, or hardware-specific conclusions.";
	public const string OutputSchemaJson = """
	{
	  "type": "object",
	  "properties": {
	    "status": { "type": "string", "enum": ["Unsupported", "PlayableWithCaveats", "Playable"] },
	    "summary": { "type": "string", "minLength": 1, "maxLength": 4000 }
	  },
	  "required": ["status", "summary"],
	  "additionalProperties": false
	}
	""";

	// Covers Responses message/schema framing that is not represented by the literal strings.
	public const int ProtocolTokenReserve = 64;
}

public sealed class CompatibilitySummaryProviderException(ProviderFailureKind kind, string message, Exception? innerException = null) : Exception(message, innerException)
{
	public ProviderFailureKind Kind { get; } = kind;
}

public static class ProviderOutputValidator
{
	public const int MaximumSummaryLength = 4_000;
	public static (CompatibilityStatus Status, string Summary) Parse(string json)
	{
		try
		{
			using JsonDocument document = JsonDocument.Parse(json);
			JsonElement root = document.RootElement;
			if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 2) throw Malformed("Output must contain exactly status and summary.");
			string? statusText = root.GetProperty("status").GetString();
			string? summary = root.GetProperty("summary").GetString();
			if (!Enum.TryParse(statusText, false, out CompatibilityStatus status) || status == CompatibilityStatus.Unknown) throw Malformed("Output status is unknown.");
			if (string.IsNullOrWhiteSpace(summary)) throw Malformed("Output summary is blank.");
			if (summary.Length > MaximumSummaryLength) throw Malformed("Output summary exceeds 4000 characters.");
			return (status, summary.Trim());
		}
		catch (CompatibilitySummaryProviderException) { throw; }
		catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException)
		{
			throw Malformed("Output does not match the required schema.", exception);
		}
	}

	private static CompatibilitySummaryProviderException Malformed(string message, Exception? inner = null) => new(ProviderFailureKind.Permanent, message, inner);
}

public static class ProviderFailureClassifier
{
	public static ProviderFailureKind Classify(Exception exception, CancellationToken requestedCancellation = default)
	{
		if (exception is OperationCanceledException) return requestedCancellation.IsCancellationRequested ? ProviderFailureKind.Cancelled : ProviderFailureKind.Transient;
		if (exception is CompatibilitySummaryProviderException providerException) return providerException.Kind;
		if (exception is ClientResultException clientResultException)
			return ClassifyHttpStatus(clientResultException.Status);
		if (exception is TimeoutException or HttpRequestException) return ProviderFailureKind.Transient;
		return ProviderFailureKind.Permanent;
	}

	public static ProviderFailureKind ClassifyHttpStatus(int status) => status is 408 or 429 or >= 500
		? ProviderFailureKind.Transient
		: ProviderFailureKind.Permanent;
}
