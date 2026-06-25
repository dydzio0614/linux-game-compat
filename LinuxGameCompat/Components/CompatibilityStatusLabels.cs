using LinuxGameCompat.Data;

namespace LinuxGameCompat.Components;

public static class CompatibilityStatusLabels
{
	public static string ToPublicLabel(CompatibilityStatus status)
	{
		return status switch
		{
			CompatibilityStatus.Unknown => "Unknown",
			CompatibilityStatus.Unsupported => "Unsupported",
			CompatibilityStatus.PlayableWithCaveats => "Playable with caveats",
			CompatibilityStatus.Playable => "Playable",
			_ => status.ToString()
		};
	}

	public static string ToCssClass(CompatibilityStatus status)
	{
		return status switch
		{
			CompatibilityStatus.Playable => "status-label--playable",
			CompatibilityStatus.PlayableWithCaveats => "status-label--caveats",
			CompatibilityStatus.Unsupported => "status-label--unsupported",
			CompatibilityStatus.Unknown => "status-label--unknown",
			_ => "status-label--unknown"
		};
	}
}
