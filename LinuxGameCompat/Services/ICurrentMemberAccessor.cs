namespace LinuxGameCompat.Services;

public sealed record CurrentMember(string Id, string? Email);

public interface ICurrentMemberAccessor
{
	CurrentMember? GetCurrentMember();
}
