using System.Text.Json;
namespace LinuxGameCompat.Services.EvidenceGeneration;

public sealed class AreWeAntiCheatYetSourceAdapter(
	ISourceFetchTransport transport,
	IEvidenceClaimTokenCounter tokenCounter,
	EvidenceGenerationOptions options)
{
	public const string ContractVersion = "awa-games-v1";
	public const string DataUrl = "https://raw.githubusercontent.com/AreWeAntiCheatYet/AreWeAntiCheatYet/refs/heads/master/games.json";
	private const string CitationHost = "areweanticheatyet.com";
	private static readonly IReadOnlyDictionary<string, string> Statuses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
	{
		["Supported"] = "Supported",
		["Running"] = "Running",
		["Broken"] = "Broken",
		["Denied"] = "Denied",
		["Planned"] = "Planned"
	};
	private Task<IReadOnlyDictionary<string, JsonElement>>? _recordsTask;
	private string? _etag;
	private DateTimeOffset? _lastModifiedAt;

	public async Task<NormalizedSourceFacts> FetchAsync(SourceReferenceInput source, CancellationToken cancellationToken = default)
	{
		ValidateSource(source);
		IReadOnlyDictionary<string, JsonElement> records = await GetRecordsAsync(cancellationToken);
		if (!records.TryGetValue(source.SourceGameId, out JsonElement record))
			throw new EvidenceSourceException("not_found", "The AWA source record was not found.");
		return Normalize(source.SourceGameId, record, tokenCounter, options.MaximumInputTokens) with { ETag = _etag, LastModifiedAt = _lastModifiedAt };
	}

	public static void ValidateSource(SourceReferenceInput source)
	{
		ArgumentNullException.ThrowIfNull(source);
		if (string.IsNullOrWhiteSpace(source.SourceGameId) || source.SourceGameId.Length > 120 || source.SourceGameId.Contains('/'))
			throw new EvidenceSourceException("invalid_source_id", "The AWA source game ID is invalid.");
		if (!Uri.TryCreate(source.CitationUrl, UriKind.Absolute, out Uri? citation) || !ProtonDbSourceAdapter.IsSafeHttpsUri(citation) ||
			!string.Equals(citation.IdnHost, CitationHost, StringComparison.OrdinalIgnoreCase) ||
			citation.AbsolutePath != $"/game/{source.SourceGameId}" || citation.Query.Length != 0 || citation.Fragment.Length != 0)
			throw new EvidenceSourceException("invalid_citation_url", "The AWA citation URL does not match the source game ID.");
	}

	internal static NormalizedSourceFacts Normalize(string sourceGameId, JsonElement record, IEvidenceClaimTokenCounter tokenCounter, int maximumInputTokens)
	{
		if (record.ValueKind != JsonValueKind.Object) throw new EvidenceSourceException("invalid_payload", "The AWA game record must be a JSON object.");
		string statusText = RequiredString(record, "status");
		if (!Statuses.TryGetValue(statusText, out string? status)) throw new EvidenceSourceException("unknown_status", "The AWA record contains an unknown status.");
		string dateChanged = RequiredString(record, "dateChanged");
		ValidateLength(dateChanged, 120, "dateChanged");

		List<string> antiCheats = ReadAntiCheats(record);
		List<AwaNote> notes = ReadNotes(record);
		List<AwaUpdate> updates = ReadUpdates(record);

		while (true)
		{
			var facts = new { status, dateChanged, antiCheats, notes, updates };
			NormalizedSourceFacts normalized = SourceFactSerializer.Create(sourceGameId, status, ContractVersion, facts);
			if (EvidenceClaimPromptBuilder.CountInputTokens(normalized.Json, tokenCounter) <= maximumInputTokens) return normalized;
			if (updates.Count > 0) { updates.RemoveAt(0); continue; }
			if (notes.Count > 0) { notes.RemoveAt(notes.Count - 1); continue; }
			if (antiCheats.Count > 0) { antiCheats.RemoveAt(antiCheats.Count - 1); continue; }
			throw new EvidenceSourceException("prompt_budget_exceeded", "The status-only AWA fact contract exceeds the input-token budget.");
		}
	}

	private async Task<IReadOnlyDictionary<string, JsonElement>> GetRecordsAsync(CancellationToken cancellationToken)
	{
		Task<IReadOnlyDictionary<string, JsonElement>>? recordsTask = Volatile.Read(ref _recordsTask);
		if (recordsTask is null)
		{
			Task<IReadOnlyDictionary<string, JsonElement>> created = LoadRecordsAsync(cancellationToken);
			recordsTask = Interlocked.CompareExchange(ref _recordsTask, created, null) ?? created;
		}
		try { return await recordsTask; }
		catch { _ = Interlocked.CompareExchange(ref _recordsTask, null, recordsTask); throw; }
	}

	private async Task<IReadOnlyDictionary<string, JsonElement>> LoadRecordsAsync(CancellationToken cancellationToken)
	{
		Uri uri = new(DataUrl);
		using SourceFetchResult response = await transport.FetchAsync(
			new SourceFetchRequest(uri, candidate => candidate == uri, AllowPlainText: true), cancellationToken);
		_etag = response.ETag;
		_lastModifiedAt = response.LastModifiedAt;
		if (response.Document.RootElement.ValueKind != JsonValueKind.Array)
			throw new EvidenceSourceException("invalid_payload", "The AWA dataset must be a JSON array.");
		Dictionary<string, JsonElement> records = new(StringComparer.Ordinal);
		foreach (JsonElement record in response.Document.RootElement.EnumerateArray())
		{
			string slug = RequiredString(record, "slug");
			if (!records.TryAdd(slug, record.Clone())) throw new EvidenceSourceException("duplicate_source_id", "The AWA dataset contains duplicate slugs.");
		}
		return records;
	}

	private static List<string> ReadAntiCheats(JsonElement record)
	{
		if (!record.TryGetProperty("anticheats", out JsonElement values) || values.ValueKind != JsonValueKind.Array)
			throw new EvidenceSourceException("invalid_payload", "AWA anticheats must be an array.");
		List<string> result = [];
		foreach (JsonElement value in values.EnumerateArray())
		{
			if (value.ValueKind != JsonValueKind.String) throw new EvidenceSourceException("invalid_payload", "AWA anti-cheats must be strings.");
			string text = value.GetString()?.Trim() ?? string.Empty;
			if (text.Length == 0) continue;
			ValidateLength(text, 120, "anti-cheat");
			result.Add(text);
		}
		return result.Order(StringComparer.Ordinal).Take(8).ToList();
	}

	private static List<AwaNote> ReadNotes(JsonElement record)
	{
		if (!record.TryGetProperty("notes", out JsonElement values) || values.ValueKind != JsonValueKind.Array)
			throw new EvidenceSourceException("invalid_payload", "AWA notes must be an array.");
		List<AwaNote> result = [];
		foreach (JsonElement value in values.EnumerateArray())
		{
			if (value.ValueKind != JsonValueKind.Array) throw new EvidenceSourceException("invalid_payload", "Each AWA note must be an array.");
			JsonElement[] fields = value.EnumerateArray().ToArray();
			if (fields.Length < 1 || fields.Length > 2 || fields[0].ValueKind != JsonValueKind.String ||
				(fields.Length == 2 && fields[1].ValueKind is not JsonValueKind.String and not JsonValueKind.Null))
				throw new EvidenceSourceException("invalid_payload", "Each AWA note must contain text and an optional reference.");
			string text = fields[0].GetString()?.Trim() ?? string.Empty;
			if (text.Length == 0) continue;
			string reference = fields.Length == 2 ? fields[1].GetString()?.Trim() ?? string.Empty : string.Empty;
			ValidateLength(text, 1000, "note text");
			ValidateLength(reference, 1000, "note reference");
			if (result.Count < 4) result.Add(new AwaNote(text, reference));
		}
		return result;
	}

	private static List<AwaUpdate> ReadUpdates(JsonElement record)
	{
		if (!record.TryGetProperty("updates", out JsonElement values) || values.ValueKind != JsonValueKind.Array)
			throw new EvidenceSourceException("invalid_payload", "AWA updates must be an array.");
		List<AwaUpdate> all = [];
		foreach (JsonElement value in values.EnumerateArray())
		{
			string name = RequiredString(value, "name");
			string date = RequiredString(value, "date");
			string reference = RequiredString(value, "reference", allowBlank: true);
			ValidateLength(name, 500, "update name");
			ValidateLength(date, 120, "update date");
			ValidateLength(reference, 1000, "update reference");
			all.Add(new AwaUpdate(name, date, reference));
		}
		return all.Skip(Math.Max(0, all.Count - 8)).ToList();
	}

	private static string RequiredString(JsonElement element, string propertyName, bool allowBlank = false)
	{
		if (!element.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind != JsonValueKind.String)
			throw new EvidenceSourceException("invalid_payload", $"AWA {propertyName} must be a string.");
		string value = property.GetString()?.Trim() ?? string.Empty;
		if (!allowBlank && value.Length == 0) throw new EvidenceSourceException("invalid_payload", $"AWA {propertyName} must not be blank.");
		return value;
	}

	private static void ValidateLength(string value, int maximum, string field)
	{
		if (value.Length > maximum) throw new EvidenceSourceException("field_too_long", $"AWA {field} exceeds {maximum} characters.");
	}

	private sealed record AwaNote(string Text, string Reference);
	private sealed record AwaUpdate(string Name, string Date, string Reference);
}
