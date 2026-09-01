using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CodexConversationMigrator;

internal sealed class OfficialThreadDeletionResult
{
	public bool Succeeded { get; set; }

	public string CodexPath { get; set; }

	public string Error { get; set; }
}

internal static class CodexAppServerThreadDeletion
{
	private const int TimeoutMilliseconds = 45000;

	private static readonly object VersionCacheGate = new object();

	private static readonly Dictionary<string, Version> VersionCache = new Dictionary<string, Version>(StringComparer.OrdinalIgnoreCase);

	internal static Func<string, string, OfficialThreadDeletionResult> TestOverride { get; set; }

	internal static Func<string, string> VersionOutputOverrideForTest { get; set; }

	public static OfficialThreadDeletionResult DeleteThread(string codexHome, string threadId)
	{
		if (string.IsNullOrWhiteSpace(codexHome))
		{
			throw new ArgumentException("Codex 目录为空。", nameof(codexHome));
		}
		if (string.IsNullOrWhiteSpace(threadId))
		{
			throw new ArgumentException("Thread ID 为空。", nameof(threadId));
		}
		if (TestOverride != null)
		{
			OfficialThreadDeletionResult testResult = TestOverride(codexHome, threadId);
			if (testResult == null)
			{
				throw new InvalidOperationException("Codex 官方删除测试回调没有返回结果。");
			}
			if (!testResult.Succeeded)
			{
				throw new InvalidOperationException(BuildFailureMessage(testResult.Error));
			}
			return testResult;
		}

		string codexPath = ResolveCodexPath();
		if (string.IsNullOrWhiteSpace(codexPath))
		{
			throw new FileNotFoundException("没有找到可独立调用的 Codex CLI（codex.exe）。会话删除和旧侧边栏修复需要先安装 Codex CLI；为避免留下打不开的侧边栏记录，本次没有删除任何会话。");
		}

		string requestId = "ccm-delete-" + Guid.NewGuid().ToString("N");
		ProcessStartInfo startInfo = new ProcessStartInfo
		{
			FileName = codexPath,
			Arguments = "app-server --stdio",
			WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
			UseShellExecute = false,
			RedirectStandardInput = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			CreateNoWindow = true,
			StandardOutputEncoding = Encoding.UTF8,
			StandardErrorEncoding = Encoding.UTF8
		};
		startInfo.EnvironmentVariables["CODEX_HOME"] = Path.GetFullPath(codexHome);

		using Process process = new Process { StartInfo = startInfo };
		Task<string> standardError = null;
		try
		{
			process.Start();
			standardError = process.StandardError.ReadToEndAsync();
			WriteRequest(process, new Dictionary<string, object>
			{
				{ "id", "ccm-initialize" },
				{ "method", "initialize" },
				{ "params", new Dictionary<string, object>
					{
						{ "clientInfo", new Dictionary<string, object>
							{
								{ "name", "codex-conversation-migrator" },
								{ "title", "Codex Conversation Migrator" },
								{ "version", typeof(CodexAppServerThreadDeletion).Assembly.GetName().Version?.ToString(3) ?? "1.0.0" }
							}
						}
					}
				}
			});
			WriteRequest(process, new Dictionary<string, object> { { "method", "initialized" } });
			WriteRequest(process, new Dictionary<string, object>
			{
				{ "id", requestId },
				{ "method", "thread/delete" },
				{ "params", new Dictionary<string, object> { { "threadId", threadId } } }
			});
			process.StandardInput.Flush();

			DateTime deadline = DateTime.UtcNow.AddMilliseconds(TimeoutMilliseconds);
			while (DateTime.UtcNow < deadline)
			{
				int remaining = Math.Max(1, (int)(deadline - DateTime.UtcNow).TotalMilliseconds);
				Task<string> readLine = process.StandardOutput.ReadLineAsync();
				Task completed = Task.WhenAny(readLine, Task.Delay(remaining)).GetAwaiter().GetResult();
				if (!ReferenceEquals(completed, readLine))
				{
					throw new TimeoutException("Codex 官方删除接口等待超时；本次没有继续执行本地删除。");
				}
				string line = readLine.GetAwaiter().GetResult();
				if (line == null)
				{
					string stderr = ReadCompletedError(standardError, waitForCompletion: true);
					throw new InvalidOperationException("Codex app-server 在返回删除结果前提前退出；本次没有继续执行本地删除。\n程序：" + codexPath + (string.IsNullOrWhiteSpace(stderr) ? string.Empty : "\n" + stderr));
				}
				Dictionary<string, object> response = ParseObject(line);
				if (!string.Equals(Value(response, "id"), requestId, StringComparison.Ordinal))
				{
					continue;
				}
				if (response.TryGetValue("error", out object errorValue) && errorValue is Dictionary<string, object> error)
				{
					string message = Value(error, "message");
					throw new InvalidOperationException(BuildFailureMessage(message));
				}
				return new OfficialThreadDeletionResult
				{
					Succeeded = true,
					CodexPath = codexPath,
					Error = string.Empty
				};
			}
			throw new TimeoutException("Codex 官方删除接口没有返回结果；本次没有继续执行本地删除。");
		}
		catch (Exception ex) when (!(ex is InvalidOperationException) && !(ex is TimeoutException) && !(ex is FileNotFoundException))
		{
			string stderr = ReadCompletedError(standardError, waitForCompletion: false);
			throw new InvalidOperationException("无法调用 Codex 官方删除接口；为避免留下打不开的侧边栏记录，本次没有继续删除。" + (string.IsNullOrWhiteSpace(stderr) ? string.Empty : "\n" + stderr), ex);
		}
		finally
		{
			try
			{
				process.StandardInput.Close();
			}
			catch
			{
			}
			try
			{
				if (!process.HasExited && !process.WaitForExit(1500))
				{
					process.Kill();
					process.WaitForExit(1500);
				}
			}
			catch
			{
			}
		}
	}

	public static string ResolveCodexPath()
	{
		string explicitPath = ResolveExistingCandidate(Environment.GetEnvironmentVariable("CODEX_MIGRATOR_CODEX_PATH"));
		if (!string.IsNullOrWhiteSpace(explicitPath))
		{
			return explicitPath;
		}

		List<string> candidates = new List<string>();
		AddCandidate(candidates, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "codex.exe"));
		string userProfile = Environment.GetEnvironmentVariable("USERPROFILE");
		if (string.IsNullOrWhiteSpace(userProfile))
		{
			userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		}
		string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
		string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		AddCodexDesktopCandidates(candidates, Path.Combine(localAppData, "OpenAI", "Codex"));
		AddCodexDesktopCandidates(candidates, Path.Combine(localAppData, "Programs", "OpenAI", "Codex"));
		AddCandidate(candidates, Path.Combine(appData, "npm", "node_modules", "@openai", "codex", "node_modules", "@openai", "codex-win32-x64", "vendor", "x86_64-pc-windows-msvc", "bin", "codex.exe"));
		AddCandidate(candidates, Path.Combine(appData, "npm", "node_modules", "@openai", "codex", "node_modules", "@openai", "codex-win32-arm64", "vendor", "aarch64-pc-windows-msvc", "bin", "codex.exe"));
		AddCandidate(candidates, Path.Combine(userProfile, ".cache", "codex-runtimes", "codex-primary-runtime", "codex.exe"));
		AddVersionedExtensionCandidates(candidates, Path.Combine(userProfile, ".vscode", "extensions"));
		AddVersionedExtensionCandidates(candidates, Path.Combine(userProfile, ".vscode-insiders", "extensions"));
		AddVersionedExtensionCandidates(candidates, Path.Combine(userProfile, ".cursor", "extensions"));

		string pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
		foreach (string pathEntry in pathValue.Split(Path.PathSeparator))
		{
			if (!string.IsNullOrWhiteSpace(pathEntry))
			{
				string pathDirectory = pathEntry.Trim().Trim('"');
				AddCandidate(candidates, Path.Combine(pathDirectory, "codex.exe"));
				AddCandidate(candidates, Path.Combine(pathDirectory, "node_modules", "@openai", "codex", "node_modules", "@openai", "codex-win32-x64", "vendor", "x86_64-pc-windows-msvc", "bin", "codex.exe"));
				AddCandidate(candidates, Path.Combine(pathDirectory, "node_modules", "@openai", "codex", "node_modules", "@openai", "codex-win32-arm64", "vendor", "aarch64-pc-windows-msvc", "bin", "codex.exe"));
			}
		}

		return SelectNewestCodexCandidate(candidates);
	}

	internal static string SelectNewestCodexCandidateForTest(IEnumerable<string> candidates)
	{
		return SelectNewestCodexCandidate(candidates);
	}

	internal static IReadOnlyList<string> CodexDesktopCandidatesForTest(string installRoot)
	{
		List<string> candidates = new List<string>();
		AddCodexDesktopCandidates(candidates, installRoot);
		return candidates;
	}

	private static string SelectNewestCodexCandidate(IEnumerable<string> candidates)
	{
		return (candidates ?? Enumerable.Empty<string>())
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.Select((candidate, order) => new { Path = ResolveExistingCandidate(candidate), Order = order })
			.Where(candidate => !string.IsNullOrWhiteSpace(candidate.Path))
			.Select(candidate => new { candidate.Path, candidate.Order, Version = ReadCodexVersion(candidate.Path) })
			.OrderByDescending(candidate => candidate.Version)
			.ThenBy(candidate => candidate.Order)
			.Select(candidate => candidate.Path)
			.FirstOrDefault() ?? string.Empty;
	}

	private static void AddCodexDesktopCandidates(ICollection<string> candidates, string installRoot)
	{
		try
		{
			AddCandidate(candidates, Path.Combine(installRoot, "bin", "codex.exe"));
			AddCandidate(candidates, Path.Combine(installRoot, "app", "bin", "codex.exe"));
			string binRoot = Path.Combine(installRoot, "bin");
			if (Directory.Exists(binRoot))
			{
				foreach (string runtimeDirectory in Directory.EnumerateDirectories(binRoot, "*", SearchOption.TopDirectoryOnly))
				{
					AddCandidate(candidates, Path.Combine(runtimeDirectory, "codex.exe"));
				}
			}
			if (!Directory.Exists(installRoot))
			{
				return;
			}
			foreach (string versionDirectory in Directory.EnumerateDirectories(installRoot, "*", SearchOption.TopDirectoryOnly))
			{
				AddCandidate(candidates, Path.Combine(versionDirectory, "bin", "codex.exe"));
				AddCandidate(candidates, Path.Combine(versionDirectory, "resources", "codex.exe"));
				AddCandidate(candidates, Path.Combine(versionDirectory, "resources", "bin", "codex.exe"));
			}
		}
		catch
		{
		}
	}

	private static void AddVersionedExtensionCandidates(ICollection<string> candidates, string extensionsRoot)
	{
		try
		{
			if (!Directory.Exists(extensionsRoot))
			{
				return;
			}
			foreach (string extension in Directory.EnumerateDirectories(extensionsRoot, "openai.chatgpt-*-win32-*", SearchOption.TopDirectoryOnly))
			{
				AddCandidate(candidates, Path.Combine(extension, "bin", "windows-x86_64", "codex.exe"));
				AddCandidate(candidates, Path.Combine(extension, "bin", "windows-aarch64", "codex.exe"));
			}
		}
		catch
		{
		}
	}

	private static string ResolveExistingCandidate(string candidate)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(candidate))
			{
				return string.Empty;
			}
			string fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(candidate));
			return File.Exists(fullPath) && fullPath.IndexOf("\\WindowsApps\\", StringComparison.OrdinalIgnoreCase) < 0 ? fullPath : string.Empty;
		}
		catch
		{
			return string.Empty;
		}
	}

	private static Version ReadCodexVersion(string path)
	{
		Func<string, string> testOverride = VersionOutputOverrideForTest;
		if (testOverride != null)
		{
			return ParseCodexVersion(testOverride(path));
		}
		lock (VersionCacheGate)
		{
			if (VersionCache.TryGetValue(path, out Version cached))
			{
				return cached;
			}
		}
		Version resolved = new Version(0, 0);
		try
		{
			FileVersionInfo info = FileVersionInfo.GetVersionInfo(path);
			resolved = ParseCodexVersion(string.IsNullOrWhiteSpace(info.ProductVersion) ? info.FileVersion : info.ProductVersion);
		}
		catch
		{
		}
		if (resolved.CompareTo(new Version(0, 0)) == 0)
		{
			resolved = ProbeCodexVersion(path);
		}
		lock (VersionCacheGate)
		{
			VersionCache[path] = resolved;
		}
		return resolved;
	}

	private static Version ProbeCodexVersion(string path)
	{
		try
		{
			ProcessStartInfo info = new ProcessStartInfo
			{
				FileName = path,
				Arguments = "--version",
				WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true,
				StandardOutputEncoding = Encoding.UTF8,
				StandardErrorEncoding = Encoding.UTF8
			};
			using Process process = new Process { StartInfo = info };
			process.Start();
			Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
			Task<string> standardError = process.StandardError.ReadToEndAsync();
			if (!process.WaitForExit(3000))
			{
				try
				{
					process.Kill();
				}
				catch
				{
				}
				return new Version(0, 0);
			}
			return ParseCodexVersion(standardOutput.GetAwaiter().GetResult() + " " + standardError.GetAwaiter().GetResult());
		}
		catch
		{
			return new Version(0, 0);
		}
	}

	private static Version ParseCodexVersion(string raw)
	{
		Match match = Regex.Match(raw ?? string.Empty, @"\d+(?:\.\d+){1,3}");
		return match.Success && Version.TryParse(match.Value, out Version version) ? version : new Version(0, 0);
	}
	private static void AddCandidate(ICollection<string> candidates, string value)
	{
		if (!string.IsNullOrWhiteSpace(value))
		{
			candidates.Add(value);
		}
	}

	private static void WriteRequest(Process process, Dictionary<string, object> request)
	{
		process.StandardInput.WriteLine(JsonSerialization.NewSerializer().Serialize(request));
	}

	private static Dictionary<string, object> ParseObject(string json)
	{
		try
		{
			return JsonSerialization.NewSerializer().DeserializeObject(json) as Dictionary<string, object> ?? new Dictionary<string, object>();
		}
		catch
		{
			return new Dictionary<string, object>();
		}
	}

	private static string Value(Dictionary<string, object> source, string key)
	{
		return source != null && source.TryGetValue(key, out object value) && value != null ? Convert.ToString(value) : string.Empty;
	}

	private static string BuildFailureMessage(string detail)
	{
		return "Codex 官方删除接口拒绝了本次操作；为避免会话内容已删但侧边栏仍残留，本次没有继续执行本地删除。" + (string.IsNullOrWhiteSpace(detail) ? string.Empty : "\n" + detail.Trim());
	}

	private static string ReadCompletedError(Task<string> standardError, bool waitForCompletion)
	{
		if (standardError == null)
		{
			return string.Empty;
		}
		if (!standardError.IsCompleted && waitForCompletion)
		{
			Task.WhenAny(standardError, Task.Delay(1500)).GetAwaiter().GetResult();
		}
		if (!standardError.IsCompleted)
		{
			return string.Empty;
		}
		try
		{
			string value = standardError.GetAwaiter().GetResult() ?? string.Empty;
			value = value.Trim();
			return value.Length <= 2000 ? value : value.Substring(value.Length - 2000);
		}
		catch
		{
			return string.Empty;
		}
	}
}
