using Npgsql;

namespace LinuxGameCompat.Data;

public static class CompatibilityDbContextOptions
{
	public const string ConnectionStringName = "CompatibilityDatabase";

	public static string GetConnectionString(IConfiguration configuration)
	{
		var databaseUrl = configuration["DATABASE_URL"];
		if (!string.IsNullOrWhiteSpace(databaseUrl))
		{
			return ConvertDatabaseUrl(databaseUrl);
		}

		var connectionString = configuration.GetConnectionString(ConnectionStringName);
		if (!string.IsNullOrWhiteSpace(connectionString))
		{
			return connectionString;
		}

		throw new InvalidOperationException(
			$"Database configuration is missing. Set ConnectionStrings:{ConnectionStringName} or DATABASE_URL.");
	}

	private static string ConvertDatabaseUrl(string databaseUrl)
	{
		var uri = new Uri(databaseUrl);
		var userInfo = uri.UserInfo.Split(':', 2);

		var builder = new NpgsqlConnectionStringBuilder
		{
			Host = uri.Host,
			Port = uri.Port > 0 ? uri.Port : 5432,
			Database = uri.AbsolutePath.TrimStart('/'),
			Username = Uri.UnescapeDataString(userInfo[0]),
			Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty,
			SslMode = SslMode.Require
		};

		return builder.ConnectionString;
	}
}
