namespace LinuxGameCompat.Services;

public interface IGameCompatibilityReadService
{
	Task<IReadOnlyList<GameListItem>> GetVisibleGamesAsync(CancellationToken cancellationToken);

	Task<IReadOnlyList<GameListItem>> GetVisibleGamesAsync(
		int limit = 20,
		int offset = 0,
		CancellationToken cancellationToken = default);

	Task<IReadOnlyList<GameListItem>> SearchVisibleGamesByTitleAsync(
		string query,
		int limit = 20,
		CancellationToken cancellationToken = default);

	Task<GameDetail?> GetVisibleGameBySlugAsync(string slug, CancellationToken cancellationToken = default);
}
