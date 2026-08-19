using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CodexConversationMigrator;

internal static class ConversationIndexMaintenance
{
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
		ThreadIndexRemovalResult index = WinSqliteMaintenance.RemoveThreads(codexHome, ids);
		DesktopThreadRemovalResult desktop = CodexDesktopProjectRegistry.RemoveThreads(codexHome, ids);
		result.RepairedCount = index.RemovedThreadCount;
		result.IndexBackupPath = index.BackupPath;
		result.DesktopStateBackupPath = desktop.BackupPath;
		return result;
	}
}
