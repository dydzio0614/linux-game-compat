using LinuxGameCompat.Data;

namespace LinuxGameCompat.Services.SummaryGeneration;

public sealed record GenerationEvidenceClaim(int ClaimId, EvidenceClaimType ClaimType, string ClaimValue, string ClaimText,
	DateTimeOffset ObservedAt, SourceSystemType SourceType, string SourceName, string SourceGameId, string SourceUrl);
public sealed record CanonicalEvidence(string Hash, string Serialized)
{
	public const string ContractVersion = "compatibility-summary-v2";
}
public sealed record PromptSelection(CanonicalEvidence Evidence, IReadOnlyList<GenerationEvidenceClaim> Claims, string Prompt, int InputTokens);
