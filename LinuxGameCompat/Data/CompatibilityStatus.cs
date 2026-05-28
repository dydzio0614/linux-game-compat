namespace LinuxGameCompat.Data;

/// <summary>
/// Source-neutral compatibility status used by the application. Public UI labels
/// can map these values to product copy later.
/// </summary>
public enum CompatibilityStatus
{
	/// <summary>No compatibility evidence has been curated yet.</summary>
	Unknown = 0,
	/// <summary>Known blocker prevents Linux or Proton play.</summary>
	Unsupported = 1,
	/// <summary>Playable, but with known caveats or required workarounds.</summary>
	PlayableWithCaveats = 2,
	/// <summary>Playable without known major caveats in the curated evidence.</summary>
	Playable = 3
}
