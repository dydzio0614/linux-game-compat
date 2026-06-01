using System.Security.Claims;

namespace LinuxGameCompat.Services;

public sealed class CurrentMemberAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentMemberAccessor
{
	public CurrentMember? GetCurrentMember()
	{
		var user = httpContextAccessor.HttpContext?.User;
		if (user?.Identity?.IsAuthenticated != true)
		{
			return null;
		}

		var id = user.FindFirstValue(ClaimTypes.NameIdentifier);
		if (string.IsNullOrWhiteSpace(id))
		{
			return null;
		}

		var email = user.FindFirstValue(ClaimTypes.Email) ?? user.Identity.Name;
		return new CurrentMember(id, email);
	}
}
