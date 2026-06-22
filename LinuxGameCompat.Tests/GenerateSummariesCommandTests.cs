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

	[Fact]
	public void ResultFormattingAndExitCodesCoverSuccessNoWorkLockContentionAndFailure()
	{
		SummaryGenerationRunResult success = new(2, 2, 0, 1, TimeSpan.FromMilliseconds(125), 40, 12);
		SummaryGenerationRunResult noWork = new(0, 0, 0, 0, TimeSpan.Zero, 0, 0);
		SummaryGenerationRunResult contended = noWork with { LockContended = true };
		SummaryGenerationRunResult failure = new(1, 0, 1, 0, TimeSpan.FromMilliseconds(50), 10, 0);

		Assert.Equal(0, GenerateSummariesCommand.ExitCodeFor(success));
		Assert.Equal(0, GenerateSummariesCommand.ExitCodeFor(noWork));
		Assert.Equal(0, GenerateSummariesCommand.ExitCodeFor(contended));
		Assert.Equal(1, GenerateSummariesCommand.ExitCodeFor(failure));
		Assert.Equal("selected=2 succeeded=2 failed=0 skipped=1 duration_ms=125 input_tokens=40 output_tokens=12",
			GenerateSummariesCommand.FormatResult(success));
	}
}
