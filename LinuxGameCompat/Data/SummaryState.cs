namespace LinuxGameCompat.Data;

/// <summary>
/// Lifecycle state for an optional generated compatibility summary.
/// </summary>
public enum SummaryState
{
	/// <summary>No summary has been generated yet.</summary>
	NotGenerated = 0,
	/// <summary>The summary matches the current evidence version/hash.</summary>
	Current = 1,
	/// <summary>The summary exists but no longer matches the current evidence.</summary>
	Stale = 2,
	/// <summary>The latest summary generation attempt failed.</summary>
	Failed = 3
}
