using LinuxGameCompat.Data;

namespace LinuxGameCompat.Services;

public sealed record GameListItem(
	int Id,
	string Title,
	int? SteamAppId,
	string Slug,
	CompatibilityStatus CompatibilityStatus);

public sealed record GameDetail(
	int Id,
	string Title,
	int? SteamAppId,
	string Slug,
	CompatibilityStatus CompatibilityStatus,
	IReadOnlyList<SourceReferenceDetail> SourceReferences,
	IReadOnlyList<EvidenceClaimDetail> EvidenceClaims,
	GameCompatibilitySummaryDetail? Summary);

public sealed record SourceReferenceDetail(
	int Id,
	SourceSystemType SourceType,
	string SourceName,
	string SourceGameId,
	string Url,
	string? MetadataJson);

public sealed record EvidenceClaimDetail(
	int Id,
	EvidenceClaimType ClaimType,
	string ClaimValue,
	string ClaimText,
	DateTimeOffset ObservedAt,
	SourceReferenceDetail SourceReference);

public sealed record GameCompatibilitySummaryDetail(
	SummaryState State,
	CompatibilityStatus SummaryStatus,
	string? SummaryText,
	DateTimeOffset? GeneratedAt,
	bool IsStale,
	bool HasStatusDisagreement,
	bool IsAiStatusFallback);
