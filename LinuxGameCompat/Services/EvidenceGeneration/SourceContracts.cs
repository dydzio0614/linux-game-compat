using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LinuxGameCompat.Services.EvidenceGeneration;

public sealed record SourceReferenceInput(string SourceGameId, string CitationUrl);

public sealed record NormalizedSourceFacts(
	string SourceGameId,
	string NativeStatus,
	string ContractVersion,
	string ContentHash,
	string Json);

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
