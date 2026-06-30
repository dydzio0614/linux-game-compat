using LinuxGameCompat.Services.EvidenceGeneration;

namespace LinuxGameCompat.Tests;

public sealed class RefreshCompatibilityTests
{
	[Fact]
	public void Parser_AcceptsSupportedOptions()
	{
		bool parsed = RefreshCompatibilityCommand.TryParse(
			["refresh-compatibility", "--limit", "3", "--slug", "baldurs-gate-3", "--force"], 10,
			out CompatibilityRefreshOptions? options, out string? error);
		Assert.True(parsed, error);
		Assert.Equal(3, options!.Limit);
		Assert.Equal("baldurs-gate-3", options.Slug);
		Assert.True(options.Force);
	}

	[Theory]
	[InlineData("invalid")]
	[InlineData("0")]
	[InlineData("11")]
	public void Parser_RejectsInvalidLimits(string limit) =>
		Assert.False(RefreshCompatibilityCommand.TryParse(["refresh-compatibility", "--limit", limit], 10, out _, out _));

	[Fact]
	public void Parser_RejectsRemovedAndUnknownCommands()
	{
		Assert.False(RefreshCompatibilityCommand.TryParse(["generate-summaries"], 10, out _, out _));
		Assert.False(RefreshCompatibilityCommand.TryParse(["refresh-compatibility", "--unknown"], 10, out _, out _));
	}

	[Fact]
	public void ResultFormattingAndExitCodesCoverSuccessNoWorkLockContentionAndFailure()
	{
		CompatibilityRefreshRunResult success = new(2, 2, 0, 1, 1, 1, TimeSpan.FromMilliseconds(125), 40, 12);
		CompatibilityRefreshRunResult noWork = new(0, 0, 0, 0, 0, 0, TimeSpan.Zero, 0, 0);
		CompatibilityRefreshRunResult contended = noWork with { LockContended = true };
		CompatibilityRefreshRunResult failure = new(1, 0, 1, 0, 1, 0, TimeSpan.FromMilliseconds(50), 10, 0);
		Assert.Equal(0, RefreshCompatibilityCommand.ExitCodeFor(success));
		Assert.Equal(0, RefreshCompatibilityCommand.ExitCodeFor(noWork));
		Assert.Equal(0, RefreshCompatibilityCommand.ExitCodeFor(contended));
		Assert.Equal(1, RefreshCompatibilityCommand.ExitCodeFor(failure));
		Assert.Contains("changed_claim_games=1 generated_summaries=1", RefreshCompatibilityCommand.FormatResult(success));
		Assert.Contains("lock_contended=false", RefreshCompatibilityCommand.FormatResult(success));
	}
}
