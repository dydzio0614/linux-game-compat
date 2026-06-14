using LinuxGameCompat.Data;
using Microsoft.EntityFrameworkCore;

namespace LinuxGameCompat.Services;

public sealed class MemberFavoritesService(
	CompatibilityDbContext dbContext,
	ICurrentMemberAccessor currentMemberAccessor) : IMemberFavoritesService
{
	public async Task<MemberFavoriteState> GetFavoriteStateAsync(
		int gameId,
		CancellationToken cancellationToken = default)
	{
		var member = currentMemberAccessor.GetCurrentMember();
		var isVisibleGame = await dbContext.Games
			.AsNoTracking()
			.AnyAsync(game => game.Id == gameId && !game.IsHidden, cancellationToken);

		if (member is null)
		{
			return new MemberFavoriteState(false, isVisibleGame, false);
		}

		var isFavorite = isVisibleGame && await dbContext.MemberFavorites
			.AsNoTracking()
			.AnyAsync(
				favorite => favorite.MemberId == member.Id && favorite.GameId == gameId,
				cancellationToken);

		return new MemberFavoriteState(true, isVisibleGame, isFavorite);
	}

	public async Task<MemberFavoriteMutationResult> AddCurrentMemberFavoriteAsync(
		int gameId,
		CancellationToken cancellationToken = default)
	{
		var member = currentMemberAccessor.GetCurrentMember();
		if (member is null)
		{
			return MemberFavoriteMutationResult.Unauthenticated;
		}

		var isVisibleGame = await dbContext.Games
			.AsNoTracking()
			.AnyAsync(game => game.Id == gameId && !game.IsHidden, cancellationToken);
		if (!isVisibleGame)
		{
			return MemberFavoriteMutationResult.HiddenOrMissingGame;
		}

		await dbContext.Database.ExecuteSqlInterpolatedAsync(
			$"""
			INSERT INTO "MemberFavorites" ("MemberId", "GameId", "CreatedAt")
			VALUES ({member.Id}, {gameId}, {DateTimeOffset.UtcNow})
			ON CONFLICT ("MemberId", "GameId") DO NOTHING
			""",
			cancellationToken);

		return MemberFavoriteMutationResult.SucceededResult;
	}

	public async Task<MemberFavoriteMutationResult> RemoveCurrentMemberFavoriteAsync(
		int gameId,
		CancellationToken cancellationToken = default)
	{
		var member = currentMemberAccessor.GetCurrentMember();
		if (member is null)
		{
			return MemberFavoriteMutationResult.Unauthenticated;
		}

		var isVisibleGame = await dbContext.Games
			.AsNoTracking()
			.AnyAsync(game => game.Id == gameId && !game.IsHidden, cancellationToken);
		if (!isVisibleGame)
		{
			return MemberFavoriteMutationResult.HiddenOrMissingGame;
		}

		await dbContext.MemberFavorites
			.Where(favorite => favorite.MemberId == member.Id && favorite.GameId == gameId)
			.ExecuteDeleteAsync(cancellationToken);
		return MemberFavoriteMutationResult.SucceededResult;
	}

	public async Task<IReadOnlyList<GameListItem>> GetCurrentMemberFavoritesAsync(
		CancellationToken cancellationToken = default)
	{
		var member = currentMemberAccessor.GetCurrentMember();
		if (member is null)
		{
			return [];
		}

		return await dbContext.MemberFavorites
			.AsNoTracking()
			.Where(favorite => favorite.MemberId == member.Id && !favorite.Game.IsHidden)
			.OrderBy(favorite => favorite.Game.Title)
			.Select(favorite => new GameListItem(
				favorite.Game.Id,
				favorite.Game.Title,
				favorite.Game.SteamAppId,
				favorite.Game.Slug,
				favorite.Game.CompatibilityStatus))
			.ToListAsync(cancellationToken);
	}
}
