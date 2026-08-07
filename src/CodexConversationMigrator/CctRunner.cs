using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace CodexConversationMigrator;

internal static class CctRunner
{
	public static string ResolveCctPath(string preferred)
	{
		List<string> list = new List<string>();
		if (!string.IsNullOrWhiteSpace(preferred))
		{
			list.Add(preferred);
		}
		list.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cct.exe"));
		list.Add(Path.Combine(ResolveUserProfile(), "Downloads\\cct_v1.2.0_windows_amd64\\cct_v1.2.0_windows_amd64\\cct.exe"));
		string text = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
		string[] array = text.Split(Path.PathSeparator);
		foreach (string text2 in array)
		{
			if (!string.IsNullOrWhiteSpace(text2))
			{
				list.Add(Path.Combine(text2.Trim(), "cct.exe"));
			}
		}
		foreach (string item in list)
		{
			try
			{
				string fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(item));
				if (File.Exists(fullPath))
				{
					return fullPath;
				}
			}
			catch
			{
			}
		}
		string path = Path.Combine(ResolveUserProfile(), "Downloads");
		try
		{
			if (Directory.Exists(path))
			{
				string text3 = Directory.EnumerateFiles(path, "cct.exe", SearchOption.AllDirectories).FirstOrDefault();
				if (!string.IsNullOrWhiteSpace(text3))
				{
					return text3;
				}
			}
		}
		catch
		{
		}
		return string.Empty;
	}

	private static string ResolveUserProfile()
	{
		string environmentVariable = Environment.GetEnvironmentVariable("USERPROFILE");
		if (!string.IsNullOrWhiteSpace(environmentVariable))
		{
			return environmentVariable;
		}
		return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
	}

	public static Task<CctResult> RunAsync(string exe, IEnumerable<string> arguments, string workingDirectory)
	{
		return Task.Run(() => Run(exe, arguments, workingDirectory));
	}

	public static CctResult Run(string exe, IEnumerable<string> arguments, string workingDirectory)
	{
		List<string> source = ((arguments == null) ? new List<string>() : arguments.ToList());
		string text = string.Join(" ", source.Select(QuoteArgument).ToArray());
		ProcessStartInfo processStartInfo = new ProcessStartInfo();
		processStartInfo.FileName = exe;
		processStartInfo.Arguments = text;
		processStartInfo.WorkingDirectory = (string.IsNullOrWhiteSpace(workingDirectory) ? AppDomain.CurrentDomain.BaseDirectory : workingDirectory);
		processStartInfo.UseShellExecute = false;
		processStartInfo.RedirectStandardOutput = true;
		processStartInfo.RedirectStandardError = true;
		processStartInfo.CreateNoWindow = true;
		processStartInfo.StandardOutputEncoding = Encoding.UTF8;
		processStartInfo.StandardErrorEncoding = Encoding.UTF8;
		ProcessStartInfo startInfo = processStartInfo;
		using Process process = new Process();
		process.StartInfo = startInfo;
		process.Start();
		Task<string> task = process.StandardOutput.ReadToEndAsync();
		Task<string> task2 = process.StandardError.ReadToEndAsync();
		process.WaitForExit();
		Task.WaitAll(task, task2);
		CctResult cctResult = new CctResult();
		cctResult.ExitCode = process.ExitCode;
		cctResult.StdOut = task.Result ?? string.Empty;
		cctResult.StdErr = task2.Result ?? string.Empty;
		cctResult.CommandLine = QuoteArgument(exe) + " " + text;
		return cctResult;
	}

	public static List<SessionInfo> ParseSessions(string json)
	{
		JavaScriptSerializer javaScriptSerializer = NewSerializer();
		if (!(javaScriptSerializer.DeserializeObject(json) is Dictionary<string, object> dictionary) || !dictionary.ContainsKey("sessions"))
		{
			throw new InvalidDataException("cct list --json 返回的数据中没有 sessions。");
		}
		List<SessionInfo> list = new List<SessionInfo>();
		if (!(dictionary["sessions"] is object[] array))
		{
			return list;
		}
		object[] array2 = array;
		foreach (object obj in array2)
		{
			if (obj is Dictionary<string, object> item)
			{
				string text = GetString(item, "updated_at");
				string text2 = GetString(item, "source");
				list.Add(new SessionInfo
				{
					ThreadId = GetString(item, "thread_id"),
					SessionPath = GetString(item, "path"),
					RelativePath = GetString(item, "rel_path"),
					Cwd = TextHelpers.StripExtendedPrefix(GetString(item, "cwd")),
					Title = string.Empty,
					Preview = GetString(item, "preview"),
					Source = text2,
					UpdatedAt = text,
					UpdatedDate = TextHelpers.ParseDate(text),
					Archived = GetBool(item, "archived"),
					Compressed = GetBool(item, "compressed"),
					IsSubagent = string.Equals(text2, "subagent", StringComparison.OrdinalIgnoreCase)
				});
			}
		}
		return list;
	}

	public static JavaScriptSerializer NewSerializer()
	{
		JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
		javaScriptSerializer.MaxJsonLength = int.MaxValue;
		javaScriptSerializer.RecursionLimit = 256;
		return javaScriptSerializer;
	}

	public static string FirstUseful(CctResult result)
	{
		string text = ((!string.IsNullOrWhiteSpace(result.StdErr)) ? result.StdErr : result.StdOut);
		if (!string.IsNullOrWhiteSpace(text))
		{
			return text.Trim();
		}
		return "cct 执行失败（退出码 " + result.ExitCode + "）。";
	}

	private static string GetString(Dictionary<string, object> item, string key)
	{
		if (!item.TryGetValue(key, out var value) || value == null)
		{
			return string.Empty;
		}
		return Convert.ToString(value);
	}

	private static bool GetBool(Dictionary<string, object> item, string key)
	{
		if (!item.TryGetValue(key, out var value) || value == null)
		{
			return false;
		}
		if (bool.TryParse(Convert.ToString(value), out var result))
		{
			return result;
		}
		return false;
	}

	private static string QuoteArgument(string value)
	{
		if (value == null)
		{
			return "\"\"";
		}
		if (value.Length > 0 && value.IndexOfAny(new char[5] { ' ', '\t', '\n', '\v', '"' }) < 0)
		{
			return value;
		}
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append('"');
		int num = 0;
		foreach (char c in value)
		{
			switch (c)
			{
			case '\\':
				num++;
				break;
			case '"':
				stringBuilder.Append('\\', num * 2 + 1);
				stringBuilder.Append('"');
				num = 0;
				break;
			default:
				stringBuilder.Append('\\', num);
				num = 0;
				stringBuilder.Append(c);
				break;
			}
		}
		stringBuilder.Append('\\', num * 2);
		stringBuilder.Append('"');
		return stringBuilder.ToString();
	}
}
