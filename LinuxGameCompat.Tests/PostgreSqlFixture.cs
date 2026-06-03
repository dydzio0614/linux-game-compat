using LinuxGameCompat.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace LinuxGameCompat.Tests;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
	private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
		.WithDatabase("linux_game_compat_tests")
		.WithUsername("linux_game_compat")
		.WithPassword("linux_game_compat_dev")
		.Build();

	public DbContextOptions<CompatibilityDbContext> Options { get; private set; } = null!;
	public string ConnectionString { get; private set; } = string.Empty;

	public async Task InitializeAsync()
	{
		await _postgres.StartAsync();
		ConnectionString = _postgres.GetConnectionString();
		Options = new DbContextOptionsBuilder<CompatibilityDbContext>()
			.UseNpgsql(ConnectionString)
			.Options;

		await using var dbContext = CreateDbContext();
		await dbContext.Database.MigrateAsync();
	}

	public async Task DisposeAsync()
	{
		await _postgres.DisposeAsync();
	}

	public CompatibilityDbContext CreateDbContext()
	{
		return new CompatibilityDbContext(Options);
	}
}
