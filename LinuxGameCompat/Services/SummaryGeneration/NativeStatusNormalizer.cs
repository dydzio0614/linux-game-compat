using LinuxGameCompat.Data;

namespace LinuxGameCompat.Services.SummaryGeneration;

public static class NativeStatusNormalizer
{
	public static CompatibilityStatus? Parse(SourceSystemType sourceType, string? nativeValue)
	{
		if (string.IsNullOrWhiteSpace(nativeValue)) return null;
		string normalizedValue = nativeValue.Trim().ToUpperInvariant();

		return (sourceType, normalizedValue) switch
		{
			(SourceSystemType.ProtonDb, "PLATINUM" or "NATIVE" or "GOLD" or "T1" or "S" or "A" or "PLAYABLE" or "VERIFIED")
				=> CompatibilityStatus.Playable,
			(SourceSystemType.ProtonDb, "BRONZE" or "SILVER" or "T2" or "T3" or "T4" or "B" or "C" or "D")
				=> CompatibilityStatus.PlayableWithCaveats,
			(SourceSystemType.ProtonDb, "BORKED" or "T5" or "F" or "UNSUPPORTED")
				=> CompatibilityStatus.Unsupported,
			(SourceSystemType.AreWeAntiCheatYet, "SUPPORTED")
				=> CompatibilityStatus.Playable,
			(SourceSystemType.AreWeAntiCheatYet, "RUNNING")
				=> CompatibilityStatus.PlayableWithCaveats,
			(SourceSystemType.AreWeAntiCheatYet, "BROKEN" or "DENIED" or "PLANNED")
				=> CompatibilityStatus.Unsupported,
			_ => null
		};
	}

	public static CompatibilityStatus? Reduce(IEnumerable<NativeStatusEvidence> evidence)
	{
		static int Severity(CompatibilityStatus status) => status switch
		{
			CompatibilityStatus.Unsupported => 3,
			CompatibilityStatus.PlayableWithCaveats => 2,
			CompatibilityStatus.Playable => 1,
			_ => 0
		};

		CompatibilityStatus? result = null;
		foreach (NativeStatusEvidence item in evidence)
		{
			CompatibilityStatus? parsed = Parse(item.SourceType, item.Value);
			if (parsed is not null && (result is null || Severity(parsed.Value) > Severity(result.Value))) result = parsed;
		}
		return result;
	}
}

public readonly record struct NativeStatusEvidence(SourceSystemType SourceType, string? Value);
