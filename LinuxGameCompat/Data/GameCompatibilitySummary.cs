namespace LinuxGameCompat.Data;

/// <summary>
/// Optional cached compatibility summary reserved for future generated output.
/// A missing row is valid and means no summary has been generated for the game.
/// </summary>
public sealed class GameCompatibilitySummary
{
	public int Id { get; set; }

	public int GameId { get; set; }

	public Game Game { get; set; } = null!;

	/// <summary>
	/// Lifecycle state of the summary generation result.
	/// </summary>
	public SummaryState State { get; set; } = SummaryState.NotGenerated;

	/// <summary>
	/// Status represented by the summary at generation time. This may lag behind
	/// <see cref="Game.CompatibilityStatus"/> when the summary is stale.
	/// </summary>
	public CompatibilityStatus SummaryStatus { get; set; } = CompatibilityStatus.Unknown;

	public string? SummaryText { get; set; }

	public string? Provider { get; set; }

	public string? Model { get; set; }

	/// <summary>
	/// Version label for the evidence set used to produce the summary.
	/// </summary>
	public string? EvidenceVersion { get; set; }

	/// <summary>
	/// Hash of the source evidence used to detect stale summaries after claims change.
	/// </summary>
	public string? EvidenceHash { get; set; }

	public DateTimeOffset? GeneratedAt { get; set; }

	public DateTimeOffset? LastAttemptedAt { get; set; }

	public int? InputTokenCount { get; set; }

	public int? OutputTokenCount { get; set; }

	/// <summary>
	/// Explicit stale marker so read paths do not need to recompute freshness.
	/// </summary>
	public bool IsStale { get; set; }

	public string? ErrorCode { get; set; }

	public string? ErrorMessage { get; set; }
}
