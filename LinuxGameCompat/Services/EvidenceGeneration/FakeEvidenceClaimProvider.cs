using LinuxGameCompat.Data;

namespace LinuxGameCompat.Services.EvidenceGeneration;

public sealed class FakeEvidenceClaimProvider : IEvidenceClaimProvider
{
	public Task<EvidenceClaimProviderResult> GenerateAsync(EvidenceClaimProviderRequest request, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		IReadOnlyList<GeneratedEvidenceClaim> claims = [new(EvidenceClaimType.Note, "Fixture", "Fixture-derived compatibility note.")];
		return Task.FromResult(new EvidenceClaimProviderResult(claims, 0, 0));
	}
}
