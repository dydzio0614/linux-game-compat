using LinuxGameCompat.Data;

namespace LinuxGameCompat.Services.SummaryGeneration;

/// <summary>Development-only provider used to exercise the finite command path without network access.</summary>
public sealed class FakeCompatibilitySummaryProvider : ICompatibilitySummaryProvider
{
	public Task<CompatibilitySummaryProviderResult> GenerateAsync(
		CompatibilitySummaryProviderRequest request,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return Task.FromResult(new CompatibilitySummaryProviderResult(
			CompatibilityStatus.PlayableWithCaveats,
			"Test-provider summary generated from the selected evidence.",
			0,
			0));
	}
}
