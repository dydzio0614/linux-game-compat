namespace LinuxGameCompat.Services.SummaryGeneration;

public sealed record GenerateSummariesCommandOptions(int Limit, string? Slug, bool Force);

public static class GenerateSummariesCommand
{
	public static bool IsRequested(string[] args) => args.Length > 0 && args[0] == "generate-summaries";

	public static bool TryParse(string[] args, int defaultLimit, out GenerateSummariesCommandOptions? options, out string? error)
	{
		options = null; error = null;
		if (!IsRequested(args)) { error = "Expected generate-summaries command."; return false; }
		int limit = defaultLimit; string? slug = null; bool force = false;
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
		if (limit is < 1 or > 10) { error = "--limit must be between 1 and 10."; return false; }
		options = new GenerateSummariesCommandOptions(limit, slug, force); return true;
	}
}
