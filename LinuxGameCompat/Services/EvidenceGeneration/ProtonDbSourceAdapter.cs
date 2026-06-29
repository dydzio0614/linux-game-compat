using System.Globalization;
using System.Text.Json;

namespace LinuxGameCompat.Services.EvidenceGeneration;

public sealed class ProtonDbSourceAdapter(ISourceFetchTransport transport)
{
	public const string ContractVersion = "protondb-summary-v1";
	private const string CitationHost = "www.protondb.com";
	private static readonly IReadOnlyDictionary<string, string> Tiers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
	{
		["native"] = "Native",
		["platinum"] = "Platinum",
		["gold"] = "Gold",
		["silver"] = "Silver",
		["bronze"] = "Bronze",
		["borked"] = "Borked"
	};

	public async Task<NormalizedSourceFacts> FetchAsync(SourceReferenceInput source, CancellationToken cancellationToken = default)
	{
		ValidateSource(source);
		Uri fetchUri = new($"https://{CitationHost}/api/v1/reports/summaries/{source.SourceGameId}.json");
		using SourceFetchResult response = await transport.FetchAsync(
			new SourceFetchRequest(fetchUri, candidate => IsExactFetchUri(candidate, source.SourceGameId)), cancellationToken);
		return Normalize(source.SourceGameId, response.Document.RootElement);
	}

	public static void ValidateSource(SourceReferenceInput source)
	{
		ArgumentNullException.ThrowIfNull(source);
		if (source.SourceGameId.Length == 0 || source.SourceGameId.Any(character => character is < '0' or > '9'))
			throw new EvidenceSourceException("invalid_source_id", "ProtonDB source game IDs must contain only ASCII digits.");
		if (!Uri.TryCreate(source.CitationUrl, UriKind.Absolute, out Uri? citation) || !IsSafeHttpsUri(citation) ||
			!string.Equals(citation.IdnHost, CitationHost, StringComparison.OrdinalIgnoreCase) ||
			(citation.AbsolutePath != $"/app/{source.SourceGameId}" && citation.AbsolutePath != $"/app/{source.SourceGameId}/") ||
			citation.Query.Length != 0 || citation.Fragment.Length != 0)
			throw new EvidenceSourceException("invalid_citation_url", "The ProtonDB citation URL does not match the source game ID.");
	}

	internal static NormalizedSourceFacts Normalize(string sourceGameId, JsonElement root)
	{
		if (root.ValueKind != JsonValueKind.Object) throw new EvidenceSourceException("invalid_payload", "The ProtonDB summary must be a JSON object.");
		string? tier = OptionalString(root, "tier");
		string? effectiveTier = NormalizeTier(tier);
		if (string.Equals(tier, "pending", StringComparison.OrdinalIgnoreCase))
			effectiveTier = NormalizeTier(OptionalString(root, "provisionalTier"));
		if (effectiveTier is null) throw new EvidenceSourceException("unknown_status", "The ProtonDB summary has no recognized effective tier.");

		string? trendingTier = NormalizeOptionalTier(root, "trendingTier");
		string? bestReportedTier = NormalizeOptionalTier(root, "bestReportedTier");
		string? confidence = OptionalString(root, "confidence");
		if (confidence?.Length > 120) throw new EvidenceSourceException("field_too_long", "ProtonDB confidence exceeds 120 characters.");
		decimal? score = OptionalDecimal(root, "score");
		int? totalReports = OptionalInt32(root, "total");

		var facts = new
		{
			status = effectiveTier,
			trendingTier,
			bestReportedTier,
			confidence,
			score,
			totalReports
		};
		return SourceFactSerializer.Create(sourceGameId, effectiveTier, ContractVersion, facts);
	}

	private static string? NormalizeOptionalTier(JsonElement root, string propertyName)
	{
		string? value = OptionalString(root, propertyName);
		if (value is null || string.Equals(value, "pending", StringComparison.OrdinalIgnoreCase)) return null;
		return NormalizeTier(value) ?? throw new EvidenceSourceException("unknown_status", $"ProtonDB {propertyName} contains an unknown tier.");
	}

	private static string? NormalizeTier(string? value) => value is not null && Tiers.TryGetValue(value.Trim(), out string? tier) ? tier : null;

	private static string? OptionalString(JsonElement root, string propertyName)
	{
		if (!root.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind == JsonValueKind.Null) return null;
		if (property.ValueKind != JsonValueKind.String) throw new EvidenceSourceException("invalid_payload", $"ProtonDB {propertyName} must be a string.");
		return property.GetString()?.Trim();
	}

	private static decimal? OptionalDecimal(JsonElement root, string propertyName)
	{
		if (!root.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind == JsonValueKind.Null) return null;
		if (property.ValueKind != JsonValueKind.Number || !property.TryGetDecimal(out decimal value))
			throw new EvidenceSourceException("invalid_payload", $"ProtonDB {propertyName} must be a decimal number.");
		return value;
	}

	private static int? OptionalInt32(JsonElement root, string propertyName)
	{
		if (!root.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind == JsonValueKind.Null) return null;
		if (property.ValueKind != JsonValueKind.Number || !property.TryGetInt32(out int value) || value < 0)
			throw new EvidenceSourceException("invalid_payload", $"ProtonDB {propertyName} must be a non-negative integer.");
		return value;
	}

	private static bool IsExactFetchUri(Uri uri, string sourceGameId) => IsSafeHttpsUri(uri) &&
		string.Equals(uri.IdnHost, CitationHost, StringComparison.OrdinalIgnoreCase) &&
		uri.AbsolutePath == $"/api/v1/reports/summaries/{sourceGameId}.json" && uri.Query.Length == 0 && uri.Fragment.Length == 0;

	internal static bool IsSafeHttpsUri(Uri uri) => uri.IsAbsoluteUri && uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort &&
		string.IsNullOrEmpty(uri.UserInfo);
}
