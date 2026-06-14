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
		var loginMessage = ComposeLoginLinkMessage(sender, email, loginLink);

		using var message = new MailMessage(loginMessage.Sender, loginMessage.Recipient)
		{
			Subject = loginMessage.Subject,
			Body = loginMessage.Body,
			IsBodyHtml = loginMessage.IsBodyHtml
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

	internal static SmtpAuthEmailMessage ComposeLoginLinkMessage(string sender, string recipient, Uri loginLink)
	{
		return new SmtpAuthEmailMessage(
			sender,
			recipient,
			"Your LinuxGameCompat login link",
			$"Use this link to sign in: {loginLink}",
			IsBodyHtml: false);
	}
}

internal sealed record SmtpAuthEmailMessage(
	string Sender,
	string Recipient,
	string Subject,
	string Body,
	bool IsBodyHtml);
