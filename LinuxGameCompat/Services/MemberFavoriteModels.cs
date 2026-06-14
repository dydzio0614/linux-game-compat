namespace LinuxGameCompat.Services;

public sealed record MemberFavoriteState(bool IsAuthenticated, bool IsVisibleGame, bool IsFavorite);

public sealed record MemberFavoriteMutationResult(MemberFavoriteMutationStatus Status)
{
	public bool Succeeded => Status is MemberFavoriteMutationStatus.Succeeded;

	public static MemberFavoriteMutationResult Unauthenticated { get; } =
		new(MemberFavoriteMutationStatus.Unauthenticated);

	public static MemberFavoriteMutationResult HiddenOrMissingGame { get; } =
		new(MemberFavoriteMutationStatus.HiddenOrMissingGame);

	public static MemberFavoriteMutationResult SucceededResult { get; } =
		new(MemberFavoriteMutationStatus.Succeeded);

	public static MemberFavoriteMutationResult Failed { get; } =
		new(MemberFavoriteMutationStatus.Failed);
}

public enum MemberFavoriteMutationStatus
{
	Unauthenticated,
	HiddenOrMissingGame,
	Succeeded,
	Failed
}
