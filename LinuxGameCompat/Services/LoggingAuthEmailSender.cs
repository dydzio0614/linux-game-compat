using Microsoft.Extensions.Logging;

namespace LinuxGameCompat.Services;

public sealed class LoggingAuthEmailSender(ILogger<LoggingAuthEmailSender> logger) : IAuthEmailSender
{
	public Task SendLoginLinkAsync(string email, Uri loginLink, CancellationToken cancellationToken = default)
	{
		logger.LogInformation("Passwordless login link for {Email}: {LoginLink}", email, loginLink);
		return Task.CompletedTask;
	}
}
