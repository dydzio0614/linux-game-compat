namespace LinuxGameCompat.Data;

public sealed class SourceSystem
{
	public int Id { get; set; }

	public SourceSystemType Type { get; set; }

	public required string Name { get; set; }

	public required string BaseUrl { get; set; }

	public ICollection<SourceReference> SourceReferences { get; } = [];

	public ICollection<EvidenceClaim> EvidenceClaims { get; } = [];
}
