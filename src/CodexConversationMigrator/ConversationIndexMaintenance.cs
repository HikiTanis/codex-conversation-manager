using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CodexConversationMigrator;

internal static class ConversationIndexMaintenance
{
	private const string RepairLedgerName = "sidebar-remnant-repairs.json";
	private static readonly Regex MissingThreadIdPattern = new Regex(@"(?:no rollout found for thread id|thread not loaded:)\s*(?<id>[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
	private static readonly Regex ConversationIdPattern = new Regex(@"conversationId=(?<id>[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

	internal static string LogRootOverride { get; set; }

	public static List<DbThread> FindOrphanedThreads(string codexHome)
	{
		string databasePath = WinSqliteMaintenance.FindActiveDatabase(codexHome);
		if (string.IsNullOrWhiteSpace(databasePath) || !File.Exists(databasePath))
		{
			return new List<DbThread>();
		}
		string sessionsRoot = Path.GetFullPath(Path.Combine(codexHome, "sessions"));
		string archivedRoot = Path.GetFullPath(Path.Combine(codexHome, "archived_sessions"));
		List<DbThread> result = new List<DbThread>();
		foreach (DbThread thread in WinSqliteReader.ReadThreads(databasePath))
		{
			if (thread == null || string.IsNullOrWhiteSpace(thread.Id) || string.IsNullOrWhiteSpace(thread.RolloutPath))
			{
				continue;
			}
			try
			{
				string path = Path.GetFullPath(TextHelpers.StripExtendedPrefix(thread.RolloutPath));
				if ((TextHelpers.IsWithin(path, sessionsRoot) || TextHelpers.IsWithin(path, archivedRoot)) && !File.Exists(path))
				{
					result.Add(thread);
				}
			}
			catch
			{
			}
		}
		return result;
	}

	public static OrphanIndexRepairResult RepairSelectedOrphans(string codexHome, IEnumerable<string> confirmedThreadIds)
	{
		HashSet<string> confirmed = new HashSet<string>((confirmedThreadIds ?? Enumerable.Empty<string>()).Where((string id) => !string.IsNullOrWhiteSpace(id)), StringComparer.OrdinalIgnoreCase);
		List<DbThread> orphans = FindOrphanedThreads(codexHome)
			.Where((DbThread thread) => confirmed.Contains(thread.Id))
			.ToList();
		OrphanIndexRepairResult result = new OrphanIndexRepairResult
		{
			DetectedCount = orphans.Count
		};
		if (confirmed.Count == 0 || orphans.Count == 0)
		{
			return result;
		}
		if (CodexDesktopProjectRegistry.IsDesktopRunning(codexHome))
		{
			result.DesktopRunning = true;
			return result;
		}
		string[] ids = orphans.Select((DbThread thread) => thread.Id).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
		CodexDesktopProjectRegistry.EnsureImportCanWrite(codexHome);
		foreach (DbThread orphan in orphans)
		{
			EnsureNoLiveDescendants(codexHome, orphan.Id);
			CompleteOfficialDeletion(codexHome, orphan);
			WriteRepairLedger(codexHome, new string[1] { orphan.Id });
			result.RepairedCount++;
		}
		ThreadIndexRemovalResult index = WinSqliteMaintenance.RemoveThreads(codexHome, ids);
		DesktopThreadRemovalResult desktop = CodexDesktopProjectRegistry.RemoveThreads(codexHome, ids);
		result.IndexBackupPath = index.BackupPath;
		result.DesktopStateBackupPath = desktop.BackupPath;
		return result;
	}

	public static List<DbThread> FindDeletedSidebarRemnants(string codexHome)
	{
		string databasePath = WinSqliteMaintenance.FindActiveDatabase(codexHome);
		if (string.IsNullOrWhiteSpace(databasePath) || !File.Exists(databasePath))
		{
			return new List<DbThread>();
		}
		string backupPath = FindLatestIndexBackup(codexHome);
		if (string.IsNullOrWhiteSpace(backupPath))
		{
			return new List<DbThread>();
		}
		HashSet<string> logConfirmedIds = FindLogConfirmedMissingThreadIds(backupPath);
		if (logConfirmedIds.Count == 0)
		{
			return new List<DbThread>();
		}
		HashSet<string> activeIds = new HashSet<string>(WinSqliteReader.ReadThreads(databasePath).Select((DbThread thread) => thread.Id), StringComparer.OrdinalIgnoreCase);
		HashSet<string> completedIds = ReadRepairLedger(codexHome);
		return WinSqliteReader.ReadThreads(backupPath)
			.Where((DbThread thread) => thread != null && !string.IsNullOrWhiteSpace(thread.Id) && logConfirmedIds.Contains(thread.Id) && !activeIds.Contains(thread.Id) && !completedIds.Contains(thread.Id) && IsMissingSessionPath(codexHome, thread.RolloutPath))
			.GroupBy((DbThread thread) => thread.Id, StringComparer.OrdinalIgnoreCase)
			.Select((IGrouping<string, DbThread> group) => group.First())
			.OrderByDescending((DbThread thread) => thread.UpdatedAtMilliseconds)
			.ToList();
	}

	public static OrphanIndexRepairResult RepairDeletedSidebarRemnants(string codexHome, IEnumerable<string> confirmedThreadIds)
	{
		HashSet<string> confirmed = new HashSet<string>((confirmedThreadIds ?? Enumerable.Empty<string>()).Where((string id) => !string.IsNullOrWhiteSpace(id)), StringComparer.OrdinalIgnoreCase);
		List<DbThread> remnants = FindDeletedSidebarRemnants(codexHome)
			.Where((DbThread thread) => confirmed.Contains(thread.Id))
			.ToList();
		OrphanIndexRepairResult result = new OrphanIndexRepairResult
		{
			DetectedCount = remnants.Count,
			IndexBackupPath = FindLatestIndexBackup(codexHome)
		};
		if (confirmed.Count == 0 || remnants.Count == 0)
		{
			return result;
		}
		if (CodexDesktopProjectRegistry.IsDesktopRunning(codexHome))
		{
			result.DesktopRunning = true;
			return result;
		}
		CodexDesktopProjectRegistry.EnsureImportCanWrite(codexHome);
		List<string> completed = new List<string>();
		foreach (DbThread remnant in remnants)
		{
			EnsureNoLiveDescendants(codexHome, remnant.Id);
			CompleteOfficialDeletion(codexHome, remnant);
			completed.Add(remnant.Id);
			WriteRepairLedger(codexHome, new string[1] { remnant.Id });
			result.RepairedCount++;
		}
		ThreadIndexRemovalResult index = WinSqliteMaintenance.RemoveThreads(codexHome, completed);
		DesktopThreadRemovalResult desktop = CodexDesktopProjectRegistry.RemoveThreads(codexHome, completed);
		if (!string.IsNullOrWhiteSpace(index.BackupPath))
		{
			result.IndexBackupPath = index.BackupPath;
		}
		result.DesktopStateBackupPath = desktop.BackupPath;
		return result;
	}

	private static HashSet<string> FindLogConfirmedMissingThreadIds(string backupPath)
	{
		HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		string logRoot = string.IsNullOrWhiteSpace(LogRootOverride)
			? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Codex", "Logs")
			: Path.GetFullPath(LogRootOverride);
		if (!Directory.Exists(logRoot))
		{
			return result;
		}
		DateTime earliest = File.GetLastWriteTimeUtc(backupPath).AddMinutes(-10.0);
		IEnumerable<string> logs;
		try
		{
			logs = Directory.EnumerateFiles(logRoot, "*.log", SearchOption.AllDirectories)
				.Where((string path) => File.GetLastWriteTimeUtc(path) >= earliest)
				.OrderByDescending(File.GetLastWriteTimeUtc)
				.Take(120)
				.ToArray();
		}
		catch
		{
			return result;
		}
		foreach (string logPath in logs)
		{
			try
			{
				using (FileStream stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
				using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
				{
					string line;
					while ((line = reader.ReadLine()) != null)
					{
						bool rolloutFailure = line.IndexOf("rollout_not_found", StringComparison.OrdinalIgnoreCase) >= 0 ||
							line.IndexOf("no rollout found for thread id", StringComparison.OrdinalIgnoreCase) >= 0 ||
							line.IndexOf("thread not loaded:", StringComparison.OrdinalIgnoreCase) >= 0 ||
							(line.IndexOf("failed to resolve rollout path", StringComparison.OrdinalIgnoreCase) >= 0 && line.IndexOf("file does not exist", StringComparison.OrdinalIgnoreCase) >= 0);
						if (!rolloutFailure)
						{
							continue;
						}
						foreach (Match match in MissingThreadIdPattern.Matches(line))
						{
							result.Add(match.Groups["id"].Value);
						}
						if (line.IndexOf("rollout_not_found", StringComparison.OrdinalIgnoreCase) >= 0)
						{
							Match conversation = ConversationIdPattern.Match(line);
							if (conversation.Success)
							{
								result.Add(conversation.Groups["id"].Value);
							}
						}
					}
				}
			}
			catch
			{
			}
		}
		return result;
	}

	private static void CompleteOfficialDeletion(string codexHome, DbThread thread)
	{
		string temporaryRollout = ValidateMissingSessionPath(codexHome, thread);
		string parent = Path.GetDirectoryName(temporaryRollout);
		if (string.IsNullOrWhiteSpace(parent))
		{
			throw new InvalidDataException("失效会话路径没有有效的上级目录。");
		}
		Directory.CreateDirectory(parent);
		Dictionary<string, object> sessionMeta = new Dictionary<string, object>
		{
			{ "timestamp", DateTimeOffset.UtcNow.ToString("o") },
			{ "type", "session_meta" },
			{ "payload", new Dictionary<string, object>
				{
					{ "id", thread.Id },
					{ "timestamp", DateTimeOffset.UtcNow.ToString("o") },
					{ "cwd", string.IsNullOrWhiteSpace(thread.Cwd) ? codexHome : thread.Cwd },
					{ "originator", "codex-desktop" },
					{ "cli_version", "sidebar-remnant-repair" },
					{ "source", "vscode" },
					{ "model_provider", "openai" }
				}
			}
		};
		bool created = false;
		try
		{
			using (FileStream stream = new FileStream(temporaryRollout, FileMode.CreateNew, FileAccess.Write, FileShare.None))
			using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
			{
				writer.WriteLine(CctRunner.NewSerializer().Serialize(sessionMeta));
			}
			created = true;
			CodexAppServerThreadDeletion.DeleteThread(codexHome, thread.Id);
		}
		finally
		{
			if (created && File.Exists(temporaryRollout))
			{
				File.Delete(temporaryRollout);
			}
		}
	}

	private static void EnsureNoLiveDescendants(string codexHome, string rootThreadId)
	{
		string databasePath = WinSqliteMaintenance.FindActiveDatabase(codexHome);
		if (string.IsNullOrWhiteSpace(databasePath) || !File.Exists(databasePath) || string.IsNullOrWhiteSpace(rootThreadId))
		{
			return;
		}
		List<DbThread> threads = WinSqliteReader.ReadThreads(databasePath);
		Dictionary<string, string> parentByChild = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		Dictionary<string, DbThread> byId = threads.Where((DbThread item) => item != null && !string.IsNullOrWhiteSpace(item.Id)).GroupBy((DbThread item) => item.Id, StringComparer.OrdinalIgnoreCase).ToDictionary((IGrouping<string, DbThread> group) => group.Key, (IGrouping<string, DbThread> group) => group.First(), StringComparer.OrdinalIgnoreCase);
		foreach (DbThread thread in byId.Values)
		{
			string parentId = thread.ParentThreadId;
			string rolloutPath = TextHelpers.StripExtendedPrefix(thread.RolloutPath);
			if (string.IsNullOrWhiteSpace(parentId) && !string.IsNullOrWhiteSpace(rolloutPath) && File.Exists(rolloutPath))
			{
				try
				{
					parentId = TargetedThreadIndexer.ReadMetadataForIndexing(rolloutPath, thread.Title, thread.Title).ParentThreadId;
				}
				catch
				{
				}
			}
			if (!string.IsNullOrWhiteSpace(parentId))
			{
				parentByChild[thread.Id] = parentId;
			}
		}
		HashSet<string> descendants = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		Queue<string> pending = new Queue<string>();
		pending.Enqueue(rootThreadId);
		while (pending.Count > 0)
		{
			string parentId = pending.Dequeue();
			foreach (KeyValuePair<string, string> relation in parentByChild.Where((KeyValuePair<string, string> relation) => string.Equals(relation.Value, parentId, StringComparison.OrdinalIgnoreCase)).ToArray())
			{
				if (descendants.Add(relation.Key))
				{
					pending.Enqueue(relation.Key);
				}
			}
		}
		List<DbThread> liveDescendants = descendants.Where(byId.ContainsKey).Select((string id) => byId[id]).Where((DbThread thread) => !string.IsNullOrWhiteSpace(thread.RolloutPath) && File.Exists(TextHelpers.StripExtendedPrefix(thread.RolloutPath))).ToList();
		if (liveDescendants.Count > 0)
		{
			throw new InvalidOperationException("该失效主对话仍关联 " + liveDescendants.Count + " 个存在本地文件的子代理。为防止 Codex 官方删除级联清除这些记录，本次侧边栏修复已停止。请先在主界面将关联子代理移入软件回收站或另行备份。\n\n" + string.Join("\n", liveDescendants.Take(5).Select((DbThread thread) => thread.Id)));
		}
	}

	private static string ValidateMissingSessionPath(string codexHome, DbThread thread)
	{
		if (thread == null || string.IsNullOrWhiteSpace(thread.Id) || string.IsNullOrWhiteSpace(thread.RolloutPath))
		{
			throw new InvalidDataException("失效会话缺少 Thread ID 或原始路径。");
		}
		if (!Guid.TryParse(thread.Id, out _))
		{
			throw new InvalidDataException("失效会话的 Thread ID 无效：" + thread.Id);
		}
		string path = Path.GetFullPath(TextHelpers.StripExtendedPrefix(thread.RolloutPath));
		string sessionsRoot = Path.GetFullPath(Path.Combine(codexHome, "sessions"));
		string archivedRoot = Path.GetFullPath(Path.Combine(codexHome, "archived_sessions"));
		if (!TextHelpers.IsWithin(path, sessionsRoot) && !TextHelpers.IsWithin(path, archivedRoot))
		{
			throw new InvalidDataException("失效会话路径不在 Codex 会话目录内：\n" + path);
		}
		if (File.Exists(path))
		{
			throw new IOException("失效会话路径已被其他文件占用，未执行修复：\n" + path);
		}
		return path;
	}

	private static bool IsMissingSessionPath(string codexHome, string rolloutPath)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(rolloutPath))
			{
				return false;
			}
			string path = Path.GetFullPath(TextHelpers.StripExtendedPrefix(rolloutPath));
			string sessionsRoot = Path.GetFullPath(Path.Combine(codexHome, "sessions"));
			string archivedRoot = Path.GetFullPath(Path.Combine(codexHome, "archived_sessions"));
			return (TextHelpers.IsWithin(path, sessionsRoot) || TextHelpers.IsWithin(path, archivedRoot)) && !File.Exists(path);
		}
		catch
		{
			return false;
		}
	}

	private static string FindLatestIndexBackup(string codexHome)
	{
		string directory = Path.Combine(Path.GetFullPath(codexHome), "conversation-migrator-index-backups");
		if (!Directory.Exists(directory))
		{
			return string.Empty;
		}
		foreach (string path in Directory.EnumerateFiles(directory, "*.sqlite", SearchOption.TopDirectoryOnly).OrderByDescending(File.GetLastWriteTimeUtc))
		{
			try
			{
				WinSqliteReader.ReadThreads(path);
				return path;
			}
			catch
			{
			}
		}
		return string.Empty;
	}

	private static HashSet<string> ReadRepairLedger(string codexHome)
	{
		HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		string path = RepairLedgerPath(codexHome);
		if (!File.Exists(path))
		{
			return result;
		}
		try
		{
			Dictionary<string, object> root = CctRunner.NewSerializer().DeserializeObject(File.ReadAllText(path, Encoding.UTF8)) as Dictionary<string, object>;
			if (root != null && root.TryGetValue("thread_ids", out object value) && value is object[] ids)
			{
				foreach (object id in ids)
				{
					string text = Convert.ToString(id);
					if (!string.IsNullOrWhiteSpace(text))
					{
						result.Add(text);
					}
				}
			}
		}
		catch
		{
		}
		return result;
	}

	private static void WriteRepairLedger(string codexHome, IEnumerable<string> threadIds)
	{
		HashSet<string> ids = ReadRepairLedger(codexHome);
		ids.UnionWith((threadIds ?? Enumerable.Empty<string>()).Where((string id) => !string.IsNullOrWhiteSpace(id)));
		string path = RepairLedgerPath(codexHome);
		Directory.CreateDirectory(Path.GetDirectoryName(path));
		string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
		try
		{
			Dictionary<string, object> root = new Dictionary<string, object>
			{
				{ "schema", 1 },
				{ "updated_at", DateTimeOffset.Now.ToString("o") },
				{ "thread_ids", ids.OrderBy((string id) => id, StringComparer.OrdinalIgnoreCase).Cast<object>().ToArray() }
			};
			File.WriteAllText(temporary, CctRunner.NewSerializer().Serialize(root), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			if (File.Exists(path))
			{
				File.Replace(temporary, path, null, ignoreMetadataErrors: true);
			}
			else
			{
				File.Move(temporary, path);
			}
		}
		finally
		{
			if (File.Exists(temporary))
			{
				File.Delete(temporary);
			}
		}
	}

	private static string RepairLedgerPath(string codexHome)
	{
		return Path.Combine(Path.GetFullPath(codexHome), "conversation-migrator-index-backups", RepairLedgerName);
	}
}
