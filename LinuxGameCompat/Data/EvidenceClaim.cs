namespace LinuxGameCompat.Data;

/// <summary>
/// A source-backed compatibility assertion for a game. Claims justify or explain
/// the normalized fields on <see cref="Game"/> without replacing them.
/// </summary>
public sealed class EvidenceClaim
{
	public int Id { get; set; }

	public int GameId { get; set; }

	public Game Game { get; set; } = null!;

	public int SourceSystemId { get; set; }

	public SourceSystem SourceSystem { get; set; } = null!;

	/// <summary>
	/// Required citation target. The linked source reference carries the canonical
	/// URL and source-native game identifier.
	/// </summary>
	public int SourceReferenceId { get; set; }

	public SourceReference SourceReference { get; set; } = null!;

	/// <summary>
	/// Broad purpose of the claim: status, caveat, workaround, or note.
	/// </summary>
	public EvidenceClaimType ClaimType { get; set; }

	/// <summary>
	/// Source-native value or compact normalized value for the claim.
	/// For status claims, this can differ from <see cref="Game.CompatibilityStatus"/>
	/// when a source uses its own labels.
	/// </summary>
	public required string ClaimValue { get; set; }

	/// <summary>
	/// Human-readable assertion derived from the source.
	/// </summary>
	public required string ClaimText { get; set; }

	/// <summary>
	/// Timestamp representing when this evidence was curated or observed.
	/// </summary>
	public DateTimeOffset ObservedAt { get; set; }
}
