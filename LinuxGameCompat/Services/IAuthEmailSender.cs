namespace LinuxGameCompat.Services;

public interface IAuthEmailSender
{
	Task SendLoginLinkAsync(string email, Uri loginLink, CancellationToken cancellationToken = default);
}
