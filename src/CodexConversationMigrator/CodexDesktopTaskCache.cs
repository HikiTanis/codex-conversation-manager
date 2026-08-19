using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CodexConversationMigrator;

internal sealed class DesktopTaskCacheInvalidationResult
{
	public int MatchedThreadCount => MatchedThreadIds.Count;

	public int ClearedDirectoryCount => ClearedDirectories.Count;

	public List<string> MatchedThreadIds { get; } = new List<string>();

	public List<string> ClearedDirectories { get; } = new List<string>();
}

internal static class CodexDesktopTaskCache
{
	private const int ScanBufferSize = 65536;

	internal static string UserDataRootOverride { get; set; }

	public static DesktopTaskCacheInvalidationResult InvalidateThreads(string codexHome, IEnumerable<string> threadIds)
	{
		DesktopTaskCacheInvalidationResult result = new DesktopTaskCacheInvalidationResult();
		string[] ids = (threadIds ?? Enumerable.Empty<string>())
			.Where((string id) => Guid.TryParse(id, out _))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();
		if (ids.Length == 0)
		{
			return result;
		}

		bool overridden = !string.IsNullOrWhiteSpace(UserDataRootOverride);
		if (!overridden && !IsDefaultCodexHome(codexHome))
		{
			return result;
		}

		string userDataRoot = Path.GetFullPath(overridden
			? UserDataRootOverride
			: Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Codex", "web", "Codex"));
		if (!Directory.Exists(userDataRoot))
		{
			return result;
		}

		foreach (string cacheDirectory in CandidateDirectories(userDataRoot))
		{
			if (!Directory.Exists(cacheDirectory))
			{
				continue;
			}
			HashSet<string> matches = FindThreadIds(cacheDirectory, ids);
			if (matches.Count == 0)
			{
				continue;
			}
			CodexDesktopProjectRegistry.EnsureImportCanWrite(codexHome);
			Directory.Delete(cacheDirectory, recursive: true);
			if (Directory.Exists(cacheDirectory))
			{
				throw new IOException("Codex 桌面任务列表缓存清理校验失败：" + Environment.NewLine + cacheDirectory);
			}
			result.ClearedDirectories.Add(cacheDirectory);
			foreach (string id in matches)
			{
				if (!result.MatchedThreadIds.Contains(id, StringComparer.OrdinalIgnoreCase))
				{
					result.MatchedThreadIds.Add(id);
				}
			}
		}
		return result;
	}

	private static IEnumerable<string> CandidateDirectories(string userDataRoot)
	{
		string[] relativePaths =
		{
			Path.Combine("Default", "Cache", "Cache_Data"),
			Path.Combine("Default", "Partitions", "codex-browser-app", "Cache", "Cache_Data"),
			Path.Combine("codex-browser-app", "Cache", "Cache_Data")
		};
		foreach (string relativePath in relativePaths)
		{
			string path = Path.GetFullPath(Path.Combine(userDataRoot, relativePath));
			if (!TextHelpers.IsWithin(path, userDataRoot) || string.Equals(TextHelpers.CanonicalPath(path), TextHelpers.CanonicalPath(userDataRoot), StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidOperationException("Codex 桌面缓存路径安全校验失败：" + Environment.NewLine + path);
			}
			yield return path;
		}
	}

	private static HashSet<string> FindThreadIds(string directory, IEnumerable<string> threadIds)
	{
		Dictionary<string, byte[]> remaining = threadIds.ToDictionary((string id) => id, (string id) => Encoding.ASCII.GetBytes(id), StringComparer.OrdinalIgnoreCase);
		foreach (string path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
		{
			if (remaining.Count == 0)
			{
				break;
			}
			try
			{
				FindPatterns(path, remaining);
			}
			catch (FileNotFoundException)
			{
			}
			catch (DirectoryNotFoundException)
			{
			}
		}
		HashSet<string> all = new HashSet<string>(threadIds, StringComparer.OrdinalIgnoreCase);
		all.ExceptWith(remaining.Keys);
		return all;
	}

	private static void FindPatterns(string path, IDictionary<string, byte[]> remaining)
	{
		int overlap = remaining.Values.Select((byte[] pattern) => pattern.Length).DefaultIfEmpty(1).Max() - 1;
		byte[] buffer = new byte[ScanBufferSize + overlap];
		int carry = 0;
		using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, ScanBufferSize, FileOptions.SequentialScan);
		while (remaining.Count > 0)
		{
			int read = stream.Read(buffer, carry, ScanBufferSize);
			if (read == 0)
			{
				break;
			}
			int count = carry + read;
			foreach (KeyValuePair<string, byte[]> pair in remaining.ToArray())
			{
				if (IndexOf(buffer, count, pair.Value) >= 0)
				{
					remaining.Remove(pair.Key);
				}
			}
			carry = Math.Min(overlap, count);
			if (carry > 0)
			{
				Buffer.BlockCopy(buffer, count - carry, buffer, 0, carry);
			}
		}
	}

	private static int IndexOf(byte[] buffer, int count, byte[] pattern)
	{
		if (pattern.Length == 0 || pattern.Length > count)
		{
			return -1;
		}
		int last = count - pattern.Length;
		for (int i = 0; i <= last; i++)
		{
			int j = 0;
			while (j < pattern.Length && buffer[i + j] == pattern[j])
			{
				j++;
			}
			if (j == pattern.Length)
			{
				return i;
			}
		}
		return -1;
	}

	private static bool IsDefaultCodexHome(string codexHome)
	{
		if (string.IsNullOrWhiteSpace(codexHome))
		{
			return false;
		}
		string profile = Environment.GetEnvironmentVariable("USERPROFILE");
		if (string.IsNullOrWhiteSpace(profile))
		{
			profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		}
		return string.Equals(TextHelpers.CanonicalPath(codexHome), TextHelpers.CanonicalPath(Path.Combine(profile, ".codex")), StringComparison.OrdinalIgnoreCase);
	}
}
