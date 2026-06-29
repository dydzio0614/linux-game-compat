namespace LinuxGameCompat.Data;

/// <summary>
/// Operational state for importing one external source reference.
/// </summary>
public sealed class SourceReferenceImportState
{
	public int SourceReferenceId { get; set; }

	public SourceReference SourceReference { get; set; } = null!;

	public string? ContentHash { get; set; }

	public string? ContractVersion { get; set; }

	public DateTimeOffset? LastAttemptedAt { get; set; }

	public DateTimeOffset? LastSucceededAt { get; set; }

	public string? ETag { get; set; }

	public DateTimeOffset? LastModifiedAt { get; set; }

	public string? ErrorCode { get; set; }

	public string? ErrorMessage { get; set; }
}
