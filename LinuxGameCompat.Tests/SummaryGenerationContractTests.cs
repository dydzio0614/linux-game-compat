using LinuxGameCompat.Data;
using LinuxGameCompat.Services.SummaryGeneration;

namespace LinuxGameCompat.Tests;

public sealed class SummaryGenerationContractTests
{
	public static TheoryData<SourceSystemType, string, CompatibilityStatus?> NativeStatuses => new()
	{
		{ SourceSystemType.ProtonDb, "Platinum", CompatibilityStatus.Playable },
		{ SourceSystemType.ProtonDb, "Native", CompatibilityStatus.Playable },
		{ SourceSystemType.ProtonDb, "Gold", CompatibilityStatus.Playable },
		{ SourceSystemType.ProtonDb, "T1", CompatibilityStatus.Playable },
		{ SourceSystemType.ProtonDb, "S", CompatibilityStatus.Playable },
		{ SourceSystemType.ProtonDb, "A", CompatibilityStatus.Playable },
		{ SourceSystemType.ProtonDb, "Playable", CompatibilityStatus.Playable },
		{ SourceSystemType.ProtonDb, "Verified", CompatibilityStatus.Playable },
		{ SourceSystemType.ProtonDb, "Bronze", CompatibilityStatus.PlayableWithCaveats },
		{ SourceSystemType.ProtonDb, "Silver", CompatibilityStatus.PlayableWithCaveats },
		{ SourceSystemType.ProtonDb, "T2", CompatibilityStatus.PlayableWithCaveats },
		{ SourceSystemType.ProtonDb, "T3", CompatibilityStatus.PlayableWithCaveats },
		{ SourceSystemType.ProtonDb, "T4", CompatibilityStatus.PlayableWithCaveats },
		{ SourceSystemType.ProtonDb, "B", CompatibilityStatus.PlayableWithCaveats },
		{ SourceSystemType.ProtonDb, "C", CompatibilityStatus.PlayableWithCaveats },
		{ SourceSystemType.ProtonDb, "D", CompatibilityStatus.PlayableWithCaveats },
		{ SourceSystemType.ProtonDb, "Borked", CompatibilityStatus.Unsupported },
		{ SourceSystemType.ProtonDb, "T5", CompatibilityStatus.Unsupported },
		{ SourceSystemType.ProtonDb, "F", CompatibilityStatus.Unsupported },
		{ SourceSystemType.ProtonDb, "Unsupported", CompatibilityStatus.Unsupported },
		{ SourceSystemType.AreWeAntiCheatYet, "Supported", CompatibilityStatus.Playable },
		{ SourceSystemType.AreWeAntiCheatYet, "Running", CompatibilityStatus.PlayableWithCaveats },
		{ SourceSystemType.AreWeAntiCheatYet, "Broken", CompatibilityStatus.Unsupported },
		{ SourceSystemType.AreWeAntiCheatYet, "Denied", CompatibilityStatus.Unsupported },
		{ SourceSystemType.AreWeAntiCheatYet, "Planned", CompatibilityStatus.Unsupported },
		{ SourceSystemType.ProtonDb, "pending", null },
		{ SourceSystemType.ProtonDb, "unknown", null },
		{ SourceSystemType.ProtonDb, "?", null },
		{ SourceSystemType.AreWeAntiCheatYet, "unsupported", null },
		{ SourceSystemType.Manual, "Playable", null }
	};

	[Theory]
	[MemberData(nameof(NativeStatuses))]
	public void Native_statuses_map_exactly(SourceSystemType source, string value, CompatibilityStatus? expected)
	{
		Assert.Equal(expected, NativeStatusNormalizer.Parse(source, $" {value.ToLowerInvariant()} "));
	}

	[Fact]
	public void Reduction_is_pessimistic_and_order_independent()
	{
		NativeStatusEvidence[] values =
		{
			new NativeStatusEvidence(SourceSystemType.ProtonDb, "Gold"),
			new NativeStatusEvidence(SourceSystemType.AreWeAntiCheatYet, "Running"),
			new NativeStatusEvidence(SourceSystemType.AreWeAntiCheatYet, "Denied")
		};
		Assert.Equal(CompatibilityStatus.Unsupported, NativeStatusNormalizer.Reduce(values));
		Assert.Equal(CompatibilityStatus.Unsupported, NativeStatusNormalizer.Reduce(values.Reverse()));
		Assert.Null(NativeStatusNormalizer.Reduce([new(SourceSystemType.ProtonDb, "unknown")]));
	}

	[Fact]
	public void Canonical_hash_is_order_independent_and_covers_every_field()
	{
		GenerationEvidenceClaim first = Claim(1, EvidenceClaimType.Status, "Gold", "Playable", "https://one");
		GenerationEvidenceClaim second = Claim(2, EvidenceClaimType.Caveat, "video", "Video issue", "https://two");
		EvidencePromptBuilder builder = new(new OpenAiTokenCounter());
		CanonicalEvidence baseline = builder.Build([first, second], 12, 2500).Evidence;
		Assert.Equal(baseline.Hash, builder.Build([second, first], 12, 2500).Evidence.Hash);
		Assert.Matches("^[0-9A-F]{64}$", baseline.Hash);
		Assert.NotEqual(baseline.Hash, builder.Build([first with { SourceUrl = "https://changed" }, second], 12, 2500).Evidence.Hash);
		Assert.NotEqual(baseline.Hash, builder.Build([first with { ClaimText = "changed" }, second], 12, 2500).Evidence.Hash);
		Assert.NotEqual(baseline.Hash, builder.Build([first with { ObservedAt = first.ObservedAt.AddSeconds(1) }, second], 12, 2500).Evidence.Hash);
	}

	[Fact]
	public void Prompt_selection_applies_mix_and_budget_without_changing_full_hash()
	{
		GenerationEvidenceClaim[] claims = Enumerable.Range(1, 20).Select(i => Claim(i, (EvidenceClaimType)(i % 4), $"v{i}", new string('x', 50), $"https://{i}" )).ToArray();
		EvidencePromptBuilder builder = new(new OpenAiTokenCounter());
		string completeHash = builder.Build(claims, 12, 2500).Evidence.Hash;
		PromptSelection result = builder.Build(claims, 12, 400);
		Assert.Equal(completeHash, result.Evidence.Hash);
		Assert.True(result.Claims.Count <= 12);
		Assert.True(result.InputTokens <= 400);
		Assert.True(result.Claims.Count(c => c.ClaimType == EvidenceClaimType.Status) <= 4);
		Assert.True(result.Claims.Count(c => c.ClaimType == EvidenceClaimType.Caveat) <= 3);
		Assert.True(result.Claims.Count(c => c.ClaimType == EvidenceClaimType.Workaround) <= 3);
		Assert.True(result.Claims.Count(c => c.ClaimType == EvidenceClaimType.Note) <= 2);
	}

	[Theory]
	[InlineData("{\"status\":\"Unknown\",\"summary\":\"text\"}")]
	[InlineData("{\"status\":\"Playable\",\"summary\":\" \"}")]
	[InlineData("{\"status\":\"not-a-status\",\"summary\":\"text\"}")]
	[InlineData("{\"status\":\"Playable\",\"summary\":\"text\",\"extra\":true}")]
	public void Structured_output_rejects_invalid_payloads(string json)
	{
		CompatibilitySummaryProviderException exception = Assert.Throws<CompatibilitySummaryProviderException>(() => ProviderOutputValidator.Parse(json));
		Assert.Equal(ProviderFailureKind.Permanent, exception.Kind);
	}

	[Fact]
	public void Structured_output_rejects_oversized_text()
	{
		string json = System.Text.Json.JsonSerializer.Serialize(new { status = "Playable", summary = new string('x', 4001) });
		Assert.Throws<CompatibilitySummaryProviderException>(() => ProviderOutputValidator.Parse(json));
	}

	[Fact]
	public void Failure_classifier_distinguishes_transient_permanent_and_cancelled()
	{
		Assert.Equal(ProviderFailureKind.Transient, ProviderFailureClassifier.Classify(new HttpRequestException()));
		Assert.Equal(ProviderFailureKind.Permanent, ProviderFailureClassifier.Classify(new ArgumentException()));
		using CancellationTokenSource source = new(); source.Cancel();
		Assert.Equal(ProviderFailureKind.Cancelled, ProviderFailureClassifier.Classify(new OperationCanceledException(), source.Token));
		Assert.Equal(ProviderFailureKind.Transient, ProviderFailureClassifier.Classify(new OperationCanceledException()));
	}

	[Fact]
	public void Configuration_defaults_are_bounded_and_model_is_locked()
	{
		GenerationOptions options = new();
		Assert.Empty(options.Validate());
		options.Model = "gpt-5-mini";
		Assert.NotEmpty(options.Validate());
	}

	private static GenerationEvidenceClaim Claim(int id, EvidenceClaimType type, string value, string text, string url) =>
		new(id, type, value, text, new DateTimeOffset(2026, 1, 1, 0, 0, id % 60, TimeSpan.Zero),
			SourceSystemType.ProtonDb, "ProtonDB", "game", url);
}
