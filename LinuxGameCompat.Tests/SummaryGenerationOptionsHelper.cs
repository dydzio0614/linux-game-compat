using LinuxGameCompat.Services.SummaryGeneration;
using Microsoft.Extensions.Configuration;

namespace LinuxGameCompat.Tests;

internal static class SummaryGenerationOptionsHelper
{
	public static GenerationOptions FromAppSettings()
	{
		string? appSettingsPath = FindAppSettingsPath();
		if (appSettingsPath is null)
			throw new FileNotFoundException("Could not locate LinuxGameCompat/appsettings.json from the test output directory.");

		GenerationOptions options = new();
		new ConfigurationBuilder()
			.AddJsonFile(appSettingsPath, optional: false)
			.Build()
			.GetSection(GenerationOptions.SectionName)
			.Bind(options);

		return options;
	}

	private static string? FindAppSettingsPath()
	{
		DirectoryInfo? directory = new(AppContext.BaseDirectory);
		while (directory is not null)
		{
			string candidate = Path.Combine(directory.FullName, "LinuxGameCompat", "appsettings.json");
			if (File.Exists(candidate)) return candidate;
			directory = directory.Parent;
		}

		return null;
	}
}
