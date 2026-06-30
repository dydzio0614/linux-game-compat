namespace LinuxGameCompat.Services.EvidenceGeneration;

public sealed class EvidenceGenerationOptions
{
	public const string SectionName = "EvidenceGeneration";

	public string Provider { get; set; } = string.Empty;
	public string Model { get; set; } = string.Empty;
	public int MaximumGames { get; set; }
	public int MaximumGeneratedClaimsPerSource { get; set; }
	public int MaximumInputTokens { get; set; }
	public int MaximumOutputTokens { get; set; }
	public int FetchTimeoutSeconds { get; set; }
	public int MaximumResponseBytes { get; set; }
	public int ProviderTimeoutSeconds { get; set; }
	public int MaximumProviderRetries { get; set; }
	public int Concurrency { get; set; }

	public IReadOnlyList<string> Validate()
	{
		List<string> errors = [];
		if (!string.Equals(Provider, "OpenAI", StringComparison.Ordinal)) errors.Add("Provider must be OpenAI.");
		if (!string.Equals(Model, "gpt-5.4-mini", StringComparison.Ordinal)) errors.Add("Model must be gpt-5.4-mini for evidence contract v1.");
		if (MaximumGames is < 1 or > 10) errors.Add("MaximumGames must be between 1 and 10.");
		if (MaximumGeneratedClaimsPerSource is < 1 or > 8) errors.Add("MaximumGeneratedClaimsPerSource must be between 1 and 8.");
		if (MaximumInputTokens != 2500) errors.Add("MaximumInputTokens must be 2500 for evidence contract v1.");
		if (MaximumOutputTokens != 800) errors.Add("MaximumOutputTokens must be 800 for evidence contract v1.");
		if (FetchTimeoutSeconds != 15) errors.Add("FetchTimeoutSeconds must be 15 for evidence contract v1.");
		if (MaximumResponseBytes != 8 * 1024 * 1024) errors.Add("MaximumResponseBytes must be 8388608 for evidence contract v1.");
		if (ProviderTimeoutSeconds != 30) errors.Add("ProviderTimeoutSeconds must be 30 for evidence contract v1.");
		if (MaximumProviderRetries is < 0 or > 2) errors.Add("MaximumProviderRetries must be between 0 and 2.");
		if (Concurrency != 1) errors.Add("Concurrency must be 1 for the MVP.");
		return errors;
	}
}
