namespace LinuxGameCompat.Services.EvidenceGeneration;

public sealed record CompatibilityRefreshOptions(int Limit, string? Slug = null, bool Force = false);

public static class RefreshCompatibilityCommand
{
	public static bool IsRequested(string[] args) => args.Length > 0 && args[0] == "refresh-compatibility";

	public static bool TryParse(string[] args, int maximumLimit, out CompatibilityRefreshOptions? options, out string? error)
	{
		options = null;
		error = null;
		if (!IsRequested(args)) { error = "Expected refresh-compatibility command."; return false; }
		int limit = maximumLimit;
		string? slug = null;
		bool force = false;
		for (int index = 1; index < args.Length; index++)
		{
			switch (args[index])
			{
				case "--force": force = true; break;
				case "--limit" when index + 1 < args.Length && int.TryParse(args[++index], out int parsed): limit = parsed; break;
				case "--slug" when index + 1 < args.Length && !string.IsNullOrWhiteSpace(args[index + 1]): slug = args[++index]; break;
				default: error = $"Invalid argument: {args[index]}"; return false;
			}
		}
		if (limit < 1 || limit > maximumLimit) { error = $"Limit must be between 1 and {maximumLimit}."; return false; }
		options = new CompatibilityRefreshOptions(limit, slug, force);
		return true;
	}

	public static string FormatResult(CompatibilityRefreshRunResult result) =>
		$"selected={result.Selected} succeeded={result.Succeeded} failed={result.Failed} skipped={result.Skipped} changed_claim_games={result.ChangedClaimGames} generated_summaries={result.GeneratedSummaries} duration_ms={result.Duration.TotalMilliseconds:F0} input_tokens={result.InputTokens} output_tokens={result.OutputTokens} lock_contended={result.LockContended.ToString().ToLowerInvariant()}";

	public static int ExitCodeFor(CompatibilityRefreshRunResult result) => result.Failed > 0 ? 1 : 0;
}
