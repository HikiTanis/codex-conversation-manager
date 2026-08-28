using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CodexConversationMigrator;

internal sealed class CctBackupRollbackResult
{
	public int RestoredCount { get; set; }

	public int DeletedCount { get; set; }

	public int RemovedImportedCount { get; set; }
}

internal sealed class LegacyCctBackupMigrationResult
{
	public int MovedToTrashCount { get; set; }

	public int RedundantDeletedCount { get; set; }
}

internal sealed class CctBackupTransaction
{
	internal static Func<string, bool> CommitCleanupFailureForTest { get; set; }

	private readonly string codexHome;

	private readonly HashSet<string> baseline;

	private readonly HashSet<string> baselineSessionFiles;

	private readonly HashSet<string> trackedImportedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private bool completed;

	private CctBackupTransaction(string codexHome)
	{
		this.codexHome = Path.GetFullPath(codexHome);
		baseline = CctBackupMaintenance.Snapshot(this.codexHome);
		baselineSessionFiles = TargetedThreadIndexer.SnapshotSessionFiles(this.codexHome);
	}

	public static CctBackupTransaction Begin(string codexHome)
	{
		return new CctBackupTransaction(codexHome);
	}

	public void TrackImportedSessionFiles(IEnumerable<string> candidates, IEnumerable<string> plannedThreadIds)
	{
		if (completed)
		{
			throw new InvalidOperationException("导入事务已经结束。");
		}
		HashSet<string> ids = new HashSet<string>((plannedThreadIds ?? Enumerable.Empty<string>())
			.Where(id => Guid.TryParse(id, out _)), StringComparer.OrdinalIgnoreCase);
		if (ids.Count == 0)
		{
			return;
		}
		HashSet<string> current = TargetedThreadIndexer.SnapshotSessionFiles(codexHome);
		foreach (string candidate in candidates ?? Enumerable.Empty<string>())
		{
			string fullPath = Path.GetFullPath(candidate);
			if (!current.Contains(fullPath) || baselineSessionFiles.Contains(fullPath))
			{
				continue;
			}
			string fileName = Path.GetFileName(fullPath);
			bool belongsToImport = ids.Any(id => fileName.IndexOf(id, StringComparison.OrdinalIgnoreCase) >= 0);
			if (!belongsToImport && ConversationLineage.TryReadPayload(fullPath, out Dictionary<string, object> payload))
			{
				string currentId = ConversationLineage.ResolveCurrentThreadId(payload, string.Empty);
				string originId = ConversationLineage.ResolveOriginThreadId(payload, currentId);
				belongsToImport = ids.Contains(currentId) || ids.Contains(originId);
			}
			if (belongsToImport)
			{
				trackedImportedFiles.Add(fullPath);
			}
		}
	}

	public int CommitAndDeleteTemporaryBackups()
	{
		if (completed)
		{
			return 0;
		}
		List<string> created = CctBackupMaintenance.CreatedSince(codexHome, baseline);
		if (created.Count == 0)
		{
			completed = true;
			return 0;
		}

		string cleanupRoot = CctBackupMaintenance.TransactionCleanupRoot(codexHome);
		string stagingDirectory = Path.Combine(cleanupRoot, Guid.NewGuid().ToString("N"));
		List<KeyValuePair<string, string>> staged = new List<KeyValuePair<string, string>>();
		Directory.CreateDirectory(stagingDirectory);

		// The import and indexes are already valid when this method is called. Once the isolated
		// staging directory exists, cleanup failures must not roll the completed import back.
		completed = true;
		for (int i = 0; i < created.Count; i++)
		{
			string source = created[i];
			if (!File.Exists(source))
			{
				continue;
			}
			string destination = Path.Combine(stagingDirectory, "snapshot-" + i.ToString("D4") + ".jsonl");
			try
			{
				File.Move(source, destination);
				staged.Add(new KeyValuePair<string, string>(source, destination));
			}
			catch
			{
				// A locked snapshot remains in place and can be organized on the next refresh.
			}
		}

		foreach (KeyValuePair<string, string> item in staged)
		{
			try
			{
				if (CommitCleanupFailureForTest?.Invoke(item.Value) == true)
				{
					throw new IOException("Simulated post-commit cleanup failure.");
				}
				if (File.Exists(item.Value))
				{
					File.Delete(item.Value);
				}
			}
			catch
			{
				// The staged snapshot is intentionally left outside sessions for later manual cleanup.
			}
		}
		CctBackupMaintenance.DeleteEmptyTransactionCleanupDirectories(stagingDirectory);
		return staged.Count;
	}

	public CctBackupRollbackResult RollbackAndDeleteTemporaryBackups()
	{
		CctBackupRollbackResult result = new CctBackupRollbackResult();
		if (completed)
		{
			return result;
		}
		List<IGrouping<string, string>> groups = CctBackupMaintenance.CreatedSince(codexHome, baseline)
			.GroupBy(CctBackupMaintenance.ActivePathForBackup, StringComparer.OrdinalIgnoreCase)
			.ToList();
		foreach (IGrouping<string, string> group in groups)
		{
			string original = group.OrderBy(CctBackupMaintenance.BackupOrdinal).First();
			Directory.CreateDirectory(Path.GetDirectoryName(group.Key));
			File.Copy(original, group.Key, overwrite: true);
			result.RestoredCount++;
			foreach (string backup in group)
			{
				if (File.Exists(backup))
				{
					File.Delete(backup);
					result.DeletedCount++;
				}
			}
		}
		foreach (string path in trackedImportedFiles)
		{
			if (File.Exists(path) && !baselineSessionFiles.Contains(path))
			{
				File.Delete(path);
				result.RemovedImportedCount++;
			}
		}
		completed = true;
		return result;
	}
}

internal static class CctBackupMaintenance
{
	private const string Marker = ".cct-bak-";

	private const string TransactionCleanupDirectoryName = ".conversation-migrator-transaction-cleanup";

	private static readonly Regex ThreadIdPattern = new Regex("[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	public static HashSet<string> Snapshot(string codexHome)
	{
		return new HashSet<string>(Enumerate(codexHome), StringComparer.OrdinalIgnoreCase);
	}

	public static List<string> CreatedSince(string codexHome, HashSet<string> baseline)
	{
		return Enumerate(codexHome).Where(path => baseline == null || !baseline.Contains(path)).ToList();
	}

	internal static string TransactionCleanupRoot(string codexHome)
	{
		return Path.Combine(Path.GetFullPath(codexHome), TransactionCleanupDirectoryName);
	}

	internal static void DeleteEmptyTransactionCleanupDirectories(string stagingDirectory)
	{
		try
		{
			if (!string.IsNullOrWhiteSpace(stagingDirectory) && Directory.Exists(stagingDirectory) && !Directory.EnumerateFileSystemEntries(stagingDirectory).Any())
			{
				Directory.Delete(stagingDirectory);
			}
			string root = string.IsNullOrWhiteSpace(stagingDirectory) ? string.Empty : Path.GetDirectoryName(stagingDirectory);
			if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root) && !Directory.EnumerateFileSystemEntries(root).Any())
			{
				Directory.Delete(root);
			}
		}
		catch
		{
			// Cleanup directories contain no live session files and may safely remain when locked.
		}
	}

	public static string ActivePathForBackup(string backupPath)
	{
		int marker = backupPath.LastIndexOf(Marker, StringComparison.OrdinalIgnoreCase);
		if (marker <= 0)
		{
			throw new InvalidDataException("无法识别 cct 临时备份路径：" + backupPath);
		}
		return backupPath.Substring(0, marker);
	}

	public static long BackupOrdinal(string backupPath)
	{
		int marker = backupPath.LastIndexOf(Marker, StringComparison.OrdinalIgnoreCase);
		long value;
		return marker >= 0 && long.TryParse(backupPath.Substring(marker + Marker.Length), out value) ? value : long.MaxValue;
	}

	public static int DeleteForSession(SessionInfo session)
	{
		if (session == null)
		{
			return 0;
		}
		return DeleteForThread(CodexCatalog.ResolveCodexHome(), session.ThreadId, session.SessionPath);
	}

	public static int DeleteForThread(string codexHome, string threadId, string activePath)
	{
		string fullActive = string.IsNullOrWhiteSpace(activePath) ? string.Empty : Path.GetFullPath(TextHelpers.StripExtendedPrefix(activePath));
		List<string> matches = Enumerate(codexHome).Where(delegate(string path)
		{
			if (!string.IsNullOrWhiteSpace(fullActive) && string.Equals(ActivePathForBackup(path), fullActive, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			return !string.IsNullOrWhiteSpace(threadId) && Path.GetFileName(path).IndexOf(threadId, StringComparison.OrdinalIgnoreCase) >= 0;
		}).ToList();
		foreach (string path in matches)
		{
			File.Delete(path);
		}
		return matches.Count;
	}

	public static LegacyCctBackupMigrationResult MoveLegacyBackupsToTrash(string codexHome)
	{
		LegacyCctBackupMigrationResult result = new LegacyCctBackupMigrationResult();
		List<IGrouping<string, string>> groups = Enumerate(codexHome)
			.GroupBy(ActivePathForBackup, StringComparer.OrdinalIgnoreCase)
			.ToList();
		foreach (IGrouping<string, string> group in groups)
		{
			List<string> ordered = group.OrderBy(BackupOrdinal).ToList();
			if (ordered.Count == 0)
			{
				continue;
			}
			MoveOneLegacyBackupToTrash(ordered[0], group.Key);
			result.MovedToTrashCount++;
			foreach (string redundant in ordered.Skip(1))
			{
				if (File.Exists(redundant))
				{
					File.Delete(redundant);
					result.RedundantDeletedCount++;
				}
			}
		}
		return result;
	}

	private static List<string> Enumerate(string codexHome)
	{
		List<string> result = new List<string>();
		if (string.IsNullOrWhiteSpace(codexHome))
		{
			return result;
		}
		foreach (string folder in new string[2] { "sessions", "archived_sessions" })
		{
			string root = Path.Combine(Path.GetFullPath(codexHome), folder);
			if (!Directory.Exists(root))
			{
				continue;
			}
			result.AddRange(Directory.EnumerateFiles(root, "*.cct-bak-*", SearchOption.AllDirectories).Select(Path.GetFullPath));
		}
		return result;
	}

	private static void MoveOneLegacyBackupToTrash(string sourcePath, string activePath)
	{
		string trashRoot = ConversationStorage.TrashRoot;
		string datedDirectory = Path.Combine(trashRoot, DateTime.Now.ToString("yyyy-MM-dd"));
		Directory.CreateDirectory(datedDirectory);
		string destination = UniquePath(datedDirectory, DateTime.Now.ToString("HHmmssfff") + "-legacy-snapshot-" + Path.GetFileName(activePath));
		string sidecar = destination + ".delete-info.json";
		File.Move(sourcePath, destination);
		try
		{
			Dictionary<string, object> payload;
			ConversationLineage.TryReadPayload(destination, out payload);
			string fallbackId = ThreadIdPattern.Match(Path.GetFileName(activePath)).Value;
			string threadId = ConversationLineage.ResolveCurrentThreadId(payload, fallbackId);
			string cwd = GetString(payload, "cwd");
			string title = ConversationReader.ReadTitleCandidate(destination);
			if (string.IsNullOrWhiteSpace(title))
			{
				title = threadId;
			}
			Dictionary<string, object> metadata = new Dictionary<string, object>
			{
				{ "schema", 2 },
				{ "thread_id", threadId ?? string.Empty },
				{ "title", "旧版安全快照 · " + title },
				{ "original_path", activePath },
				{ "backup_path", destination },
				{ "project_path", cwd ?? string.Empty },
				{ "size_bytes", new FileInfo(destination).Length },
				{ "deleted_at", DateTimeOffset.Now.ToString("o") },
				{ "legacy_cct_snapshot", true },
				{ "snapshot_ordinal", BackupOrdinal(sourcePath).ToString() }
			};
			File.WriteAllText(sidecar, CctRunner.NewSerializer().Serialize(metadata), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		}
		catch
		{
			if (File.Exists(sidecar))
			{
				File.Delete(sidecar);
			}
			if (File.Exists(destination) && !File.Exists(sourcePath))
			{
				File.Move(destination, sourcePath);
			}
			throw;
		}
	}

	private static string UniquePath(string directory, string fileName)
	{
		string candidate = Path.Combine(directory, fileName);
		int suffix = 2;
		while (File.Exists(candidate) || File.Exists(candidate + ".delete-info.json"))
		{
			candidate = Path.Combine(directory, Path.GetFileNameWithoutExtension(fileName) + "-" + suffix + Path.GetExtension(fileName));
			suffix++;
		}
		return candidate;
	}

	private static string GetString(Dictionary<string, object> payload, string key)
	{
		if (payload != null && payload.TryGetValue(key, out object value) && value != null)
		{
			return Convert.ToString(value);
		}
		return string.Empty;
	}
}
