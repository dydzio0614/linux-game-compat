using LinuxGameCompat.Data;
using Microsoft.EntityFrameworkCore;

namespace LinuxGameCompat.Services;

public sealed class GameCompatibilityReadService(CompatibilityDbContext dbContext) : IGameCompatibilityReadService
{
	private const int MaxVisibleGamesLimit = 100;

	public async Task<IReadOnlyList<GameListItem>> GetVisibleGamesAsync(
		int limit = 20,
		int offset = 0,
		CancellationToken cancellationToken = default)
	{
		if (limit <= 0)
		{
			return [];
		}

		var boundedLimit = Math.Min(limit, MaxVisibleGamesLimit);
		var boundedOffset = Math.Max(offset, 0);

		return await dbContext.Games
			.AsNoTracking()
			.Where(game => !game.IsHidden)
			.OrderBy(game => game.Title)
			.Skip(boundedOffset)
			.Take(boundedLimit)
			.Select(game => MapGameListItem(game))
			.ToListAsync(cancellationToken);
	}

	public async Task<IReadOnlyList<GameListItem>> SearchVisibleGamesByTitleAsync(
		string query,
		int limit = 20,
		CancellationToken cancellationToken = default)
	{
		var normalizedQuery = query.Trim();
		if (normalizedQuery.Length == 0 || limit <= 0)
		{
			return [];
		}

		return await dbContext.Games
			.AsNoTracking()
			.Where(game => !game.IsHidden && EF.Functions.ILike(game.Title, $"%{normalizedQuery}%"))
			.OrderBy(game => game.Title)
			.Take(limit)
			.Select(game => MapGameListItem(game))
			.ToListAsync(cancellationToken);
	}

	public async Task<GameDetail?> GetVisibleGameBySlugAsync(string slug, CancellationToken cancellationToken = default)
	{
		var normalizedSlug = slug.Trim();
		if (normalizedSlug.Length == 0)
		{
			return null;
		}

		var game = await dbContext.Games
			.AsNoTracking()
			.Include(game => game.SourceReferences)
				.ThenInclude(reference => reference.SourceSystem)
			.Include(game => game.SourceReferences)
				.ThenInclude(reference => reference.EvidenceClaims)
			.Include(game => game.CompatibilitySummary)
			.AsSplitQuery()
			.SingleOrDefaultAsync(game => game.Slug == normalizedSlug && !game.IsHidden, cancellationToken);

		return game is null ? null : MapGameDetail(game);
	}

	private static GameDetail MapGameDetail(Game game)
	{
		var references = game.SourceReferences
			.OrderBy(reference => reference.SourceSystem.Name)
			.ThenBy(reference => reference.SourceGameId)
			.Select(MapSourceReference)
			.ToArray();

		var claims = game.SourceReferences
			.SelectMany(reference => reference.EvidenceClaims.Select(claim => (Claim: claim, SourceReference: reference)))
			.OrderBy(item => item.Claim.ClaimType)
			.ThenBy(item => item.Claim.Id)
			.Select(item => new EvidenceClaimDetail(
				item.Claim.Id,
				item.Claim.ClaimType,
				item.Claim.ClaimValue,
				item.Claim.ClaimText,
				item.Claim.ObservedAt,
				MapSourceReference(item.SourceReference)))
			.ToArray();

		return new GameDetail(
			game.Id,
			game.Title,
			game.SteamAppId,
			game.Slug,
			game.CompatibilityStatus,
			references,
			claims,
			MapSummary(game.CompatibilitySummary));
	}

	private static GameListItem MapGameListItem(Game game)
	{
		return new GameListItem(
			game.Id,
			game.Title,
			game.SteamAppId,
			game.Slug,
			game.CompatibilityStatus);
	}

	private static SourceReferenceDetail MapSourceReference(SourceReference reference)
	{
		return new SourceReferenceDetail(
			reference.Id,
			reference.SourceSystem.Type,
			reference.SourceSystem.Name,
			reference.SourceGameId,
			reference.Url,
			reference.MetadataJson);
	}

	private static GameCompatibilitySummaryDetail? MapSummary(GameCompatibilitySummary? summary)
	{
		if (summary is null)
		{
			return null;
		}

		return new GameCompatibilitySummaryDetail(
			summary.State,
			summary.SummaryStatus,
			summary.SummaryText,
			summary.Provider,
			summary.Model,
			summary.EvidenceVersion,
			summary.EvidenceHash,
			summary.GeneratedAt,
			summary.IsStale,
			summary.ErrorCode,
			summary.ErrorMessage);
	}
}
