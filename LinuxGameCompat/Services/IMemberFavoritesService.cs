namespace LinuxGameCompat.Services;

public interface IMemberFavoritesService
{
	Task<MemberFavoriteState> GetFavoriteStateAsync(int gameId, CancellationToken cancellationToken = default);

	Task<MemberFavoriteMutationResult> AddCurrentMemberFavoriteAsync(
		int gameId,
		CancellationToken cancellationToken = default);

	Task<MemberFavoriteMutationResult> RemoveCurrentMemberFavoriteAsync(
		int gameId,
		CancellationToken cancellationToken = default);

	Task<IReadOnlyList<GameListItem>> GetCurrentMemberFavoritesAsync(CancellationToken cancellationToken = default);
}
