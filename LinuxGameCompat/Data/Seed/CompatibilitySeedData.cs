using Microsoft.EntityFrameworkCore;

namespace LinuxGameCompat.Data.Seed;

public static class CompatibilitySeedData
{
	private static readonly DateTimeOffset SeededAt = new(2026, 5, 28, 0, 0, 0, TimeSpan.Zero);

	public static void Apply(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<SourceSystem>().HasData(
			new SourceSystem
			{
				Id = 1,
				Type = SourceSystemType.ProtonDb,
				Name = "ProtonDB",
				BaseUrl = "https://www.protondb.com"
			},
			new SourceSystem
			{
				Id = 2,
				Type = SourceSystemType.AreWeAntiCheatYet,
				Name = "Are We Anti-Cheat Yet?",
				BaseUrl = "https://areweanticheatyet.com"
			});

		modelBuilder.Entity<Game>().HasData(
			new Game
			{
				Id = 1,
				Title = "Baldur's Gate 3",
				SteamAppId = 1086940,
				Slug = "baldurs-gate-3",
				CompatibilityStatus = CompatibilityStatus.Playable,
				IsHidden = false,
				CreatedAt = SeededAt,
				UpdatedAt = SeededAt
			},
			new Game
			{
				Id = 2,
				Title = "Helldivers 2",
				SteamAppId = 553850,
				Slug = "helldivers-2",
				CompatibilityStatus = CompatibilityStatus.PlayableWithCaveats,
				IsHidden = false,
				CreatedAt = SeededAt,
				UpdatedAt = SeededAt
			},
			new Game
			{
				Id = 3,
				Title = "Destiny 2",
				SteamAppId = 1085660,
				Slug = "destiny-2",
				CompatibilityStatus = CompatibilityStatus.Unsupported,
				IsHidden = false,
				CreatedAt = SeededAt,
				UpdatedAt = SeededAt
			},
			new Game
			{
				Id = 4,
				Title = "Unnamed Prototype",
				SteamAppId = null,
				Slug = "unnamed-prototype",
				CompatibilityStatus = CompatibilityStatus.Unknown,
				IsHidden = false,
				CreatedAt = SeededAt,
				UpdatedAt = SeededAt
			},
			new Game
			{
				Id = 5,
				Title = "Suppressed Test Record",
				SteamAppId = 999001,
				Slug = "suppressed-test-record",
				CompatibilityStatus = CompatibilityStatus.Unknown,
				IsHidden = true,
				CreatedAt = SeededAt,
				UpdatedAt = SeededAt
			});

		modelBuilder.Entity<SourceReference>().HasData(
			new SourceReference
			{
				Id = 1,
				GameId = 1,
				SourceSystemId = 1,
				SourceGameId = "1086940",
				Url = "https://www.protondb.com/app/1086940",
				MetadataJson = """{"source":"protondb","kind":"steam-app"}""",
				CreatedAt = SeededAt
			},
			new SourceReference
			{
				Id = 2,
				GameId = 2,
				SourceSystemId = 1,
				SourceGameId = "553850",
				Url = "https://www.protondb.com/app/553850",
				MetadataJson = """{"source":"protondb","kind":"steam-app"}""",
				CreatedAt = SeededAt
			},
			new SourceReference
			{
				Id = 3,
				GameId = 2,
				SourceSystemId = 2,
				SourceGameId = "helldivers-2",
				Url = "https://areweanticheatyet.com/game/helldivers-2",
				MetadataJson = """{"source":"areweanticheatyet","kind":"game-page"}""",
				CreatedAt = SeededAt
			},
			new SourceReference
			{
				Id = 4,
				GameId = 3,
				SourceSystemId = 2,
				SourceGameId = "destiny-2",
				Url = "https://areweanticheatyet.com/game/destiny-2",
				MetadataJson = """{"source":"areweanticheatyet","kind":"game-page"}""",
				CreatedAt = SeededAt
			});

		modelBuilder.Entity<EvidenceClaim>().HasData(
			new EvidenceClaim
			{
				Id = 1,
				SourceReferenceId = 1,
				ClaimType = EvidenceClaimType.Status,
				ClaimValue = "playable",
				ClaimText = "Community reports indicate the game is playable through Proton.",
				ObservedAt = SeededAt
			},
			new EvidenceClaim
			{
				Id = 2,
				SourceReferenceId = 3,
				ClaimType = EvidenceClaimType.Caveat,
				ClaimValue = "anti-cheat",
				ClaimText = "Anti-cheat support is a compatibility consideration for multiplayer sessions.",
				ObservedAt = SeededAt
			},
			new EvidenceClaim
			{
				Id = 3,
				SourceReferenceId = 4,
				ClaimType = EvidenceClaimType.Status,
				ClaimValue = "unsupported",
				ClaimText = "Anti-cheat policy blocks Linux/Proton play.",
				ObservedAt = SeededAt
			});

		modelBuilder.Entity<GameCompatibilitySummary>().HasData(
			new GameCompatibilitySummary
			{
				Id = 1,
				GameId = 1,
				State = SummaryState.Current,
				SummaryStatus = CompatibilityStatus.Playable,
				SummaryText = "Playable through Proton based on stored source-backed evidence.",
				Provider = "placeholder",
				Model = "manual-baseline",
				EvidenceVersion = "seed-v1",
				EvidenceHash = "seed-baldurs-gate-3-v1",
				GeneratedAt = SeededAt,
				IsStale = false
			},
			new GameCompatibilitySummary
			{
				Id = 2,
				GameId = 2,
				State = SummaryState.NotGenerated,
				SummaryStatus = CompatibilityStatus.PlayableWithCaveats,
				EvidenceVersion = "seed-v1",
				EvidenceHash = "seed-helldivers-2-v1",
				IsStale = true
			});
	}
}
