namespace LinuxGameCompat.Data;

public sealed class Game
{
	public int Id { get; set; }

	public required string Title { get; set; }

	public int? SteamAppId { get; set; }

	public required string Slug { get; set; }

	public CompatibilityStatus CompatibilityStatus { get; set; } = CompatibilityStatus.Unknown;

	public bool IsHidden { get; set; }

	public DateTimeOffset CreatedAt { get; set; }

	public DateTimeOffset UpdatedAt { get; set; }

	public ICollection<SourceReference> SourceReferences { get; } = [];

	public ICollection<EvidenceClaim> EvidenceClaims { get; } = [];

	public GameCompatibilitySummary? CompatibilitySummary { get; set; }
}
