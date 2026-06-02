using System.Net;
using System.Net.Mail;

namespace LinuxGameCompat.Services;

public sealed class SmtpAuthEmailSender(IConfiguration configuration) : IAuthEmailSender
{
	public async Task SendLoginLinkAsync(string email, Uri loginLink, CancellationToken cancellationToken = default)
	{
		var section = configuration.GetSection("Auth:Smtp");
		var host = section["Host"];
		var sender = section["SenderAddress"];

		if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(sender))
		{
			throw new InvalidOperationException("SMTP auth email is not configured. Set Auth:Smtp:Host and Auth:Smtp:SenderAddress.");
		}

		var port = section.GetValue("Port", 587);
		var enableTls = section.GetValue("EnableTls", true);
		var username = section["Username"];
		var password = section["Password"];

		using var message = new MailMessage(sender, email)
		{
			Subject = "Your LinuxGameCompat login link",
			Body = $"Use this link to sign in: {loginLink}",
			IsBodyHtml = false
		};

		using var client = new SmtpClient(host, port)
		{
			EnableSsl = enableTls
		};

		if (!string.IsNullOrWhiteSpace(username))
		{
			client.Credentials = new NetworkCredential(username, password);
		}

		await client.SendMailAsync(message, cancellationToken);
	}
}
