using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace CodexConversationMigrator;

internal static class ConversationStorage
{
	private const string TrashDirectoryName = "conversation-migrator-trash";

	public static string TrashRoot => Path.GetFullPath(Path.Combine(CodexCatalog.ResolveCodexHome(), TrashDirectoryName));

	public static DeletedSessionResult MoveToTrash(SessionInfo session)
	{
		return MoveToTrash(session, ResolveProjectPath(session, null));
	}

	public static DeletedSessionResult MoveToTrash(SessionInfo session, string projectPath)
	{
		string sourcePath = ResolveValidatedSessionPath(session);
		CctBackupMaintenance.DeleteForThread(CodexCatalog.ResolveCodexHome(), session.ThreadId, sourcePath);
		string trashRoot = TrashRoot;
		string datedDirectory = Path.Combine(trashRoot, DateTime.Now.ToString("yyyy-MM-dd"));
		Directory.CreateDirectory(datedDirectory);
		string fileName = DateTime.Now.ToString("HHmmssfff") + "-" + Path.GetFileName(sourcePath);
		string backupPath = UniqueFilePath(datedDirectory, fileName);
		if (!TextHelpers.IsWithin(backupPath, trashRoot))
		{
			throw new InvalidOperationException("安全校验失败：备份目录越界。");
		}
		File.Move(sourcePath, backupPath);
		try
		{
			Dictionary<string, object> metadata = new Dictionary<string, object>
			{
				{ "schema", 2 },
				{ "thread_id", session.ThreadId ?? string.Empty },
				{ "title", session.DisplayTitle },
				{ "original_path", sourcePath },
				{ "backup_path", backupPath },
				{ "project_path", NormalizeOptionalPath(projectPath) },
				{ "size_bytes", new FileInfo(backupPath).Length },
				{ "deleted_at", DateTimeOffset.Now.ToString("o") }
			};
			WriteMetadata(backupPath + ".delete-info.json", metadata);
		}
		catch (Exception metadataError)
		{
			try
			{
				if (File.Exists(backupPath) && !File.Exists(sourcePath))
				{
					File.Move(backupPath, sourcePath);
				}
			}
			catch (Exception rollbackError)
			{
				throw new AggregateException("会话已移出原位置，但删除信息写入和自动回滚都失败。请立即检查：\n" + backupPath, metadataError, rollbackError);
			}
			throw new InvalidOperationException("无法写入回收站删除信息；会话已自动恢复到原位置。", metadataError);
		}
		return new DeletedSessionResult
		{
			OriginalPath = sourcePath,
			BackupPath = backupPath,
			PermanentlyDeleted = false
		};
	}

	public static DeletedSessionResult DeletePermanently(SessionInfo session)
	{
		string sourcePath = ResolveValidatedSessionPath(session);
		CctBackupMaintenance.DeleteForThread(CodexCatalog.ResolveCodexHome(), session.ThreadId, sourcePath);
		File.Delete(sourcePath);
		return new DeletedSessionResult
		{
			OriginalPath = sourcePath,
			BackupPath = string.Empty,
			PermanentlyDeleted = true
		};
	}

	public static List<TrashSessionInfo> ReadTrash()
	{
		string trashRoot = TrashRoot;
		List<TrashSessionInfo> items = new List<TrashSessionInfo>();
		if (!Directory.Exists(trashRoot))
		{
			return items;
		}
		foreach (string sidecarPath in Directory.GetFiles(trashRoot, "*.delete-info.json", SearchOption.AllDirectories))
		{
			try
			{
				if (!TextHelpers.IsWithin(sidecarPath, trashRoot))
				{
					continue;
				}
				Dictionary<string, object> metadata = ReadMetadata(sidecarPath);
				string backupPath = Value(metadata, "backup_path");
				if (string.IsNullOrWhiteSpace(backupPath))
				{
					backupPath = sidecarPath.Substring(0, sidecarPath.Length - ".delete-info.json".Length);
				}
				backupPath = ValidateTrashPath(backupPath);
				if (!File.Exists(backupPath))
				{
					continue;
				}
				string projectPath = NormalizeOptionalPath(Value(metadata, "project_path"));
				if (string.IsNullOrWhiteSpace(projectPath))
				{
					projectPath = ReadProjectPathFromSession(backupPath);
				}
				DateTimeOffset deletedAt;
				if (!DateTimeOffset.TryParse(Value(metadata, "deleted_at"), CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out deletedAt))
				{
					deletedAt = new DateTimeOffset(File.GetLastWriteTime(backupPath));
				}
				long sizeBytes;
				if (!long.TryParse(Value(metadata, "size_bytes"), NumberStyles.Integer, CultureInfo.InvariantCulture, out sizeBytes) || sizeBytes < 0L)
				{
					sizeBytes = new FileInfo(backupPath).Length;
				}
				items.Add(new TrashSessionInfo
				{
					ThreadId = Value(metadata, "thread_id"),
					Title = Value(metadata, "title"),
					OriginalPath = Value(metadata, "original_path"),
					BackupPath = backupPath,
					SidecarPath = sidecarPath,
					ProjectPath = projectPath,
					ProjectDeleteMode = Value(metadata, "project_delete_mode"),
					DeletedAt = deletedAt,
					SizeBytes = sizeBytes
				});
			}
			catch
			{
			}
		}
		return items.OrderByDescending((TrashSessionInfo item) => item.DeletedAt).ToList();
	}

	public static void Restore(TrashSessionInfo item)
	{
		if (item == null)
		{
			throw new ArgumentNullException("item");
		}
		string backupPath = ValidateTrashPath(item.BackupPath);
		string sidecarPath = ValidateTrashPath(item.SidecarPath);
		if (!File.Exists(backupPath))
		{
			throw new FileNotFoundException("回收站中的会话备份已不存在。", backupPath);
		}
		string originalPath = ValidateSessionTargetPath(item.OriginalPath);
		if (File.Exists(originalPath))
		{
			throw new IOException("原位置已存在同名会话，已拒绝覆盖：\n" + originalPath);
		}
		string parent = Path.GetDirectoryName(originalPath);
		if (string.IsNullOrWhiteSpace(parent))
		{
			throw new InvalidOperationException("原会话路径无效。");
		}
		Directory.CreateDirectory(parent);
		File.Move(backupPath, originalPath);
		if (File.Exists(sidecarPath))
		{
			File.Delete(sidecarPath);
		}
		TryDeleteEmptyDirectory(Path.GetDirectoryName(backupPath));
	}

	public static void DeleteFromTrash(TrashSessionInfo item)
	{
		if (item == null)
		{
			throw new ArgumentNullException("item");
		}
		CctBackupMaintenance.DeleteForThread(CodexCatalog.ResolveCodexHome(), item.ThreadId, item.OriginalPath);
		string backupPath = ValidateTrashPath(item.BackupPath);
		string sidecarPath = ValidateTrashPath(item.SidecarPath);
		if (File.Exists(backupPath))
		{
			File.Delete(backupPath);
		}
		if (File.Exists(sidecarPath))
		{
			File.Delete(sidecarPath);
		}
		TryDeleteEmptyDirectory(Path.GetDirectoryName(backupPath));
	}

	public static string ResolveProjectPath(SessionInfo session, ProjectGroup project)
	{
		string cwd = session == null ? string.Empty : session.Cwd;
		string projectPath = project == null ? string.Empty : project.ProjectPath;
		try
		{
			if (!string.IsNullOrWhiteSpace(projectPath))
			{
				string fullProjectPath = Path.GetFullPath(TextHelpers.StripExtendedPrefix(projectPath));
				if (string.IsNullOrWhiteSpace(cwd) || TextHelpers.IsWithin(cwd, fullProjectPath))
				{
					return fullProjectPath;
				}
			}
			if (!string.IsNullOrWhiteSpace(cwd))
			{
				return Path.GetFullPath(TextHelpers.StripExtendedPrefix(cwd));
			}
		}
		catch
		{
		}
		return string.Empty;
	}

	public static string ValidateProjectPath(string projectPath)
	{
		if (string.IsNullOrWhiteSpace(projectPath))
		{
			throw new InvalidOperationException("该会话没有可用的项目路径。");
		}
		string fullPath = Path.GetFullPath(TextHelpers.StripExtendedPrefix(projectPath));
		if (!Directory.Exists(fullPath))
		{
			throw new DirectoryNotFoundException("项目目录不存在：\n" + fullPath);
		}
		string root = Path.GetPathRoot(fullPath);
		if (string.Equals(TextHelpers.CanonicalPath(fullPath), TextHelpers.CanonicalPath(root), StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException("安全校验失败：不能删除磁盘根目录。");
		}
		foreach (Environment.SpecialFolder folder in ProtectedSpecialFolders())
		{
			string protectedPath = Environment.GetFolderPath(folder);
			if (!string.IsNullOrWhiteSpace(protectedPath) && string.Equals(TextHelpers.CanonicalPath(fullPath), TextHelpers.CanonicalPath(protectedPath), StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidOperationException("安全校验失败：不能删除系统或用户根目录：\n" + fullPath);
			}
		}
		string codexHome = Path.GetFullPath(CodexCatalog.ResolveCodexHome());
		if (TextHelpers.IsWithin(fullPath, codexHome) || TextHelpers.IsWithin(codexHome, fullPath))
		{
			throw new InvalidOperationException("安全校验失败：项目路径与 Codex 配置目录重叠。");
		}
		string applicationDirectory = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);
		if (TextHelpers.IsWithin(applicationDirectory, fullPath) || TextHelpers.IsWithin(fullPath, applicationDirectory))
		{
			throw new InvalidOperationException("安全校验失败：项目路径与当前工具目录重叠。请把工具移到其他位置后再处理该项目。");
		}
		return fullPath;
	}

	public static void DeleteProject(string projectPath, ProjectDeleteMode mode)
	{
		if (mode == ProjectDeleteMode.None)
		{
			return;
		}
		string fullPath = ValidateProjectPath(projectPath);
		if (mode == ProjectDeleteMode.RecycleBin)
		{
			Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(fullPath, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
			return;
		}
		Directory.Delete(fullPath, recursive: true);
	}

	public static void MarkProjectHandled(TrashSessionInfo item, ProjectDeleteMode mode)
	{
		if (item == null || string.IsNullOrWhiteSpace(item.SidecarPath))
		{
			return;
		}
		string sidecarPath = ValidateTrashPath(item.SidecarPath);
		Dictionary<string, object> metadata = ReadMetadata(sidecarPath);
		metadata["project_path"] = NormalizeOptionalPath(item.ProjectPath);
		metadata["project_delete_mode"] = mode == ProjectDeleteMode.RecycleBin ? "windows_recycle_bin" : "permanent";
		metadata["project_deleted_at"] = DateTimeOffset.Now.ToString("o");
		WriteMetadata(sidecarPath, metadata);
	}

	public static void MarkProjectHandled(string backupPath, string projectPath, ProjectDeleteMode mode)
	{
		if (string.IsNullOrWhiteSpace(backupPath))
		{
			return;
		}
		string validatedBackup = ValidateTrashPath(backupPath);
		string sidecarPath = validatedBackup + ".delete-info.json";
		if (!File.Exists(sidecarPath))
		{
			return;
		}
		Dictionary<string, object> metadata = ReadMetadata(sidecarPath);
		metadata["project_path"] = NormalizeOptionalPath(projectPath);
		metadata["project_delete_mode"] = mode == ProjectDeleteMode.RecycleBin ? "windows_recycle_bin" : "permanent";
		metadata["project_deleted_at"] = DateTimeOffset.Now.ToString("o");
		WriteMetadata(sidecarPath, metadata);
	}

	private static string ResolveValidatedSessionPath(SessionInfo session)
	{
		if (session == null)
		{
			throw new ArgumentNullException("session");
		}
		string path = CodexCatalog.ResolveSessionPath(session);
		if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
		{
			throw new FileNotFoundException("找不到该对话对应的本地会话文件。", path);
		}
		path = ValidateSessionTargetPath(path);
		return path;
	}

	private static string ValidateSessionTargetPath(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			throw new InvalidOperationException("会话路径为空。");
		}
		string fullPath = Path.GetFullPath(TextHelpers.StripExtendedPrefix(path));
		string codexHome = Path.GetFullPath(CodexCatalog.ResolveCodexHome());
		string sessionsRoot = Path.Combine(codexHome, "sessions");
		string archivedRoot = Path.Combine(codexHome, "archived_sessions");
		if (!TextHelpers.IsWithin(fullPath, sessionsRoot) && !TextHelpers.IsWithin(fullPath, archivedRoot))
		{
			throw new InvalidOperationException("安全校验失败：会话文件不在 Codex 会话目录内，已拒绝删除或恢复。");
		}
		return fullPath;
	}

	private static string ValidateTrashPath(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			throw new InvalidOperationException("回收站路径为空。");
		}
		string fullPath = Path.GetFullPath(TextHelpers.StripExtendedPrefix(path));
		if (!TextHelpers.IsWithin(fullPath, TrashRoot) || string.Equals(TextHelpers.CanonicalPath(fullPath), TextHelpers.CanonicalPath(TrashRoot), StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException("安全校验失败：目标不在软件回收站内。");
		}
		return fullPath;
	}

	private static string UniqueFilePath(string directory, string fileName)
	{
		string path = Path.GetFullPath(Path.Combine(directory, fileName));
		int suffix = 1;
		while (File.Exists(path) || File.Exists(path + ".delete-info.json"))
		{
			path = Path.Combine(directory, Path.GetFileNameWithoutExtension(fileName) + "-" + suffix + Path.GetExtension(fileName));
			suffix++;
		}
		return path;
	}

	private static string ReadProjectPathFromSession(string sessionPath)
	{
		if (!sessionPath.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase))
		{
			return string.Empty;
		}
		try
		{
			using (StreamReader reader = new StreamReader(sessionPath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
			{
				string firstLine = reader.ReadLine();
				if (string.IsNullOrWhiteSpace(firstLine))
				{
					return string.Empty;
				}
				Dictionary<string, object> root = CctRunner.NewSerializer().DeserializeObject(firstLine) as Dictionary<string, object>;
				Dictionary<string, object> payload = root != null && root.ContainsKey("payload") ? root["payload"] as Dictionary<string, object> : null;
				return NormalizeOptionalPath(Value(payload, "cwd"));
			}
		}
		catch
		{
			return string.Empty;
		}
	}

	private static Dictionary<string, object> ReadMetadata(string path)
	{
		Dictionary<string, object> metadata = CctRunner.NewSerializer().DeserializeObject(File.ReadAllText(path, Encoding.UTF8)) as Dictionary<string, object>;
		if (metadata == null)
		{
			throw new InvalidDataException("删除信息文件格式无效。");
		}
		return metadata;
	}

	private static void WriteMetadata(string path, Dictionary<string, object> metadata)
	{
		File.WriteAllText(path, CctRunner.NewSerializer().Serialize(metadata), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
	}

	private static string Value(Dictionary<string, object> source, string key)
	{
		if (source == null || !source.TryGetValue(key, out var value) || value == null)
		{
			return string.Empty;
		}
		return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
	}

	private static string NormalizeOptionalPath(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return string.Empty;
		}
		try
		{
			return Path.GetFullPath(TextHelpers.StripExtendedPrefix(path));
		}
		catch
		{
			return path.Trim();
		}
	}

	private static Environment.SpecialFolder[] ProtectedSpecialFolders()
	{
		return new Environment.SpecialFolder[12]
		{
			Environment.SpecialFolder.UserProfile,
			Environment.SpecialFolder.Desktop,
			Environment.SpecialFolder.MyDocuments,
			Environment.SpecialFolder.ApplicationData,
			Environment.SpecialFolder.LocalApplicationData,
			Environment.SpecialFolder.CommonApplicationData,
			Environment.SpecialFolder.Windows,
			Environment.SpecialFolder.System,
			Environment.SpecialFolder.ProgramFiles,
			Environment.SpecialFolder.ProgramFilesX86,
			Environment.SpecialFolder.CommonProgramFiles,
			Environment.SpecialFolder.CommonProgramFilesX86
		};
	}

	private static void TryDeleteEmptyDirectory(string directory)
	{
		try
		{
			if (!string.IsNullOrWhiteSpace(directory) && TextHelpers.IsWithin(directory, TrashRoot) && !Directory.EnumerateFileSystemEntries(directory).Any())
			{
				Directory.Delete(directory);
			}
		}
		catch
		{
		}
	}
}
