using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LinuxGameCompat.Data;
using SharpToken;

namespace LinuxGameCompat.Services.SummaryGeneration;

public interface IGenerationTokenCounter
{
	int Count(string text);
}

public sealed class OpenAiTokenCounter : IGenerationTokenCounter
{
	private readonly GptEncoding _encoding = GptEncoding.GetEncoding("o200k_base");

	public int Count(string text) => _encoding.Encode(text).Count;
}

public sealed class EvidencePromptBuilder(IGenerationTokenCounter tokenCounter)
{
	public PromptSelection Build(IEnumerable<GenerationEvidenceClaim> claims, int maximumClaims, int maximumInputTokens)
	{
		ArgumentNullException.ThrowIfNull(claims);
		GenerationEvidenceClaim[] allClaims = claims.ToArray();
		CanonicalEvidence canonicalEvidence = Canonicalize(allClaims);
		List<GenerationEvidenceClaim> selectedClaims = Enum.GetValues<EvidenceClaimType>()
			.SelectMany(type => allClaims.Where(claim => claim.ClaimType == type)
				.OrderByDescending(claim => claim.ObservedAt)
				.ThenBy(claim => claim.ClaimId)
				.Take(type switch
				{
					EvidenceClaimType.Status => 4,
					EvidenceClaimType.Caveat => 3,
					EvidenceClaimType.Workaround => 3,
					EvidenceClaimType.Note => 2,
					_ => 0
				}))
			.Take(maximumClaims)
			.ToList();

		while (true)
		{
			string prompt = FormatPrompt(selectedClaims);
			int tokenCount = tokenCounter.Count(prompt)
				+ tokenCounter.Count(CompatibilitySummaryPromptContract.Instructions)
				+ tokenCounter.Count(CompatibilitySummaryPromptContract.OutputSchemaJson)
				+ CompatibilitySummaryPromptContract.ProtocolTokenReserve;
			if (tokenCount <= maximumInputTokens)
			{
				if (selectedClaims.Count == 0) throw new PromptBudgetExceededException(maximumInputTokens);
				return new PromptSelection(canonicalEvidence, selectedClaims, prompt, tokenCount);
			}
			if (selectedClaims.Count == 0) throw new PromptBudgetExceededException(maximumInputTokens);
			selectedClaims.RemoveAt(selectedClaims.Count - 1);
		}
	}

	public static CanonicalEvidence Canonicalize(IEnumerable<GenerationEvidenceClaim> claims)
	{
		GenerationEvidenceClaim[] orderedClaims = claims.OrderBy(claim => claim.SourceType)
			.ThenBy(claim => claim.SourceName, StringComparer.Ordinal)
			.ThenBy(claim => claim.SourceGameId, StringComparer.Ordinal)
			.ThenBy(claim => claim.SourceUrl, StringComparer.Ordinal)
			.ThenBy(claim => claim.ClaimId)
			.ThenBy(claim => claim.ClaimType)
			.ThenBy(claim => claim.ClaimValue, StringComparer.Ordinal)
			.ThenBy(claim => claim.ClaimText, StringComparer.Ordinal)
			.ThenBy(claim => claim.ObservedAt)
			.ToArray();
		string serialized = JsonSerializer.Serialize(new { version = CanonicalEvidence.ContractVersion, claims = orderedClaims });
		string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(serialized)));
		return new CanonicalEvidence(hash, serialized);
	}

	private static string FormatPrompt(IEnumerable<GenerationEvidenceClaim> claims)
	{
		return JsonSerializer.Serialize(new
		{
			version = CanonicalEvidence.ContractVersion,
			task = "Summarize practical Linux/Proton compatibility. Return a normalized status and concise plain-text summary. Raw sources remain authoritative.",
			evidence = claims.Select(claim => new
			{
				claimType = claim.ClaimType.ToString(),
				sourceType = claim.SourceType.ToString(),
				sourceName = claim.SourceName,
				sourceGameId = claim.SourceGameId,
				sourceUrl = claim.SourceUrl,
				claimValue = claim.ClaimValue,
				claimText = claim.ClaimText,
				observedAt = claim.ObservedAt.ToUniversalTime().ToString("O")
			})
		});
	}
}

public sealed class PromptBudgetExceededException(int maximumTokens)
	: Exception($"The prompt cannot fit within {maximumTokens} input tokens.");
