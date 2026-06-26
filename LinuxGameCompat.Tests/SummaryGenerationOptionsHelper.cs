using LinuxGameCompat.Services.SummaryGeneration;
using Microsoft.Extensions.Configuration;

namespace LinuxGameCompat.Tests;

internal static class SummaryGenerationOptionsHelper
{
	public static GenerationOptions FromAppSettings()
	{
		string appSettingsPath = Path.GetFullPath(Path.Combine(
			AppContext.BaseDirectory,
			"..", "..", "..", "..",
			"LinuxGameCompat",
			"appsettings.json"));

		GenerationOptions options = new();
		new ConfigurationBuilder()
			.AddJsonFile(appSettingsPath, optional: false)
			.Build()
			.GetSection(GenerationOptions.SectionName)
			.Bind(options);

		return options;
	}
}
