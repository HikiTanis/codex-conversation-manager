using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace CodexConversationMigrator;

internal static class ConversationReader
{
	private const int MaxMessages = 1200;

	private const int MaxCharacters = 4000000;

	private const int MaxSingleMessage = 80000;

	public static ConversationReadResult Read(SessionInfo session)
	{
		if (session == null)
		{
			throw new ArgumentNullException("session");
		}
		string text = CodexCatalog.ResolveSessionPath(session);
		if (string.IsNullOrWhiteSpace(text) || !File.Exists(text))
		{
			throw new FileNotFoundException("找不到该对话对应的本地会话文件。", text);
		}
		ConversationReadResult conversationReadResult = new ConversationReadResult();
		conversationReadResult.Messages = new List<ConversationMessage>();
		conversationReadResult.SessionPath = text;
		ConversationReadResult conversationReadResult2 = conversationReadResult;
		if (text.EndsWith(".zst", StringComparison.OrdinalIgnoreCase))
		{
			conversationReadResult2.Messages.Add(Notice(UiLanguage.T("该会话使用 zstd 压缩，当前版本可备份，但暂不支持预览或迁移导入该压缩会话。")));
			return conversationReadResult2;
		}
		JavaScriptSerializer javaScriptSerializer = CctRunner.NewSerializer();
		int num = 0;
		using (FileStream stream = new FileStream(text, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
		{
			using StreamReader streamReader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, 8192);
			string text2;
			while ((text2 = streamReader.ReadLine()) != null)
			{
				if (conversationReadResult2.Messages.Count >= 1200 || num >= 4000000)
				{
					conversationReadResult2.Truncated = true;
					break;
				}
				if (string.IsNullOrWhiteSpace(text2))
				{
					continue;
				}
				try
				{
					if (!(javaScriptSerializer.DeserializeObject(text2) is Dictionary<string, object> dictionary) || GetString(dictionary, "type") != "response_item")
					{
						continue;
					}
					object value;
					Dictionary<string, object> dictionary2 = (dictionary.TryGetValue("payload", out value) ? (value as Dictionary<string, object>) : null);
					if (dictionary2 == null || GetString(dictionary2, "type") != "message")
					{
						continue;
					}
					string a = GetString(dictionary2, "role");
					bool flag = string.Equals(a, "user", StringComparison.OrdinalIgnoreCase);
					bool flag2 = string.Equals(a, "assistant", StringComparison.OrdinalIgnoreCase);
					if (!flag && !flag2)
					{
						continue;
					}
					string value2 = ExtractText(dictionary2);
					if (flag)
					{
						value2 = StripAmbientContext(value2);
					}
					value2 = NormalizeMessage(value2);
					if (string.IsNullOrWhiteSpace(value2))
					{
						continue;
					}
					if (value2.Length > 80000)
					{
						value2 = value2.Substring(0, 80000) + "\n\n［该条消息过长，预览已截断］";
						conversationReadResult2.Truncated = true;
					}
					if (num + value2.Length > 4000000)
					{
						int num2 = Math.Max(0, 4000000 - num);
						if (num2 > 300)
						{
							conversationReadResult2.Messages.Add(new ConversationMessage
							{
								RoleLabel = (flag ? UiLanguage.T("你") : "Codex"),
								Text = value2.Substring(0, Math.Min(value2.Length, num2)) + UiLanguage.T("\n\n［后续内容已截断］"),
								DisplayTime = FormatTime(GetString(dictionary, "timestamp")),
								IsUser = flag
							});
						}
						conversationReadResult2.Truncated = true;
						break;
					}
					ConversationMessage conversationMessage = new ConversationMessage();
					conversationMessage.RoleLabel = (flag ? UiLanguage.T("你") : "Codex");
					conversationMessage.Text = value2;
					conversationMessage.DisplayTime = FormatTime(GetString(dictionary, "timestamp"));
					conversationMessage.IsUser = flag;
					ConversationMessage conversationMessage2 = conversationMessage;
					ConversationMessage conversationMessage3 = conversationReadResult2.Messages.LastOrDefault();
					if (conversationMessage3 == null || conversationMessage3.IsUser != conversationMessage2.IsUser || !(conversationMessage3.Text == conversationMessage2.Text))
					{
						conversationReadResult2.Messages.Add(conversationMessage2);
						num += value2.Length;
					}
				}
				catch
				{
				}
			}
		}
		if (conversationReadResult2.Messages.Count == 0)
		{
			conversationReadResult2.Messages.Add(Notice(UiLanguage.T("没有在该文件中找到可显示的用户或 Codex 文本消息。")));
		}
		else if (conversationReadResult2.Truncated)
		{
			conversationReadResult2.Messages.Add(Notice(UiLanguage.T("这段对话很长，为保证界面流畅，预览只显示了前面的部分；原始会话没有被修改。")));
		}
		return conversationReadResult2;
	}

	public static string ReadTitleCandidate(string path)
	{
		if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) || path.EndsWith(".zst", StringComparison.OrdinalIgnoreCase))
		{
			return string.Empty;
		}
		JavaScriptSerializer serializer = CctRunner.NewSerializer();
		int lines = 0;
		int characters = 0;
		try
		{
			using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
			using StreamReader reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, 8192);
			string line;
			while (lines < 240 && characters < 2000000 && (line = reader.ReadLine()) != null)
			{
				lines++;
				characters += line.Length;
				if (!(serializer.DeserializeObject(line) is Dictionary<string, object> item) || GetString(item, "type") != "response_item")
				{
					continue;
				}
				object value;
				Dictionary<string, object> payload = item.TryGetValue("payload", out value) ? value as Dictionary<string, object> : null;
				if (payload == null || GetString(payload, "type") != "message" || !string.Equals(GetString(payload, "role"), "user", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				string candidate = NormalizeMessage(StripAmbientContext(ExtractText(payload)));
				if (IsTitleNoise(candidate))
				{
					continue;
				}
				return TextHelpers.CleanLine(candidate, 76, string.Empty);
			}
		}
		catch
		{
		}
		return string.Empty;
	}

	private static bool IsTitleNoise(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return true;
		}
		string text = value.Trim();
		return text.StartsWith("<turn_aborted", StringComparison.OrdinalIgnoreCase) ||
			text.StartsWith("The following is the Codex agent history", StringComparison.OrdinalIgnoreCase) ||
			text.IndexOf("whose request action you are assessing", StringComparison.OrdinalIgnoreCase) >= 0;
	}

	private static string ExtractText(Dictionary<string, object> payload)
	{
		if (!payload.TryGetValue("content", out var value) || value == null)
		{
			return string.Empty;
		}
		List<string> list = new List<string>();
		if (!(value is object[] array))
		{
			return string.Empty;
		}
		object[] array2 = array;
		foreach (object obj in array2)
		{
			if (!(obj is Dictionary<string, object> item))
			{
				continue;
			}
			string text = GetString(item, "type");
			string text2 = GetString(item, "text");
			switch (text)
			{
			case "input_text":
			case "output_text":
			case "text":
				if (!string.IsNullOrWhiteSpace(text2))
				{
					list.Add(text2);
					continue;
				}
				break;
			}
			if (text == "input_image")
			{
				list.Add("［图片］");
			}
		}
		return string.Join("\n\n", list.ToArray());
	}

	private static string StripAmbientContext(string value)
	{
		string input = value ?? string.Empty;
		input = Regex.Replace(input, "<(recommended_plugins|environment_context|in-app-browser-context)\\b[^>]*>.*?</\\1>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
		return Regex.Replace(input, "<image\\b[^>]*>", "［图片］", RegexOptions.IgnoreCase);
	}

	private static string NormalizeMessage(string value)
	{
		string input = (value ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Trim();
		return Regex.Replace(input, "\\n{3,}", "\n\n");
	}

	private static string FormatTime(string value)
	{
		if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var result))
		{
			return string.Empty;
		}
		return result.ToLocalTime().ToString("MM-dd HH:mm");
	}

	private static ConversationMessage Notice(string text)
	{
		ConversationMessage conversationMessage = new ConversationMessage();
		conversationMessage.RoleLabel = UiLanguage.T("提示");
		conversationMessage.Text = text;
		conversationMessage.DisplayTime = string.Empty;
		conversationMessage.IsNotice = true;
		return conversationMessage;
	}

	private static string GetString(Dictionary<string, object> item, string key)
	{
		if (!item.TryGetValue(key, out var value) || value == null)
		{
			return string.Empty;
		}
		return Convert.ToString(value);
	}
}
