using LinuxGameCompat.Data;
using LinuxGameCompat.Services.SummaryGeneration;
using Microsoft.EntityFrameworkCore;

namespace LinuxGameCompat.Services;

public sealed class GameCompatibilityReadService(CompatibilityDbContext dbContext) : IGameCompatibilityReadService
{
	private const int MaxVisibleGamesLimit = 100;
	private const string LikeEscapeCharacter = @"\";

	public Task<IReadOnlyList<GameListItem>> GetVisibleGamesAsync(CancellationToken cancellationToken)
	{
		return GetVisibleGamesAsync(limit: 20, offset: 0, cancellationToken);
	}

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

		var boundedLimit = Math.Min(limit, MaxVisibleGamesLimit);
		var escapedQuery = EscapeLikePattern(normalizedQuery);

		return await dbContext.Games
			.AsNoTracking()
			.Where(game => !game.IsHidden && EF.Functions.ILike(game.Title, $"%{escapedQuery}%", LikeEscapeCharacter))
			.OrderBy(game => game.Title)
			.Take(boundedLimit)
			.Select(game => MapGameListItem(game))
			.ToListAsync(cancellationToken);
	}

	private static string EscapeLikePattern(string value)
	{
		return value
			.Replace(@"\", @"\\", StringComparison.Ordinal)
			.Replace("%", @"\%", StringComparison.Ordinal)
			.Replace("_", @"\_", StringComparison.Ordinal);
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
			MapSummary(game.CompatibilitySummary, game.CompatibilityStatus, claims));
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

	private static GameCompatibilitySummaryDetail? MapSummary(
		GameCompatibilitySummary? summary,
		CompatibilityStatus publicStatus,
		IReadOnlyList<EvidenceClaimDetail> claims)
	{
		if (summary is null)
		{
			return null;
		}

		CompatibilityStatus? deterministicStatus = NativeStatusNormalizer.Reduce(claims
			.Where(claim => claim.ClaimType == EvidenceClaimType.Status)
			.Select(claim => new NativeStatusEvidence(claim.SourceReference.SourceType, claim.ClaimValue)));

		return new GameCompatibilitySummaryDetail(
			summary.State,
			summary.SummaryStatus,
			summary.SummaryText,
			summary.GeneratedAt,
			summary.IsStale,
			deterministicStatus is not null && deterministicStatus != summary.SummaryStatus,
			deterministicStatus is null && publicStatus == summary.SummaryStatus && summary.SummaryStatus != CompatibilityStatus.Unknown);
	}
}
