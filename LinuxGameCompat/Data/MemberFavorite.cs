namespace LinuxGameCompat.Data;

public sealed class MemberFavorite
{
	public long Id { get; set; }

	public required string MemberId { get; set; }

	public ApplicationUser Member { get; set; } = null!;

	public int GameId { get; set; }

	public Game Game { get; set; } = null!;

	public DateTimeOffset CreatedAt { get; set; }
}
