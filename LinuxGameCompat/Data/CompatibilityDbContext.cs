using Microsoft.EntityFrameworkCore;
using LinuxGameCompat.Data.Seed;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace LinuxGameCompat.Data;

public sealed class CompatibilityDbContext(DbContextOptions<CompatibilityDbContext> options)
	: IdentityUserContext<ApplicationUser>(options)
{
	public DbSet<Game> Games => Set<Game>();

	public DbSet<SourceSystem> SourceSystems => Set<SourceSystem>();

	public DbSet<SourceReference> SourceReferences => Set<SourceReference>();

	public DbSet<EvidenceClaim> EvidenceClaims => Set<EvidenceClaim>();

	public DbSet<GameCompatibilitySummary> GameCompatibilitySummaries => Set<GameCompatibilitySummary>();

	public DbSet<MagicLinkRequest> MagicLinkRequests => Set<MagicLinkRequest>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

		modelBuilder.Entity<ApplicationUser>(entity =>
		{
			entity.HasIndex(user => user.NormalizedEmail)
				.HasDatabaseName("EmailIndex")
				.IsUnique()
				.HasFilter("\"NormalizedEmail\" IS NOT NULL");
		});

		modelBuilder.Entity<Game>(entity =>
		{
			entity.Property(game => game.Title).HasMaxLength(200).IsRequired();
			entity.Property(game => game.Slug).HasMaxLength(220).IsRequired();
			entity.Property(game => game.CompatibilityStatus).HasConversion<string>().HasMaxLength(40).IsRequired();
			entity.HasIndex(game => game.Slug).IsUnique();
			entity.HasIndex(game => game.SteamAppId).IsUnique().HasFilter("\"SteamAppId\" IS NOT NULL");
			entity.HasIndex(game => new { game.IsHidden, game.Title });
		});

		modelBuilder.Entity<SourceSystem>(entity =>
		{
			entity.Property(source => source.Type).HasConversion<string>().HasMaxLength(40).IsRequired();
			entity.Property(source => source.Name).HasMaxLength(120).IsRequired();
			entity.Property(source => source.BaseUrl).HasMaxLength(500).IsRequired();
			entity.HasIndex(source => source.Type).IsUnique();
		});

		modelBuilder.Entity<SourceReference>(entity =>
		{
			entity.Property(reference => reference.SourceGameId).HasMaxLength(120).IsRequired();
			entity.Property(reference => reference.Url).HasMaxLength(1000).IsRequired();
			entity.Property(reference => reference.MetadataJson).HasColumnType("jsonb");
			entity.HasIndex(reference => new { reference.SourceSystemId, reference.SourceGameId }).IsUnique();
			entity.HasIndex(reference => reference.Url);
		});

		modelBuilder.Entity<EvidenceClaim>(entity =>
		{
			entity.Property(claim => claim.ClaimType).HasConversion<string>().HasMaxLength(40).IsRequired();
			entity.Property(claim => claim.ClaimValue).HasMaxLength(120).IsRequired();
			entity.Property(claim => claim.ClaimText).HasMaxLength(2000).IsRequired();
			entity.HasIndex(claim => claim.SourceReferenceId);
		});

		modelBuilder.Entity<GameCompatibilitySummary>(entity =>
		{
			entity.Property(summary => summary.State).HasConversion<string>().HasMaxLength(40).IsRequired();
			entity.Property(summary => summary.SummaryStatus).HasConversion<string>().HasMaxLength(40).IsRequired();
			entity.Property(summary => summary.SummaryText).HasMaxLength(4000);
			entity.Property(summary => summary.Provider).HasMaxLength(80);
			entity.Property(summary => summary.Model).HasMaxLength(120);
			entity.Property(summary => summary.EvidenceVersion).HasMaxLength(80);
			entity.Property(summary => summary.EvidenceHash).HasMaxLength(128);
			entity.Property(summary => summary.ErrorCode).HasMaxLength(80);
			entity.Property(summary => summary.ErrorMessage).HasMaxLength(2000);
			entity.HasIndex(summary => summary.GameId).IsUnique();
		});

		modelBuilder.Entity<MagicLinkRequest>(entity =>
		{
			entity.Property(request => request.NormalizedEmail).HasMaxLength(256).IsRequired();
			entity.Property(request => request.TokenHash).HasMaxLength(128).IsRequired();
			entity.Property(request => request.ReturnUrl).HasMaxLength(2048);
			entity.Property(request => request.RequestIpAddress).HasMaxLength(64);
			entity.Property(request => request.UserAgent).HasMaxLength(512);
			entity.HasIndex(request => request.TokenHash).IsUnique();
			entity.HasIndex(request => request.NormalizedEmail);
			entity.HasIndex(request => request.ExpiresAt);
		});

		CompatibilitySeedData.Apply(modelBuilder);
	}
}
