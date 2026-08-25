using System;
using System.IO;

namespace CodexConversationMigrator;

internal static class CodexHistoryMode
{
	public const string Legacy = "legacy";

	public const string Paginated = "paginated";

	public static string Normalize(string value, string context)
	{
		string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
		if (normalized.Length == 0)
		{
			return Legacy;
		}
		if (string.Equals(normalized, Legacy, StringComparison.Ordinal) ||
			string.Equals(normalized, Paginated, StringComparison.Ordinal))
		{
			return normalized;
		}
		throw new InvalidDataException(
			"会话使用了当前版本尚不支持的历史模式：" + normalized +
			(string.IsNullOrWhiteSpace(context) ? string.Empty : ("\n会话：" + context)));
	}
}
