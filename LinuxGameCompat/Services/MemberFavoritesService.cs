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

		var exists = await dbContext.MemberFavorites
			.AnyAsync(favorite => favorite.MemberId == member.Id && favorite.GameId == gameId, cancellationToken);
		if (exists)
		{
			return MemberFavoriteMutationResult.SucceededResult;
		}

		dbContext.MemberFavorites.Add(new MemberFavorite
		{
			MemberId = member.Id,
			GameId = gameId,
			CreatedAt = DateTimeOffset.UtcNow
		});

		try
		{
			await dbContext.SaveChangesAsync(cancellationToken);
			return MemberFavoriteMutationResult.SucceededResult;
		}
		catch (DbUpdateException)
		{
			return MemberFavoriteMutationResult.Failed;
		}
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

		var favorite = await dbContext.MemberFavorites
			.SingleOrDefaultAsync(
				favorite => favorite.MemberId == member.Id && favorite.GameId == gameId,
				cancellationToken);
		if (favorite is null)
		{
			return MemberFavoriteMutationResult.SucceededResult;
		}

		dbContext.MemberFavorites.Remove(favorite);
		await dbContext.SaveChangesAsync(cancellationToken);
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
