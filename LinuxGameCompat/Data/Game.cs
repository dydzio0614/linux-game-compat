namespace LinuxGameCompat.Data;

/// <summary>
/// Canonical catalog entry for a game, independent of any particular evidence source.
/// Games may exist before evidence is collected, and non-Steam games are represented
/// with a null <see cref="SteamAppId"/>.
/// </summary>
public sealed class Game
{
	public int Id { get; set; }

	public required string Title { get; set; }

	/// <summary>
	/// Optional Steam application ID. This is nullable because the catalog is intended
	/// to include non-Steam games and manually curated records.
	/// </summary>
	public int? SteamAppId { get; set; }

	/// <summary>
	/// Stable application slug used for internal lookup and future public URLs.
	/// This is not assumed to match any external source slug.
	/// </summary>
	public required string Slug { get; set; }

	/// <summary>
	/// Source-neutral, normalized status chosen by the application. Supporting
	/// status evidence is stored separately in <see cref="EvidenceClaims"/>.
	/// </summary>
	public CompatibilityStatus CompatibilityStatus { get; set; } = CompatibilityStatus.Unknown;

	/// <summary>
	/// Manual suppression flag. Hidden games remain stored for audit and curation,
	/// but normal read paths should exclude them by default.
	/// </summary>
	public bool IsHidden { get; set; }

	public DateTimeOffset CreatedAt { get; set; }

	public DateTimeOffset UpdatedAt { get; set; }

	/// <summary>
	/// External identities and canonical citation URLs for this game.
	/// </summary>
	public ICollection<SourceReference> SourceReferences { get; } = [];

	/// <summary>
	/// Optional cached summary slot reserved for later GPT-generated output.
	/// </summary>
	public GameCompatibilitySummary? CompatibilitySummary { get; set; }
}
