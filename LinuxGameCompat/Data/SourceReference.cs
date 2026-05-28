namespace LinuxGameCompat.Data;

public sealed class SourceReference
{
	public int Id { get; set; }

	public int GameId { get; set; }

	public Game Game { get; set; } = null!;

	public int SourceSystemId { get; set; }

	public SourceSystem SourceSystem { get; set; } = null!;

	public required string SourceGameId { get; set; }

	public required string Url { get; set; }

	public string? MetadataJson { get; set; }

	public DateTimeOffset CreatedAt { get; set; }

	public ICollection<EvidenceClaim> EvidenceClaims { get; } = [];
}
