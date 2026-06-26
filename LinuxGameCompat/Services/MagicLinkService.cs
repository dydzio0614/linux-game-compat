using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using LinuxGameCompat.Data;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace LinuxGameCompat.Services;

public sealed class MagicLinkService(
	CompatibilityDbContext dbContext,
	UserManager<ApplicationUser> userManager,
	SignInManager<ApplicationUser> signInManager,
	ILookupNormalizer normalizer,
	IAuthEmailSender emailSender,
	TimeProvider timeProvider,
	ILogger<MagicLinkService> logger) : IMagicLinkService
{
	private static readonly TimeSpan LinkLifetime = TimeSpan.FromMinutes(15);
	private static readonly EmailAddressAttribute EmailAddressValidator = new();
	private const int MaxEmailLength = 256;
	private const int MaxIpAddressLength = 64;
	private const int MaxUserAgentLength = 512;

	public async Task<MagicLinkRequestResult> RequestLoginLinkAsync(
		MagicLinkRequestInput input,
		CancellationToken cancellationToken = default)
	{
		var email = NormalizeEmailAddress(input.Email);
		if (email is null)
		{
			return new MagicLinkRequestResult(Accepted: false);
		}

		var normalizedEmail = normalizer.NormalizeEmail(email);
		if (string.IsNullOrWhiteSpace(normalizedEmail) || normalizedEmail.Length > MaxEmailLength)
		{
			return new MagicLinkRequestResult(Accepted: false);
		}

		var token = GenerateToken();
		var now = timeProvider.GetUtcNow();
		var request = new MagicLinkRequest
		{
			NormalizedEmail = normalizedEmail,
			TokenHash = HashToken(token),
			CreatedAt = now,
			ExpiresAt = now.Add(LinkLifetime),
			ReturnUrl = LocalReturnUrlNormalizer.Normalize(input.ReturnUrl),
			RequestIpAddress = Truncate(input.RequestIpAddress, MaxIpAddressLength),
			UserAgent = Truncate(input.UserAgent, MaxUserAgentLength)
		};

		dbContext.MagicLinkRequests.Add(request);
		await dbContext.SaveChangesAsync(cancellationToken);

		var link = BuildLoginLink(input.PublicBaseUri, token);
		try
		{
			await emailSender.SendLoginLinkAsync(email, link, cancellationToken);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception exception)
		{
			if (input.IncludeGeneratedLoginLink)
			{
				logger.LogWarning(
					"Failed to send magic-link email to {NormalizedEmail}. The saved request remains available for configured frontend display. ExceptionType: {ExceptionType}",
					normalizedEmail,
					exception.GetType().Name);
				return new MagicLinkRequestResult(Accepted: true, LoginLink: link);
			}

			dbContext.MagicLinkRequests.Remove(request);
			await dbContext.SaveChangesAsync(cancellationToken);
			logger.LogWarning(
				"Failed to send magic-link email to {NormalizedEmail}. The saved request was removed. ExceptionType: {ExceptionType}",
				normalizedEmail,
				exception.GetType().Name);
			return new MagicLinkRequestResult(Accepted: false);
		}

		return new MagicLinkRequestResult(
			Accepted: true,
			LoginLink: input.IncludeGeneratedLoginLink ? link : null);
	}

	public async Task<MagicLinkConsumeResult> ConsumeLoginLinkAsync(
		string token,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(token))
		{
			return Failed();
		}

		var tokenHash = HashToken(token);
		var now = timeProvider.GetUtcNow();
		var request = await dbContext.MagicLinkRequests
			.SingleOrDefaultAsync(linkRequest => linkRequest.TokenHash == tokenHash, cancellationToken);

		if (request is null || request.ConsumedAt is not null || request.ExpiresAt <= now)
		{
			return Failed();
		}

		var consumedRows = await dbContext.MagicLinkRequests
			.Where(linkRequest =>
				linkRequest.Id == request.Id &&
				linkRequest.ConsumedAt == null &&
				linkRequest.ExpiresAt > now)
			.ExecuteUpdateAsync(
				setters => setters.SetProperty(linkRequest => linkRequest.ConsumedAt, now),
				cancellationToken);

		if (consumedRows != 1)
		{
			return Failed();
		}

		var user = await userManager.FindByEmailAsync(request.NormalizedEmail);
		if (user is null)
		{
			user = new ApplicationUser
			{
				UserName = request.NormalizedEmail,
				Email = request.NormalizedEmail,
				EmailConfirmed = true
			};

			var createResult = await userManager.CreateAsync(user);
			if (!createResult.Succeeded)
			{
				return Failed();
			}
		}

		await signInManager.SignInAsync(user, isPersistent: true);

		return new MagicLinkConsumeResult(Succeeded: true, RedirectUrl: LocalReturnUrlNormalizer.Normalize(request.ReturnUrl));
	}

	private static MagicLinkConsumeResult Failed()
	{
		return new MagicLinkConsumeResult(Succeeded: false, RedirectUrl: "/login?failed=1");
	}

	private static string GenerateToken()
	{
		return WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
	}

	private static string HashToken(string token)
	{
		var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
		return Convert.ToHexString(bytes);
	}

	private static Uri BuildLoginLink(Uri publicBaseUri, string token)
	{
		var baseUri = new Uri(publicBaseUri, "/auth/magic-link/consume");
		var builder = new UriBuilder(baseUri);
		var query = QueryHelpers.ParseQuery(builder.Query);
		query["token"] = token;
		builder.Query = new QueryBuilder(query.SelectMany(
			pair => pair.Value.Select(value => new KeyValuePair<string, string>(pair.Key, value ?? string.Empty)))).ToQueryString().Value;
		return builder.Uri;
	}

	private static string? NormalizeEmailAddress(string? email)
	{
		if (string.IsNullOrWhiteSpace(email))
		{
			return null;
		}

		var trimmed = email.Trim();
		if (trimmed.Length > MaxEmailLength || !EmailAddressValidator.IsValid(trimmed))
		{
			return null;
		}

		return trimmed;
	}

	private static string? Truncate(string? value, int maxLength)
	{
		return value is { Length: > 0 } ? value[..Math.Min(value.Length, maxLength)] : value;
	}
}
