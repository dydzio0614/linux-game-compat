namespace LinuxGameCompat.Services;

public sealed record MagicLinkRequestInput(
	string Email,
	string? ReturnUrl,
	Uri PublicBaseUri,
	string? RequestIpAddress,
	string? UserAgent,
	bool IncludeGeneratedLoginLink = false);

public sealed record MagicLinkRequestResult(bool Accepted, Uri? LoginLink = null);

public sealed record MagicLinkConsumeResult(bool Succeeded, string RedirectUrl);

public interface IMagicLinkService
{
	Task<MagicLinkRequestResult> RequestLoginLinkAsync(
		MagicLinkRequestInput input,
		CancellationToken cancellationToken = default);

	Task<MagicLinkConsumeResult> ConsumeLoginLinkAsync(
		string token,
		CancellationToken cancellationToken = default);
}
