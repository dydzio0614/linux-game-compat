namespace LinuxGameCompat.Services.SummaryGeneration;

public sealed class GenerationOptions
{
	public const string SectionName = "SummaryGeneration";
	public string Provider { get; set; } = "OpenAI";
	public string Model { get; set; } = "gpt-5.4-mini";
	public int MaximumGames { get; set; } = 10;
	public int MaximumClaims { get; set; } = 12;
	public int MaximumInputTokens { get; set; } = 2_500;
	public int MaximumOutputTokens { get; set; } = 500;
	public int Concurrency { get; set; } = 1;
	public int RequestTimeoutSeconds { get; set; } = 30;
	public int MaximumRetries { get; set; } = 2;

	public IReadOnlyList<string> Validate()
	{
		List<string> errors = [];
		if (!string.Equals(Provider, "OpenAI", StringComparison.Ordinal)) errors.Add("Provider must be OpenAI.");
		if (!string.Equals(Model, "gpt-5.4-mini", StringComparison.Ordinal)) errors.Add("Model must be gpt-5.4-mini for generator contract v1.");
		if (MaximumGames is < 1 or > 10) errors.Add("MaximumGames must be between 1 and 10.");
		if (MaximumClaims is < 1 or > 12) errors.Add("MaximumClaims must be between 1 and 12.");
		if (MaximumInputTokens <= 0) errors.Add("MaximumInputTokens must be positive.");
		if (MaximumOutputTokens is < 1 or > 500) errors.Add("MaximumOutputTokens must be between 1 and 500.");
		if (Concurrency != 1) errors.Add("Concurrency must be 1 for the MVP.");
		if (RequestTimeoutSeconds <= 0) errors.Add("RequestTimeoutSeconds must be positive.");
		if (MaximumRetries is < 0 or > 2) errors.Add("MaximumRetries must be between 0 and 2.");
		return errors;
	}
}
