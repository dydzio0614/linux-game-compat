using LinuxGameCompat.Services.SummaryGeneration;

namespace LinuxGameCompat.Tests;

public sealed class GenerateSummariesCommandTests
{
	[Fact]
	public void Parser_AcceptsSupportedOptions()
	{
		bool parsed = GenerateSummariesCommand.TryParse(
			["generate-summaries", "--limit", "3", "--slug", "baldurs-gate-3", "--force"], 10,
			out GenerateSummariesCommandOptions? options, out string? error);

		Assert.True(parsed, error);
		Assert.Equal(3, options!.Limit);
		Assert.Equal("baldurs-gate-3", options.Slug);
		Assert.True(options.Force);
	}

	[Theory]
	[InlineData("0")]
	[InlineData("11")]
	[InlineData("invalid")]
	public void Parser_RejectsInvalidLimits(string limit)
	{
		Assert.False(GenerateSummariesCommand.TryParse(["generate-summaries", "--limit", limit], 10, out _, out _));
	}

	[Fact]
	public void Configuration_RequiresBoundedGenerationContract()
	{
		GenerationOptions options = new() { Concurrency = 2, MaximumRetries = 3, Model = "other" };

		Assert.Equal(3, options.Validate().Count);
	}
}
