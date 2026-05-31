namespace LinuxGameCompat.Data;

/// <summary>
/// Describes an external evidence provider such as ProtonDB or Are We Anti-Cheat Yet.
/// </summary>
public sealed class SourceSystem
{
	public int Id { get; set; }

	public SourceSystemType Type { get; set; }

	public required string Name { get; set; }

	public required string BaseUrl { get; set; }

	public ICollection<SourceReference> SourceReferences { get; } = [];
}
