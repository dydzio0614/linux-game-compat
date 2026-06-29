namespace LinuxGameCompat.Data;

/// <summary>
/// Canonical link between one game and one external source identity.
/// Evidence claims cite this row instead of duplicating source URLs.
/// </summary>
public sealed class SourceReference
{
	public int Id { get; set; }

	public int GameId { get; set; }

	public Game Game { get; set; } = null!;

	public int SourceSystemId { get; set; }

	public SourceSystem SourceSystem { get; set; } = null!;

	/// <summary>
	/// Source-native game identifier, such as a Steam app ID for ProtonDB or an
	/// Are We Anti-Cheat Yet page slug. It is unique per game and source system.
	/// </summary>
	public required string SourceGameId { get; set; }

	/// <summary>
	/// Canonical citation URL for this game in the source system.
	/// </summary>
	public required string Url { get; set; }

	/// <summary>
	/// Optional source-specific facts that are useful to preserve but not stable
	/// enough to model as first-class columns yet.
	/// </summary>
	public string? MetadataJson { get; set; }

	public DateTimeOffset CreatedAt { get; set; }

	/// <summary>
	/// Evidence claims supported by this source reference.
	/// </summary>
	public ICollection<EvidenceClaim> EvidenceClaims { get; } = [];

	/// <summary>
	/// Optional lifecycle state for generated evidence imports.
	/// </summary>
	public SourceReferenceImportState? ImportState { get; set; }
}
