namespace LinuxGameCompat.Services;

internal static class LocalReturnUrlNormalizer
{
	private const int MaxReturnUrlLength = 2048;

	public static string Normalize(string? returnUrl)
	{
		if (string.IsNullOrWhiteSpace(returnUrl) ||
			returnUrl.Length > MaxReturnUrlLength ||
			returnUrl.Contains('\\', StringComparison.Ordinal) ||
			ContainsEncodedSlashOrBackslash(returnUrl) ||
			!Uri.TryCreate(returnUrl, UriKind.Relative, out var uri))
		{
			return "/";
		}

		var value = uri.ToString();
		if (value.Length > MaxReturnUrlLength ||
			!value.StartsWith("/", StringComparison.Ordinal) ||
			value.StartsWith("//", StringComparison.Ordinal))
		{
			return "/";
		}

		return value;
	}

	private static bool ContainsEncodedSlashOrBackslash(string value)
	{
		for (var index = 0; index <= value.Length - 3; index++)
		{
			if (value[index] != '%')
			{
				continue;
			}

			var first = value[index + 1];
			var second = value[index + 2];
			if (first == '2' && second is 'f' or 'F')
			{
				return true;
			}

			if (first == '5' && second is 'c' or 'C')
			{
				return true;
			}
		}

		return false;
	}
}
