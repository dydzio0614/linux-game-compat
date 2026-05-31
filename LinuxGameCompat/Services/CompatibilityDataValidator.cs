using LinuxGameCompat.Data;

namespace LinuxGameCompat.Services;

public static class CompatibilityDataValidator
{
	public static IReadOnlyList<string> ValidateGame(Game game)
	{
		var errors = new List<string>();

		if (string.IsNullOrWhiteSpace(game.Title))
		{
			errors.Add("Game title is required.");
		}

		if (string.IsNullOrWhiteSpace(game.Slug))
		{
			errors.Add("Game slug is required.");
		}

		foreach (var sourceReference in game.SourceReferences)
		{
			errors.AddRange(ValidateSourceReference(sourceReference));
			foreach (var claim in sourceReference.EvidenceClaims)
			{
				errors.AddRange(ValidateEvidenceClaim(claim));
			}
		}

		return errors;
	}

	public static IReadOnlyList<string> ValidateEvidenceClaim(EvidenceClaim claim)
	{
		var errors = new List<string>();

		if (string.IsNullOrWhiteSpace(claim.ClaimValue))
		{
			errors.Add("Evidence claim value is required.");
		}

		if (string.IsNullOrWhiteSpace(claim.ClaimText))
		{
			errors.Add("Evidence claim text is required.");
		}

		if (claim.SourceReferenceId <= 0 && claim.SourceReference is null)
		{
			errors.Add("Evidence claim source reference metadata is required.");
		}

		if (claim.SourceReference is not null)
		{
			errors.AddRange(ValidateSourceReference(claim.SourceReference));
		}

		return errors;
	}

	public static IReadOnlyList<string> ValidateSourceReference(SourceReference reference)
	{
		var errors = new List<string>();

		if (reference.SourceSystemId <= 0 && reference.SourceSystem is null)
		{
			errors.Add("Source reference source system metadata is required.");
		}

		if (string.IsNullOrWhiteSpace(reference.SourceGameId))
		{
			errors.Add("Source reference source game ID is required.");
		}

		if (string.IsNullOrWhiteSpace(reference.Url))
		{
			errors.Add("Source reference URL is required.");
		}
		else if (!Uri.TryCreate(reference.Url, UriKind.Absolute, out _))
		{
			errors.Add("Source reference URL must be absolute.");
		}

		return errors;
	}
}
