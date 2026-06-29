using SharpToken;

namespace LinuxGameCompat.Services.EvidenceGeneration;

public interface IEvidenceClaimTokenCounter
{
	int Count(string text);
}

public sealed class OpenAiEvidenceClaimTokenCounter : IEvidenceClaimTokenCounter
{
	private readonly GptEncoding _encoding = GptEncoding.GetEncoding("o200k_base");
	public int Count(string text) => _encoding.Encode(text).Count;
}

public sealed record EvidenceClaimPrompt(string FactsJson, int InputTokens);

public sealed class EvidenceClaimPromptBuilder(IEvidenceClaimTokenCounter tokenCounter)
{
	public EvidenceClaimPrompt Build(NormalizedSourceFacts facts, int maximumInputTokens)
	{
		ArgumentNullException.ThrowIfNull(facts);
		int inputTokens = CountInputTokens(facts.Json, tokenCounter);
		if (inputTokens > maximumInputTokens)
			throw new EvidenceClaimProviderException("prompt_budget_exceeded", $"The evidence prompt exceeds {maximumInputTokens} input tokens.");
		return new EvidenceClaimPrompt(facts.Json, inputTokens);
	}

	internal static int CountInputTokens(string factsJson, IEvidenceClaimTokenCounter counter) =>
		counter.Count(factsJson) + counter.Count(EvidenceClaimPromptContract.Instructions) +
		counter.Count(EvidenceClaimPromptContract.OutputSchemaJson) + EvidenceClaimPromptContract.ProtocolTokenReserve;
}
