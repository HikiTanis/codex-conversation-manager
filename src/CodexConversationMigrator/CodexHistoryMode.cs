using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

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

	public static void ValidateSessionFile(string path, string historyMode)
	{
		if (!string.Equals(Normalize(historyMode, path), Paginated, StringComparison.Ordinal))
		{
			return;
		}
		JavaScriptSerializer serializer = new JavaScriptSerializer
		{
			MaxJsonLength = int.MaxValue,
			RecursionLimit = 256
		};
		long expectedOrdinal = 0;
		bool hasTurnContext = false;
		bool hasHistoryRecords = false;
		using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
		using StreamReader reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, 65536);
		string line;
		int lineNumber = 0;
		while ((line = reader.ReadLine()) != null)
		{
			lineNumber++;
			if (string.IsNullOrWhiteSpace(line))
			{
				continue;
			}
			Dictionary<string, object> record;
			try
			{
				record = serializer.DeserializeObject(line) as Dictionary<string, object>;
			}
			catch (Exception ex)
			{
				throw new InvalidDataException("paginated 会话第 " + lineNumber + " 行不是有效 JSON，已取消导入：" + path, ex);
			}
			if (record == null || !record.TryGetValue("ordinal", out object ordinalValue) || ordinalValue == null ||
				!long.TryParse(Convert.ToString(ordinalValue, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out long ordinal))
			{
				throw new InvalidDataException("paginated 会话第 " + lineNumber + " 行缺少整数 ordinal，Codex 将无法恢复该会话，已取消导入：" + path);
			}
			if (ordinal != expectedOrdinal)
			{
				throw new InvalidDataException("paginated 会话 ordinal 不连续；第 " + lineNumber + " 行应为 " + expectedOrdinal + "，实际为 " + ordinal + "，已取消导入：" + path);
			}
			expectedOrdinal++;
			string type = record.TryGetValue("type", out object typeValue) ? Convert.ToString(typeValue, CultureInfo.InvariantCulture) : string.Empty;
			hasTurnContext |= string.Equals(type, "turn_context", StringComparison.OrdinalIgnoreCase);
			hasHistoryRecords |= !string.Equals(type, "session_meta", StringComparison.OrdinalIgnoreCase);
		}
		if (expectedOrdinal == 0)
		{
			throw new InvalidDataException("paginated 会话为空，已取消导入：" + path);
		}
		if (hasHistoryRecords && !hasTurnContext)
		{
			throw new InvalidDataException("paginated 会话缺少 turn_context，Codex 无法还原对话轮次，已取消导入：" + path);
		}
	}
}
