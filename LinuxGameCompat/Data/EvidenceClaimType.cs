namespace LinuxGameCompat.Data;

/// <summary>
/// Classifies the kind of compatibility assertion represented by an evidence claim.
/// </summary>
public enum EvidenceClaimType
{
	/// <summary>Evidence supporting the game's normalized compatibility status.</summary>
	Status = 0,
	/// <summary>Known limitation or risk that affects playability.</summary>
	Caveat = 1,
	/// <summary>Action a player can take to improve compatibility.</summary>
	Workaround = 2,
	/// <summary>Additional compatibility note that is not a status, caveat, or workaround.</summary>
	Note = 3
}
