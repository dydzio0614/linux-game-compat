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
	TimeProvider timeProvider) : IMagicLinkService
{
	private static readonly TimeSpan LinkLifetime = TimeSpan.FromMinutes(15);

	public async Task<MagicLinkRequestResult> RequestLoginLinkAsync(
		MagicLinkRequestInput input,
		CancellationToken cancellationToken = default)
	{
		var normalizedEmail = normalizer.NormalizeEmail(input.Email);
		if (string.IsNullOrWhiteSpace(normalizedEmail))
		{
			return new MagicLinkRequestResult(Accepted: true);
		}

		var token = GenerateToken();
		var request = new MagicLinkRequest
		{
			NormalizedEmail = normalizedEmail,
			TokenHash = HashToken(token),
			CreatedAt = timeProvider.GetUtcNow(),
			ExpiresAt = timeProvider.GetUtcNow().Add(LinkLifetime),
			ReturnUrl = NormalizeLocalReturnUrl(input.ReturnUrl),
			RequestIpAddress = input.RequestIpAddress,
			UserAgent = Truncate(input.UserAgent, 512)
		};

		dbContext.MagicLinkRequests.Add(request);
		await dbContext.SaveChangesAsync(cancellationToken);

		var link = BuildLoginLink(input.PublicBaseUri, token);
		await emailSender.SendLoginLinkAsync(input.Email, link, cancellationToken);

		return new MagicLinkRequestResult(Accepted: true);
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

		return new MagicLinkConsumeResult(Succeeded: true, RedirectUrl: NormalizeLocalReturnUrl(request.ReturnUrl));
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

	private static string NormalizeLocalReturnUrl(string? returnUrl)
	{
		if (string.IsNullOrWhiteSpace(returnUrl) || !Uri.TryCreate(returnUrl, UriKind.Relative, out var uri))
		{
			return "/";
		}

		var value = uri.ToString();
		if (!value.StartsWith("/", StringComparison.Ordinal) || value.StartsWith("//", StringComparison.Ordinal))
		{
			return "/";
		}

		return value;
	}

	private static string? Truncate(string? value, int maxLength)
	{
		return value is { Length: > 0 } ? value[..Math.Min(value.Length, maxLength)] : value;
	}
}
