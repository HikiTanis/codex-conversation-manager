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
}

internal sealed class LegacyCctBackupMigrationResult
{
	public int MovedToTrashCount { get; set; }

	public int RedundantDeletedCount { get; set; }
}

internal sealed class CctBackupTransaction
{
	private readonly string codexHome;

	private readonly HashSet<string> baseline;

	private bool completed;

	private CctBackupTransaction(string codexHome)
	{
		this.codexHome = Path.GetFullPath(codexHome);
		baseline = CctBackupMaintenance.Snapshot(this.codexHome);
	}

	public static CctBackupTransaction Begin(string codexHome)
	{
		return new CctBackupTransaction(codexHome);
	}

	public int CommitAndDeleteTemporaryBackups()
	{
		if (completed)
		{
			return 0;
		}
		List<string> created = CctBackupMaintenance.CreatedSince(codexHome, baseline);
		foreach (string path in created)
		{
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}
		completed = true;
		return created.Count;
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
		completed = true;
		return result;
	}
}

internal static class CctBackupMaintenance
{
	private const string Marker = ".cct-bak-";

	private static readonly Regex ThreadIdPattern = new Regex("[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	public static HashSet<string> Snapshot(string codexHome)
	{
		return new HashSet<string>(Enumerate(codexHome), StringComparer.OrdinalIgnoreCase);
	}

	public static List<string> CreatedSince(string codexHome, HashSet<string> baseline)
	{
		return Enumerate(codexHome).Where(path => baseline == null || !baseline.Contains(path)).ToList();
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
