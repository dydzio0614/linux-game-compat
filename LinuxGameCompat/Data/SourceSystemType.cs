namespace LinuxGameCompat.Data;

/// <summary>
/// Known evidence-provider categories. Stored as strings by EF Core to keep
/// database values readable.
/// </summary>
public enum SourceSystemType
{
	ProtonDb = 0,
	AreWeAntiCheatYet = 1,
	Manual = 2
}
