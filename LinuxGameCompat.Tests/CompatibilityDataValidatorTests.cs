using LinuxGameCompat.Data;
using LinuxGameCompat.Services;

namespace LinuxGameCompat.Tests;

public sealed class CompatibilityDataValidatorTests
{
	[Fact]
	public void ValidateGame_AcceptsGameWithoutEvidenceClaims()
	{
		var game = new Game
		{
			Title = "No Evidence Yet",
			Slug = "no-evidence-yet",
			CompatibilityStatus = CompatibilityStatus.Unknown
		};

		var errors = CompatibilityDataValidator.ValidateGame(game);

		Assert.Empty(errors);
	}

	[Fact]
	public void ValidateGame_RejectsMissingTitleAndSlug()
	{
		var game = new Game
		{
			Title = " ",
			Slug = "",
			CompatibilityStatus = CompatibilityStatus.Unknown
		};

		var errors = CompatibilityDataValidator.ValidateGame(game);

		Assert.Contains("Game title is required.", errors);
		Assert.Contains("Game slug is required.", errors);
	}

	[Fact]
	public void ValidateEvidenceClaim_RejectsMissingSourceMetadata()
	{
		var claim = new EvidenceClaim
		{
			ClaimType = EvidenceClaimType.Status,
			ClaimValue = "playable",
			ClaimText = "Playable through Proton."
		};

		var errors = CompatibilityDataValidator.ValidateEvidenceClaim(claim);

		Assert.Contains("Evidence claim source reference metadata is required.", errors);
	}

	[Fact]
	public void ValidateEvidenceClaim_AcceptsClaimWithSourceMetadata()
	{
		var sourceSystem = new SourceSystem
		{
			Id = 1,
			Type = SourceSystemType.ProtonDb,
			Name = "ProtonDB",
			BaseUrl = "https://www.protondb.com"
		};
		var sourceReference = new SourceReference
		{
			Id = 1,
			SourceSystemId = sourceSystem.Id,
			SourceSystem = sourceSystem,
			SourceGameId = "1086940",
			Url = "https://www.protondb.com/app/1086940"
		};
		var claim = new EvidenceClaim
		{
			SourceReferenceId = sourceReference.Id,
			SourceReference = sourceReference,
			ClaimType = EvidenceClaimType.Status,
			ClaimValue = "playable",
			ClaimText = "Playable through Proton."
		};

		var errors = CompatibilityDataValidator.ValidateEvidenceClaim(claim);

		Assert.Empty(errors);
	}

	[Theory]
	[InlineData(CompatibilityStatus.Unknown)]
	[InlineData(CompatibilityStatus.Unsupported)]
	[InlineData(CompatibilityStatus.PlayableWithCaveats)]
	[InlineData(CompatibilityStatus.Playable)]
	public void CompatibilityStatus_DefinesMinimalNormalizedContract(CompatibilityStatus status)
	{
		Assert.True(Enum.IsDefined(status));
	}

	[Fact]
	public void GameCompatibilitySummary_AllowsMissingGeneratedFields()
	{
		var summary = new GameCompatibilitySummary
		{
			State = SummaryState.NotGenerated,
			SummaryStatus = CompatibilityStatus.Unknown,
			IsStale = true
		};

		Assert.Null(summary.SummaryText);
		Assert.Null(summary.Provider);
		Assert.Null(summary.Model);
		Assert.True(summary.IsStale);
	}
}
