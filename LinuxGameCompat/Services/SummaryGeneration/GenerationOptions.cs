namespace LinuxGameCompat.Services.SummaryGeneration;

public sealed class GenerationOptions
{
	public const string SectionName = "SummaryGeneration";
	public const int MinimumInputTokens = 256;
	public string Provider { get; set; } = string.Empty;
	public string Model { get; set; } = string.Empty;
	public int MaximumGames { get; set; }
	public int MaximumClaims { get; set; }
	public int MaximumInputTokens { get; set; }
	public int MaximumOutputTokens { get; set; }
	public int Concurrency { get; set; }
	public int RequestTimeoutSeconds { get; set; }
	public int MaximumRetries { get; set; }

	public IReadOnlyList<string> Validate()
	{
		List<string> errors = [];
		if (!string.Equals(Provider, "OpenAI", StringComparison.Ordinal)) errors.Add("Provider must be OpenAI.");
		if (!string.Equals(Model, "gpt-5.4-mini", StringComparison.Ordinal)) errors.Add("Model must be gpt-5.4-mini for generator contract v1.");
		if (MaximumGames < 1) errors.Add("MaximumGames must be positive.");
		if (MaximumClaims < 1) errors.Add("MaximumClaims must be positive.");
		if (MaximumInputTokens < MinimumInputTokens) errors.Add($"MaximumInputTokens must be at least {MinimumInputTokens}.");
		if (MaximumOutputTokens < 1) errors.Add("MaximumOutputTokens must be positive.");
		if (Concurrency != 1) errors.Add("Concurrency must be 1 for the MVP.");
		if (RequestTimeoutSeconds <= 0) errors.Add("RequestTimeoutSeconds must be positive.");
		if (MaximumRetries is < 0 or > 2) errors.Add("MaximumRetries must be between 0 and 2.");
		return errors;
	}
}
