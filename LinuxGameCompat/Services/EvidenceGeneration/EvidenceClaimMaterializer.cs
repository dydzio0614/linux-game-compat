using LinuxGameCompat.Data;

namespace LinuxGameCompat.Services.EvidenceGeneration;

public sealed record MaterializedEvidenceClaims(
	IReadOnlyList<GeneratedEvidenceClaim> Claims,
	string ContractVersion,
	int InputTokens,
	int OutputTokens);

public sealed class EvidenceClaimMaterializer(
	IEvidenceClaimProvider provider,
	EvidenceClaimPromptBuilder promptBuilder,
	EvidenceGenerationOptions settings)
{
	public static string ContractVersion(NormalizedSourceFacts facts) => $"{facts.ContractVersion}+{EvidenceClaimPromptContract.ContractVersion}";

	public async Task<MaterializedEvidenceClaims> GenerateAsync(
		string sourceName,
		NormalizedSourceFacts facts,
		CancellationToken cancellationToken)
	{
		EvidenceClaimPrompt prompt = promptBuilder.Build(facts, settings.MaximumInputTokens);
		EvidenceClaimProviderResult result = await provider.GenerateAsync(
			new EvidenceClaimProviderRequest(settings.Model, prompt.FactsJson, settings.MaximumOutputTokens), cancellationToken);
		List<GeneratedEvidenceClaim> claims =
		[
			new GeneratedEvidenceClaim(EvidenceClaimType.Status, facts.NativeStatus,
				$"{sourceName} reports the native compatibility status as {facts.NativeStatus}.")
		];
		claims.AddRange(result.Claims);
		return new MaterializedEvidenceClaims(claims, ContractVersion(facts), result.InputTokens, result.OutputTokens);
	}
}
