using LinuxGameCompat.Data;
using LinuxGameCompat.Services.EvidenceGeneration;
using System.Text.Json;

namespace LinuxGameCompat.Tests;

public sealed class EvidenceClaimGenerationContractTests
{
	[Fact]
	public void Structured_output_schema_is_valid_json_with_an_object_root()
	{
		using JsonDocument schema = JsonDocument.Parse(EvidenceClaimPromptContract.OutputSchemaJson);
		Assert.Equal("object", schema.RootElement.GetProperty("type").GetString());
	}

	[Fact]
	public void Prompt_counts_exact_facts_and_rejects_over_budget_input()
	{
		NormalizedSourceFacts facts = new("1", "Gold", "source-v1", "HASH", "{\"status\":\"Gold\"}");
		EvidenceClaimPromptBuilder builder = new(new LengthTokenCounter());

		EvidenceClaimPrompt prompt = builder.Build(facts, 10_000);

		Assert.Equal(facts.Json, prompt.FactsJson);
		Assert.Throws<EvidenceClaimProviderException>(() => builder.Build(facts, 1));
	}

	[Fact]
	public void Output_validator_accepts_only_bounded_unique_non_status_claims()
	{
		IReadOnlyList<GeneratedEvidenceClaim> claims = EvidenceClaimOutputValidator.Parse("""
			{"claims":[{"claimType":"Caveat","claimValue":"Anti-cheat","claimText":"Multiplayer is unavailable."}]}
			""", 8);

		GeneratedEvidenceClaim claim = Assert.Single(claims);
		Assert.Equal(EvidenceClaimType.Caveat, claim.ClaimType);
		Assert.Throws<EvidenceClaimProviderException>(() => EvidenceClaimOutputValidator.Parse(
			"{\"claims\":[{\"claimType\":\"Status\",\"claimValue\":\"Gold\",\"claimText\":\"status\"}]}", 8));
		Assert.Throws<EvidenceClaimProviderException>(() => EvidenceClaimOutputValidator.Parse(
			"{\"claims\":[{\"claimType\":\"Note\",\"claimValue\":\"x\",\"claimText\":\"y\",\"extra\":true}]}", 8));
		Assert.Throws<EvidenceClaimProviderException>(() => EvidenceClaimOutputValidator.Parse(
			"{\"claims\":[{\"claimType\":\"Note\",\"claimValue\":\"x\",\"claimText\":\"y\"},{\"claimType\":\"Note\",\"claimValue\":\"X\",\"claimText\":\"Y\"}]}", 8));
	}

	[Fact]
	public async Task Materializer_prepends_deterministic_status_and_never_delegates_status_wording()
	{
		CapturingProvider provider = new([new(EvidenceClaimType.Note, "Reports", "Source reports are available.")]);
		EvidenceClaimMaterializer materializer = new(provider, new EvidenceClaimPromptBuilder(new ZeroTokenCounter()), EvidenceSourceAdapterTests.ValidOptions());
		NormalizedSourceFacts facts = new("1", "Platinum", "source-v1", "HASH", "{\"status\":\"Platinum\"}");

		MaterializedEvidenceClaims result = await materializer.GenerateAsync("ProtonDB", facts, CancellationToken.None);

		Assert.Collection(result.Claims,
			claim => { Assert.Equal(EvidenceClaimType.Status, claim.ClaimType); Assert.Equal("Platinum", claim.ClaimValue); Assert.Equal("ProtonDB reports the native compatibility status as Platinum.", claim.ClaimText); },
			claim => Assert.Equal(EvidenceClaimType.Note, claim.ClaimType));
		Assert.Equal(facts.Json, provider.LastRequest!.FactsJson);
	}

	private sealed class LengthTokenCounter : IEvidenceClaimTokenCounter { public int Count(string text) => text.Length; }
	private sealed class ZeroTokenCounter : IEvidenceClaimTokenCounter { public int Count(string text) => 0; }
	private sealed class CapturingProvider(IReadOnlyList<GeneratedEvidenceClaim> claims) : IEvidenceClaimProvider
	{
		public EvidenceClaimProviderRequest? LastRequest { get; private set; }
		public Task<EvidenceClaimProviderResult> GenerateAsync(EvidenceClaimProviderRequest request, CancellationToken cancellationToken)
		{
			LastRequest = request;
			return Task.FromResult(new EvidenceClaimProviderResult(claims, 4, 2));
		}
	}
}
