using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LinuxGameCompat.Data;

namespace LinuxGameCompat.Services.EvidenceGeneration;

public sealed record SourceReferenceInput(string SourceGameId, string CitationUrl);

public sealed record NormalizedSourceFacts(
	string SourceGameId,
	string NativeStatus,
	string ContractVersion,
	string ContentHash,
	string Json,
	string? ETag = null,
	DateTimeOffset? LastModifiedAt = null);

public interface IEvidenceSourceFactsProvider
{
	Task<NormalizedSourceFacts> FetchAsync(SourceSystemType sourceType, SourceReferenceInput source, CancellationToken cancellationToken);
}

public sealed class EvidenceSourceFactsProvider(ProtonDbSourceAdapter protonDb, AreWeAntiCheatYetSourceAdapter awa) : IEvidenceSourceFactsProvider
{
	public Task<NormalizedSourceFacts> FetchAsync(SourceSystemType sourceType, SourceReferenceInput source, CancellationToken cancellationToken) => sourceType switch
	{
		SourceSystemType.ProtonDb => protonDb.FetchAsync(source, cancellationToken),
		SourceSystemType.AreWeAntiCheatYet => awa.FetchAsync(source, cancellationToken),
		_ => throw new EvidenceSourceException("unsupported_source", "The source type is not supported for evidence generation.")
	};
}

public sealed class EvidenceSourceException(string code, string message, Exception? innerException = null)
	: Exception(message, innerException)
{
	public string Code { get; } = code;
}

internal static class SourceFactSerializer
{
	public static NormalizedSourceFacts Create<T>(string sourceGameId, string nativeStatus, string contractVersion, T facts)
	{
		string json = JsonSerializer.Serialize(facts, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
		string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
		return new NormalizedSourceFacts(sourceGameId, nativeStatus, contractVersion, hash, json);
	}
}
