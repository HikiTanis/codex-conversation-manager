using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace CodexConversationMigrator;

internal static class CodexDesktopProjectRegistry
{
	private sealed class ExpectedAssignment
	{
		public string ProjectId { get; set; }

		public string TargetPath { get; set; }
	}

	public static void EnsureImportCanWrite(string codexHome)
	{
		string statePath = GetStatePath(codexHome);
		if (!File.Exists(statePath) || !IsDefaultCodexHome(codexHome))
		{
			return;
		}
		Process[] processes = Process.GetProcessesByName("ChatGPT");
		try
		{
			if (processes.Any((Process process) => !process.HasExited))
			{
				throw new InvalidOperationException("检测到 Codex 桌面端仍在运行。请完全退出 Codex（包括所有窗口）后再正式导入；否则桌面项目归属可能被正在运行的程序覆盖。");
			}
		}
		finally
		{
			foreach (Process process in processes)
			{
				process.Dispose();
			}
		}
	}

	public static DesktopProjectRegistrationResult RegisterImportedThreads(string codexHome, IEnumerable<ThreadIndexMetadata> threads)
	{
		List<ThreadIndexMetadata> mainThreads = (threads ?? Enumerable.Empty<ThreadIndexMetadata>())
			.Where(IsDesktopMainThread)
			.GroupBy((ThreadIndexMetadata item) => item.Id, StringComparer.OrdinalIgnoreCase)
			.Select((IGrouping<string, ThreadIndexMetadata> group) => group.First())
			.ToList();
		DesktopProjectRegistrationResult result = new DesktopProjectRegistrationResult
		{
			StatePath = GetStatePath(codexHome),
			ExpectedThreadCount = mainThreads.Count
		};
		if (mainThreads.Count == 0 || !File.Exists(result.StatePath))
		{
			return result;
		}
		EnsureImportCanWrite(codexHome);
		result.StateFileFound = true;
		byte[] originalBytes = File.ReadAllBytes(result.StatePath);
		Dictionary<string, object> state = DeserializeState(originalBytes, result.StatePath);
		Dictionary<string, object> localProjects = GetObjectMap(state, "local-projects");
		List<object> projectOrder = GetObjectList(state, "project-order");
		List<object> savedRoots = GetObjectList(state, "electron-saved-workspace-roots");
		Dictionary<string, object> assignments = GetObjectMap(state, "thread-project-assignments");
		List<object> projectless = GetObjectList(state, "projectless-thread-ids");
		Dictionary<string, object> workspaceHints = GetObjectMap(state, "thread-workspace-root-hints");
		Dictionary<string, object> writableRoots = GetObjectMap(state, "thread-writable-roots");
		long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		Dictionary<string, ExpectedAssignment> expected = new Dictionary<string, ExpectedAssignment>(StringComparer.OrdinalIgnoreCase);
		HashSet<string> registeredProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (IGrouping<string, ThreadIndexMetadata> cwdGroup in mainThreads.GroupBy((ThreadIndexMetadata item) => TextHelpers.CanonicalPath(item.Cwd), StringComparer.OrdinalIgnoreCase))
		{
			ThreadIndexMetadata first = cwdGroup.First();
			string targetPath = DisplayPath(first.Cwd);
			if (string.IsNullOrWhiteSpace(targetPath))
			{
				throw new InvalidDataException("无法登记 Codex 桌面项目归属：任务缺少项目路径。" + first.Id);
			}
			string projectId = FindOrCreateProject(localProjects, projectOrder, savedRoots, targetPath, now);
			registeredProjects.Add(projectId);
			foreach (ThreadIndexMetadata thread in cwdGroup)
			{
				string oldCwd = string.Empty;
				if (assignments.TryGetValue(thread.Id, out object oldAssignmentValue) && oldAssignmentValue is Dictionary<string, object> oldAssignment)
				{
					oldCwd = GetString(oldAssignment, "cwd");
				}
				if (string.IsNullOrWhiteSpace(oldCwd) && workspaceHints.TryGetValue(thread.Id, out object oldHint))
				{
					oldCwd = Convert.ToString(oldHint);
				}
				assignments[thread.Id] = new Dictionary<string, object>
				{
					{ "projectKind", "local" },
					{ "projectId", projectId },
					{ "cwd", targetPath },
					{ "pendingCoreUpdate", false }
				};
				RemoveString(projectless, thread.Id);
				workspaceHints.Remove(thread.Id);
				UpdateWritableRoots(writableRoots, thread.Id, oldCwd, targetPath);
				expected[thread.Id] = new ExpectedAssignment { ProjectId = projectId, TargetPath = targetPath };
			}
		}
		JavaScriptSerializer serializer = CctRunner.NewSerializer();
		byte[] updatedBytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(serializer.Serialize(state));
		if (!File.ReadAllBytes(result.StatePath).SequenceEqual(originalBytes))
		{
			throw new IOException("Codex 桌面项目状态在导入期间发生变化，已取消写入。请完全退出 Codex 后重试。");
		}
		result.BackupPath = CreateBackup(codexHome, originalBytes);
		AtomicReplace(result.StatePath, updatedBytes);
		try
		{
			result.VerifiedThreadCount = VerifyState(result.StatePath, expected);
			result.RegisteredProjectCount = registeredProjects.Count;
		}
		catch
		{
			AtomicReplace(result.StatePath, originalBytes);
			throw;
		}
		return result;
	}

	private static string GetStatePath(string codexHome)
	{
		return Path.Combine(Path.GetFullPath(codexHome), ".codex-global-state.json");
	}

	private static bool IsDefaultCodexHome(string codexHome)
	{
		string profile = Environment.GetEnvironmentVariable("USERPROFILE");
		if (string.IsNullOrWhiteSpace(profile))
		{
			profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		}
		return string.Equals(TextHelpers.CanonicalPath(codexHome), TextHelpers.CanonicalPath(Path.Combine(profile, ".codex")), StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsDesktopMainThread(ThreadIndexMetadata thread)
	{
		if (thread == null || thread.Archived || string.IsNullOrWhiteSpace(thread.Id) || string.IsNullOrWhiteSpace(thread.Cwd) || !string.IsNullOrWhiteSpace(thread.ParentThreadId))
		{
			return false;
		}
		string marker = (thread.ThreadSource ?? string.Empty) + " " + (thread.Source ?? string.Empty);
		return marker.IndexOf("subagent", StringComparison.OrdinalIgnoreCase) < 0;
	}

	private static Dictionary<string, object> DeserializeState(byte[] bytes, string path)
	{
		try
		{
			string json = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes).TrimStart('\uFEFF');
			if (CctRunner.NewSerializer().DeserializeObject(json) is Dictionary<string, object> state)
			{
				return state;
			}
		}
		catch (Exception ex)
		{
			throw new InvalidDataException("无法读取 Codex 桌面项目状态，未进行任何修改：" + path, ex);
		}
		throw new InvalidDataException("Codex 桌面项目状态不是有效的 JSON 对象，未进行任何修改：" + path);
	}

	private static Dictionary<string, object> GetObjectMap(Dictionary<string, object> root, string key)
	{
		if (!root.TryGetValue(key, out object value) || value == null)
		{
			Dictionary<string, object> created = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
			root[key] = created;
			return created;
		}
		if (value is Dictionary<string, object> map)
		{
			return map;
		}
		throw new InvalidDataException("Codex 桌面项目状态字段格式异常：" + key);
	}

	private static List<object> GetObjectList(Dictionary<string, object> root, string key)
	{
		if (!root.TryGetValue(key, out object value) || value == null)
		{
			List<object> created = new List<object>();
			root[key] = created;
			return created;
		}
		List<object> list;
		if (value is object[] array)
		{
			list = array.ToList();
		}
		else if (value is IEnumerable enumerable && !(value is string))
		{
			list = enumerable.Cast<object>().ToList();
		}
		else
		{
			throw new InvalidDataException("Codex 桌面项目状态字段格式异常：" + key);
		}
		root[key] = list;
		return list;
	}

	private static string FindOrCreateProject(Dictionary<string, object> localProjects, List<object> projectOrder, List<object> savedRoots, string targetPath, long now)
	{
		foreach (KeyValuePair<string, object> pair in localProjects)
		{
			if (!(pair.Value is Dictionary<string, object> project))
			{
				continue;
			}
			List<object> roots = GetObjectList(project, "rootPaths");
			if (roots.Any((object root) => string.Equals(TextHelpers.CanonicalPath(Convert.ToString(root)), TextHelpers.CanonicalPath(targetPath), StringComparison.OrdinalIgnoreCase)))
			{
				project["updatedAt"] = now;
				if (!projectOrder.Any((object value) => string.Equals(Convert.ToString(value), pair.Key, StringComparison.OrdinalIgnoreCase)))
				{
					projectOrder.Insert(0, pair.Key);
				}
				AddUniquePath(savedRoots, targetPath);
				return pair.Key;
			}
		}
		string projectId = Guid.NewGuid().ToString();
		string name = Path.GetFileName(targetPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
		if (string.IsNullOrWhiteSpace(name))
		{
			name = "迁入项目";
		}
		localProjects[projectId] = new Dictionary<string, object>
		{
			{ "id", projectId },
			{ "name", name },
			{ "rootPaths", new object[1] { targetPath } },
			{ "createdAt", now },
			{ "updatedAt", now }
		};
		projectOrder.Insert(0, projectId);
		AddUniquePath(savedRoots, targetPath);
		return projectId;
	}

	private static void AddUniquePath(List<object> values, string path)
	{
		if (!values.Any((object value) => string.Equals(TextHelpers.CanonicalPath(Convert.ToString(value)), TextHelpers.CanonicalPath(path), StringComparison.OrdinalIgnoreCase)))
		{
			values.Add(path);
		}
	}

	private static void RemoveString(List<object> values, string expected)
	{
		values.RemoveAll((object value) => string.Equals(Convert.ToString(value), expected, StringComparison.OrdinalIgnoreCase));
	}

	private static void UpdateWritableRoots(Dictionary<string, object> writableRoots, string threadId, string oldCwd, string targetPath)
	{
		List<object> roots = new List<object>();
		if (writableRoots.TryGetValue(threadId, out object value))
		{
			if (value is object[] array)
			{
				roots.AddRange(array);
			}
			else if (value is IEnumerable enumerable && !(value is string))
			{
				roots.AddRange(enumerable.Cast<object>());
			}
		}
		if (!string.IsNullOrWhiteSpace(oldCwd))
		{
			roots.RemoveAll((object root) => string.Equals(TextHelpers.CanonicalPath(Convert.ToString(root)), TextHelpers.CanonicalPath(oldCwd), StringComparison.OrdinalIgnoreCase));
		}
		AddUniquePath(roots, targetPath);
		writableRoots[threadId] = roots;
	}

	private static int VerifyState(string statePath, IDictionary<string, ExpectedAssignment> expected)
	{
		Dictionary<string, object> state = DeserializeState(File.ReadAllBytes(statePath), statePath);
		Dictionary<string, object> localProjects = GetObjectMap(state, "local-projects");
		Dictionary<string, object> assignments = GetObjectMap(state, "thread-project-assignments");
		Dictionary<string, object> writableRoots = GetObjectMap(state, "thread-writable-roots");
		List<object> projectless = GetObjectList(state, "projectless-thread-ids");
		int verified = 0;
		foreach (KeyValuePair<string, ExpectedAssignment> pair in expected)
		{
			if (!assignments.TryGetValue(pair.Key, out object assignmentValue) || !(assignmentValue is Dictionary<string, object> assignment) ||
				!string.Equals(GetString(assignment, "projectKind"), "local", StringComparison.OrdinalIgnoreCase) ||
				!string.Equals(GetString(assignment, "projectId"), pair.Value.ProjectId, StringComparison.OrdinalIgnoreCase) ||
				!string.Equals(TextHelpers.CanonicalPath(GetString(assignment, "cwd")), TextHelpers.CanonicalPath(pair.Value.TargetPath), StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidDataException("Codex 桌面项目归属验证失败：" + pair.Key);
			}
			if (!localProjects.TryGetValue(pair.Value.ProjectId, out object projectValue) || !(projectValue is Dictionary<string, object> project) ||
				!GetObjectList(project, "rootPaths").Any((object root) => string.Equals(TextHelpers.CanonicalPath(Convert.ToString(root)), TextHelpers.CanonicalPath(pair.Value.TargetPath), StringComparison.OrdinalIgnoreCase)))
			{
				throw new InvalidDataException("Codex 桌面项目目录验证失败：" + pair.Value.TargetPath);
			}
			if (!writableRoots.TryGetValue(pair.Key, out object rootsValue) || !(rootsValue is IEnumerable roots) ||
				!roots.Cast<object>().Any((object root) => string.Equals(TextHelpers.CanonicalPath(Convert.ToString(root)), TextHelpers.CanonicalPath(pair.Value.TargetPath), StringComparison.OrdinalIgnoreCase)) ||
				projectless.Any((object value) => string.Equals(Convert.ToString(value), pair.Key, StringComparison.OrdinalIgnoreCase)))
			{
				throw new InvalidDataException("Codex 桌面工作区验证失败：" + pair.Key);
			}
			verified++;
		}
		return verified;
	}

	private static string CreateBackup(string codexHome, byte[] originalBytes)
	{
		string directory = Path.Combine(Path.GetFullPath(codexHome), "conversation-migrator-state-backups", DateTime.Now.ToString("yyyy-MM-dd"));
		Directory.CreateDirectory(directory);
		string path = Path.Combine(directory, ".codex-global-state-before-import-" + DateTime.Now.ToString("HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".json");
		WriteDurably(path, originalBytes);
		if (!File.ReadAllBytes(path).SequenceEqual(originalBytes))
		{
			throw new IOException("Codex 桌面项目状态备份校验失败，未进行任何修改。");
		}
		return path;
	}

	private static void AtomicReplace(string path, byte[] contents)
	{
		string tempPath = path + ".migrator-" + Guid.NewGuid().ToString("N") + ".tmp";
		try
		{
			WriteDurably(tempPath, contents);
			File.Replace(tempPath, path, null, ignoreMetadataErrors: true);
		}
		finally
		{
			try
			{
				if (File.Exists(tempPath))
				{
					File.Delete(tempPath);
				}
			}
			catch
			{
			}
		}
	}

	private static void WriteDurably(string path, byte[] contents)
	{
		using FileStream stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.WriteThrough);
		stream.Write(contents, 0, contents.Length);
		stream.Flush(flushToDisk: true);
	}

	private static string DisplayPath(string path)
	{
		string stripped = TextHelpers.StripExtendedPrefix(path);
		if (string.IsNullOrWhiteSpace(stripped))
		{
			return string.Empty;
		}
		string fullPath = Path.GetFullPath(stripped);
		string root = Path.GetPathRoot(fullPath);
		return string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase) ? fullPath : fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
	}

	private static string GetString(Dictionary<string, object> map, string key)
	{
		return map.TryGetValue(key, out object value) && value != null ? Convert.ToString(value) : string.Empty;
	}
}
