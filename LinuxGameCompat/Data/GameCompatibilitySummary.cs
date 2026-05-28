namespace LinuxGameCompat.Data;

public sealed class GameCompatibilitySummary
{
	public int Id { get; set; }

	public int GameId { get; set; }

	public Game Game { get; set; } = null!;

	public SummaryState State { get; set; } = SummaryState.NotGenerated;

	public CompatibilityStatus SummaryStatus { get; set; } = CompatibilityStatus.Unknown;

	public string? SummaryText { get; set; }

	public string? Provider { get; set; }

	public string? Model { get; set; }

	public string? EvidenceVersion { get; set; }

	public string? EvidenceHash { get; set; }

	public DateTimeOffset? GeneratedAt { get; set; }

	public bool IsStale { get; set; }

	public string? ErrorCode { get; set; }

	public string? ErrorMessage { get; set; }
}
