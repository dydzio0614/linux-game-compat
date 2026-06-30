using System.Text.Json;
using LinuxGameCompat.Data;
using LinuxGameCompat.Services.EvidenceGeneration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace LinuxGameCompat.Tests;

public sealed class EvidenceSourceAdapterTests
{
	[Fact]
	public void Import_state_model_is_one_to_one_with_bounded_fields_and_cascade_delete()
	{
		DbContextOptions<CompatibilityDbContext> options = new DbContextOptionsBuilder<CompatibilityDbContext>()
			.UseNpgsql("Host=localhost;Database=model-only;Username=test;Password=test")
			.Options;
		using CompatibilityDbContext dbContext = new(options);
		IEntityType entity = dbContext.Model.FindEntityType(typeof(SourceReferenceImportState))!;
		IReadOnlyForeignKey foreignKey = Assert.Single(entity.GetForeignKeys());

		Assert.Equal(nameof(SourceReferenceImportState.SourceReferenceId), Assert.Single(entity.FindPrimaryKey()!.Properties).Name);
		Assert.True(foreignKey.IsUnique);
		Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
		Assert.Equal(128, entity.FindProperty(nameof(SourceReferenceImportState.ContentHash))!.GetMaxLength());
		Assert.Equal(80, entity.FindProperty(nameof(SourceReferenceImportState.ContractVersion))!.GetMaxLength());
		Assert.Equal(512, entity.FindProperty(nameof(SourceReferenceImportState.ETag))!.GetMaxLength());
		Assert.Equal(80, entity.FindProperty(nameof(SourceReferenceImportState.ErrorCode))!.GetMaxLength());
		Assert.Equal(2000, entity.FindProperty(nameof(SourceReferenceImportState.ErrorMessage))!.GetMaxLength());
	}

	[Theory]
	[InlineData("abc", "https://www.protondb.com/app/123")]
	[InlineData("123", "http://www.protondb.com/app/123")]
	[InlineData("123", "https://protondb.com/app/123")]
	[InlineData("123", "https://www.protondb.com:444/app/123")]
	[InlineData("123", "https://www.protondb.com/app/456")]
	[InlineData("123", "https://www.protondb.com/app/123?unsafe=true")]
	public void ProtonDb_rejects_invalid_source_identity(string sourceGameId, string citationUrl)
	{
		Assert.Throws<EvidenceSourceException>(() => ProtonDbSourceAdapter.ValidateSource(new SourceReferenceInput(sourceGameId, citationUrl)));
	}

	[Fact]
	public void ProtonDb_normalizes_authoritative_summary_fields()
	{
		using JsonDocument fixture = ReadFixture("protondb-summary.json");

		NormalizedSourceFacts result = ProtonDbSourceAdapter.Normalize("1086940", fixture.RootElement);

		using JsonDocument facts = JsonDocument.Parse(result.Json);
		Assert.Equal("Gold", result.NativeStatus);
		Assert.Equal("Gold", facts.RootElement.GetProperty("status").GetString());
		Assert.Equal("Silver", facts.RootElement.GetProperty("trendingTier").GetString());
		Assert.Equal("Platinum", facts.RootElement.GetProperty("bestReportedTier").GetString());
		Assert.Equal("strong", facts.RootElement.GetProperty("confidence").GetString());
		Assert.Equal(138, facts.RootElement.GetProperty("totalReports").GetInt32());
		Assert.Matches("^[0-9A-F]{64}$", result.ContentHash);
		Assert.Equal(result.ContentHash, ProtonDbSourceAdapter.Normalize("1086940", fixture.RootElement).ContentHash);
	}

	[Fact]
	public void ProtonDb_uses_recognized_provisional_tier_only_while_pending()
	{
		using JsonDocument fixture = ReadFixture("protondb-pending.json");
		NormalizedSourceFacts result = ProtonDbSourceAdapter.Normalize("1", fixture.RootElement);
		Assert.Equal("Bronze", result.NativeStatus);

		using JsonDocument invalid = JsonDocument.Parse("{\"tier\":\"pending\",\"provisionalTier\":\"mystery\"}");
		EvidenceSourceException exception = Assert.Throws<EvidenceSourceException>(() => ProtonDbSourceAdapter.Normalize("1", invalid.RootElement));
		Assert.Equal("unknown_status", exception.Code);
	}

	[Theory]
	[InlineData("https://areweanticheatyet.com/game/fixture-game/")]
	[InlineData("https://www.areweanticheatyet.com/game/fixture-game")]
	[InlineData("https://areweanticheatyet.com/game/other")]
	[InlineData("https://areweanticheatyet.com/game/fixture-game#fragment")]
	public void Awa_rejects_noncanonical_citation_urls(string citationUrl)
	{
		Assert.Throws<EvidenceSourceException>(() => AreWeAntiCheatYetSourceAdapter.ValidateSource(
			new SourceReferenceInput("fixture-game", citationUrl)));
	}

	[Fact]
	public void Awa_preserves_ordered_bounded_facts_and_stable_hash()
	{
		using JsonDocument fixture = ReadFixture("awa-games.json");
		JsonElement record = fixture.RootElement[0];

		NormalizedSourceFacts result = AreWeAntiCheatYetSourceAdapter.Normalize("fixture-game", record, new ZeroTokenCounter(), 2500);

		using JsonDocument facts = JsonDocument.Parse(result.Json);
		JsonElement root = facts.RootElement;
		Assert.Equal("Running", result.NativeStatus);
		Assert.Equal(new[] { "Alpha", "Bravo", "Charlie", "Delta", "Echo", "Foxtrot", "Golf", "Hotel" },
			root.GetProperty("antiCheats").EnumerateArray().Select(item => item.GetString()));
		Assert.Equal(new[] { "First note", "Second note", "Third note", "Fourth note" },
			root.GetProperty("notes").EnumerateArray().Select(item => item.GetProperty("text").GetString()));
		Assert.Equal(new[] { "update-3", "update-4", "update-5", "update-6", "update-7", "update-8", "update-9", "update-10" },
			root.GetProperty("updates").EnumerateArray().Select(item => item.GetProperty("name").GetString()));
		Assert.Equal(result.ContentHash, AreWeAntiCheatYetSourceAdapter.Normalize("fixture-game", record, new ZeroTokenCounter(), 2500).ContentHash);
	}

	[Fact]
	public void Awa_budget_reduction_removes_oldest_included_update_first()
	{
		using JsonDocument fixture = ReadFixture("awa-games.json");
		RejectTextTokenCounter counter = new("update-3");
		NormalizedSourceFacts result = AreWeAntiCheatYetSourceAdapter.Normalize(
			"fixture-game", fixture.RootElement[0], counter, 2500);

		using JsonDocument facts = JsonDocument.Parse(result.Json);
		string? firstUpdate = facts.RootElement.GetProperty("updates")[0].GetProperty("name").GetString();
		Assert.Equal("update-4", firstUpdate);
		Assert.True(new EvidenceClaimPromptBuilder(counter).Build(result, 2500).InputTokens <= 2500);
	}

	[Fact]
	public void Awa_rejects_unknown_status_and_oversized_fields()
	{
		using JsonDocument unknown = JsonDocument.Parse("""
			{"slug":"x","status":"Maybe","dateChanged":"today","anticheats":[],"notes":[],"updates":[]}
			""");
		Assert.Equal("unknown_status", Assert.Throws<EvidenceSourceException>(() =>
			AreWeAntiCheatYetSourceAdapter.Normalize("x", unknown.RootElement, new ZeroTokenCounter(), 2500)).Code);

		string oversizedJson = JsonSerializer.Serialize(new
		{
			slug = "x", status = "Supported", dateChanged = "today", anticheats = new[] { new string('x', 121) },
			notes = Array.Empty<string[]>(), updates = Array.Empty<object>()
		});
		using JsonDocument oversized = JsonDocument.Parse(oversizedJson);
		Assert.Equal("field_too_long", Assert.Throws<EvidenceSourceException>(() =>
			AreWeAntiCheatYetSourceAdapter.Normalize("x", oversized.RootElement, new ZeroTokenCounter(), 2500)).Code);
	}

	[Fact]
	public async Task Awa_fetches_global_dataset_once_per_adapter_instance()
	{
		string json = File.ReadAllText(FixturePath("awa-games.json"));
		CountingTransport transport = new(json);
		AreWeAntiCheatYetSourceAdapter adapter = new(transport, new ZeroTokenCounter(), ValidOptions());

		await adapter.FetchAsync(new SourceReferenceInput("fixture-game", "https://areweanticheatyet.com/game/fixture-game"));
		await adapter.FetchAsync(new SourceReferenceInput("second-game", "https://areweanticheatyet.com/game/second-game"));

		Assert.Equal(1, transport.CallCount);
	}

	private static JsonDocument ReadFixture(string name) => JsonDocument.Parse(File.ReadAllText(FixturePath(name)));
	private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "EvidenceGeneration", name);

	internal static EvidenceGenerationOptions ValidOptions() => new()
	{
		Provider = "OpenAI", Model = "gpt-5.4-mini", MaximumGames = 10, MaximumGeneratedClaimsPerSource = 8,
		MaximumInputTokens = 2500, MaximumOutputTokens = 800, FetchTimeoutSeconds = 15,
		MaximumResponseBytes = 8 * 1024 * 1024, ProviderTimeoutSeconds = 30, MaximumProviderRetries = 2, Concurrency = 1
	};

	private sealed class ZeroTokenCounter : IEvidenceClaimTokenCounter
	{
		public int Count(string text) => 0;
	}

	private sealed class RejectTextTokenCounter(string rejectedText) : IEvidenceClaimTokenCounter
	{
		public int Count(string text) => text.Contains(rejectedText, StringComparison.Ordinal) ? 2500 : 0;
	}

	private sealed class CountingTransport(string json) : ISourceFetchTransport
	{
		public int CallCount { get; private set; }

		public Task<SourceFetchResult> FetchAsync(SourceFetchRequest request, CancellationToken cancellationToken)
		{
			CallCount++;
			return Task.FromResult(new SourceFetchResult(JsonDocument.Parse(json), null, null));
		}
	}
}
