namespace LinuxGameCompat.Data;

public sealed class MagicLinkRequest
{
	public long Id { get; set; }

	public string NormalizedEmail { get; set; } = string.Empty;

	public string TokenHash { get; set; } = string.Empty;

	public DateTimeOffset ExpiresAt { get; set; }

	public DateTimeOffset? ConsumedAt { get; set; }

	public string? ReturnUrl { get; set; }

	public DateTimeOffset CreatedAt { get; set; }

	public string? RequestIpAddress { get; set; }

	public string? UserAgent { get; set; }
}
