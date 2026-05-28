namespace LinuxGameCompat.Data;

public sealed class EvidenceClaim
{
	public int Id { get; set; }

	public int GameId { get; set; }

	public Game Game { get; set; } = null!;

	public int SourceSystemId { get; set; }

	public SourceSystem SourceSystem { get; set; } = null!;

	public int SourceReferenceId { get; set; }

	public SourceReference SourceReference { get; set; } = null!;

	public EvidenceClaimType ClaimType { get; set; }

	public required string ClaimValue { get; set; }

	public required string ClaimText { get; set; }

	public DateTimeOffset ObservedAt { get; set; }
}
