using System;
using System.Globalization;
using System.IO;

namespace CodexConversationManager;

internal static class TextHelpers
{
	public static string CleanLine(string value, int maxLength, string fallback)
	{
		string text = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
		while (text.Contains("  "))
		{
			text = text.Replace("  ", " ");
		}
		if (text.Length > maxLength)
		{
			text = text.Substring(0, maxLength) + "…";
		}
		if (!string.IsNullOrWhiteSpace(text))
		{
			return text;
		}
		return fallback;
	}

	public static string SafeFileName(string value)
	{
		string text = (string.IsNullOrWhiteSpace(value) ? UiLanguage.T("Codex项目") : value);
		char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
		foreach (char oldChar in invalidFileNameChars)
		{
			text = text.Replace(oldChar, '_');
		}
		return text;
	}

	public static string StripExtendedPrefix(string path)
	{
		string text = (path ?? string.Empty).Trim();
		if (text.StartsWith("\\\\?\\UNC\\", StringComparison.OrdinalIgnoreCase))
		{
			return "\\\\" + text.Substring(8);
		}
		if (text.StartsWith("\\\\?\\", StringComparison.OrdinalIgnoreCase))
		{
			return text.Substring(4);
		}
		return text;
	}

	public static bool HasExtendedPrefix(string path)
	{
		string text = (path ?? string.Empty).Trim();
		return text.StartsWith("\\\\?\\", StringComparison.OrdinalIgnoreCase);
	}

	public static string ToCodexIndexPath(string path)
	{
		string text = StripExtendedPrefix(path).Replace('/', '\\').Trim();
		if (text.Length == 0)
		{
			return string.Empty;
		}
		string fullPath = Path.GetFullPath(text);
		if (fullPath.StartsWith("\\\\", StringComparison.OrdinalIgnoreCase))
		{
			return "\\\\?\\UNC\\" + fullPath.TrimStart('\\');
		}
		return "\\\\?\\" + fullPath;
	}

	public static string CanonicalPath(string path)
	{
		string text = StripExtendedPrefix(path).Replace('/', '\\').Trim();
		if (text.Length == 0)
		{
			return string.Empty;
		}
		try
		{
			text = Path.GetFullPath(text);
		}
		catch
		{
		}
		return text.TrimEnd('\\').ToLowerInvariant();
	}

	public static bool IsWithin(string child, string root)
	{
		string text = CanonicalPath(child);
		string text2 = CanonicalPath(root);
		if (text.Length == 0 || text2.Length == 0)
		{
			return false;
		}
		if (!string.Equals(text, text2, StringComparison.OrdinalIgnoreCase))
		{
			return text.StartsWith(text2 + "\\", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	public static DateTime ParseDate(string value)
	{
		if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var result))
		{
			return result.ToLocalTime();
		}
		if (DateTime.TryParse(value, out result))
		{
			return result.ToLocalTime();
		}
		return DateTime.MinValue;
	}

	public static DateTime FromUnixMilliseconds(long value)
	{
		if (value <= 0)
		{
			return DateTime.MinValue;
		}
		try
		{
			return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(value).ToLocalTime();
		}
		catch
		{
			return DateTime.MinValue;
		}
	}

	public static string FormatBytes(long bytes)
	{
		long value = Math.Max(0L, bytes);
		if (value >= 1099511627776L)
		{
			return (value / 1099511627776.0).ToString("0.##", CultureInfo.InvariantCulture) + " TB";
		}
		if (value >= 1073741824L)
		{
			return (value / 1073741824.0).ToString("0.##", CultureInfo.InvariantCulture) + " GB";
		}
		if (value >= 1048576L)
		{
			return (value / 1048576.0).ToString("0.##", CultureInfo.InvariantCulture) + " MB";
		}
		if (value >= 1024L)
		{
			return (value / 1024.0).ToString("0.##", CultureInfo.InvariantCulture) + " KB";
		}
		return value + " B";
	}
}
