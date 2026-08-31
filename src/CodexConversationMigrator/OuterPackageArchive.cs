using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace CodexConversationMigrator;

/// <summary>
/// Reads and extracts the outer .codexchat/.codexproject container. Inner
/// conversation bundles and project payloads have their own validation; this
/// layer prevents an untrusted outer ZIP from consuming unbounded resources
/// before those validators get a chance to run.
/// </summary>
internal static class OuterPackageArchive
{
	private const int MaximumArchiveEntries = 50000;

	private const int MaximumManifestBytes = 16 * 1024 * 1024;

	private const long MaximumSingleEntryBytes = 128L * 1024L * 1024L * 1024L;

	private const long MaximumExpandedBytes = 128L * 1024L * 1024L * 1024L;

	private const long CompressionRatioCheckThreshold = 16L * 1024L * 1024L;

	private const long MaximumCompressionRatio = 10000L;

	private sealed class ArchiveItem
	{
		public ZipArchiveEntry Entry { get; set; }

		public string RelativePath { get; set; }

		public bool IsDirectory { get; set; }
	}

	private sealed class ValidatedArchive
	{
		public List<ArchiveItem> Items { get; } = new List<ArchiveItem>();

		public ZipArchiveEntry ManifestEntry { get; set; }

		public long ExpandedBytes { get; set; }
	}

	public static PackManifest ReadManifest(string packagePath)
	{
		using ZipArchive archive = ZipFile.OpenRead(packagePath);
		ValidatedArchive validated = ValidateArchive(archive);
		return DeserializeManifest(ReadManifestBytes(validated.ManifestEntry));
	}

	public static void ExtractSafely(string packagePath, string destination)
	{
		if (string.IsNullOrWhiteSpace(destination))
		{
			throw new ArgumentException(Message("迁移包解压目录为空。"), nameof(destination));
		}
		string destinationRoot = Path.GetFullPath(destination)
			.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		Directory.CreateDirectory(destinationRoot);
		EnsureNoReparsePoint(destinationRoot, destinationRoot);

		using ZipArchive archive = ZipFile.OpenRead(packagePath);
		ValidatedArchive validated = ValidateArchive(archive);
		EnsureDestinationDiskSpace(destinationRoot, validated.ExpandedBytes);

		long totalWritten = 0L;
		foreach (ArchiveItem item in validated.Items)
		{
			string outputPath = ResolveDestination(destinationRoot, item.RelativePath);
			if (item.IsDirectory)
			{
				if (File.Exists(outputPath))
				{
					throw new InvalidDataException(Message("迁移包路径同时被声明为文件和目录：") + item.RelativePath);
				}
				EnsureNoReparsePoint(destinationRoot, Path.GetDirectoryName(outputPath));
				Directory.CreateDirectory(outputPath);
				EnsureNoReparsePoint(destinationRoot, outputPath);
				continue;
			}

			string parent = Path.GetDirectoryName(outputPath);
			EnsureNoReparsePoint(destinationRoot, parent);
			Directory.CreateDirectory(parent);
			EnsureNoReparsePoint(destinationRoot, parent);
			if (Directory.Exists(outputPath))
			{
				throw new InvalidDataException(Message("迁移包路径同时被声明为文件和目录：") + item.RelativePath);
			}
			CopyEntryCounted(item.Entry, outputPath, ref totalWritten);
		}
		if (totalWritten > validated.ExpandedBytes)
		{
			throw new InvalidDataException(Message("迁移包实际展开大小超过清单限制，已停止解压。"));
		}
	}

	public static void CreateFromDirectoryAtomic(string sourceDirectory, string outputPath)
	{
		string source = Path.GetFullPath(sourceDirectory ?? string.Empty);
		if (!Directory.Exists(source))
		{
			throw new DirectoryNotFoundException(Message("迁移包暂存目录不存在：") + source);
		}
		string output = Path.GetFullPath(outputPath ?? string.Empty);
		string outputDirectory = Path.GetDirectoryName(output);
		if (string.IsNullOrWhiteSpace(outputDirectory) || !Directory.Exists(outputDirectory))
		{
			throw new DirectoryNotFoundException(Message("迁移包保存目录不存在：") + outputDirectory);
		}
		if (IsWithin(output, source))
		{
			throw new InvalidOperationException(Message("迁移包不能保存到自身暂存目录中。"));
		}
		if (File.Exists(output))
		{
			throw new IOException(Message("备份文件在创建期间已存在，请重试：\n") + output);
		}

		string temporary = Path.Combine(
			outputDirectory,
			"." + Path.GetFileName(output) + "." + Guid.NewGuid().ToString("N") + ".tmp");
		try
		{
			ZipFile.CreateFromDirectory(source, temporary, CompressionLevel.Optimal, includeBaseDirectory: false);
			using (ZipArchive archive = ZipFile.OpenRead(temporary))
			{
				ValidatedArchive validated = ValidateArchive(archive);
				DeserializeManifest(ReadManifestBytes(validated.ManifestEntry));
			}
			if (File.Exists(output))
			{
				throw new IOException(Message("备份文件在创建期间已存在，请重试：\n") + output);
			}
			File.Move(temporary, output);
		}
		finally
		{
			TryDeleteFile(temporary);
		}
	}

	private static ValidatedArchive ValidateArchive(ZipArchive archive)
	{
		if (archive.Entries.Count > MaximumArchiveEntries)
		{
			throw new InvalidDataException(Message("迁移包文件数量超过 50000 个，已停止读取。"));
		}

		ValidatedArchive result = new ValidatedArchive();
		Dictionary<string, ArchiveItem> paths = new Dictionary<string, ArchiveItem>(StringComparer.OrdinalIgnoreCase);
		foreach (ZipArchiveEntry entry in archive.Entries)
		{
			RejectLink(entry);
			bool directory = string.IsNullOrEmpty(entry.Name);
			string relative = NormalizeRelativePath(entry.FullName, directory);
			if (directory && entry.Length != 0L)
			{
				throw new InvalidDataException(Message("迁移包目录条目包含数据，已拒绝读取：") + entry.FullName);
			}
			if (entry.Length < 0L || entry.CompressedLength < 0L || entry.Length > MaximumSingleEntryBytes)
			{
				throw new InvalidDataException(Message("迁移包单个条目展开大小超过 128 GB，已停止读取：") + entry.FullName);
			}
			if (result.ExpandedBytes > MaximumExpandedBytes - entry.Length)
			{
				throw new InvalidDataException(Message("迁移包展开大小无效或超过 128 GB，已停止读取。"));
			}
			result.ExpandedBytes += entry.Length;
			if (entry.Length >= CompressionRatioCheckThreshold &&
				(entry.CompressedLength == 0L || entry.Length / Math.Max(1L, entry.CompressedLength) > MaximumCompressionRatio))
			{
				throw new InvalidDataException(Message("迁移包包含异常压缩比文件，已停止读取：") + entry.FullName);
			}
			if (paths.ContainsKey(relative))
			{
				throw new InvalidDataException(Message("迁移包包含重复路径：") + relative);
			}
			ArchiveItem item = new ArchiveItem
			{
				Entry = entry,
				RelativePath = relative,
				IsDirectory = directory
			};
			paths.Add(relative, item);
			result.Items.Add(item);
		}

		HashSet<string> files = new HashSet<string>(
			result.Items.Where(item => !item.IsDirectory).Select(item => item.RelativePath),
			StringComparer.OrdinalIgnoreCase);
		foreach (ArchiveItem item in result.Items)
		{
			string parent = Path.GetDirectoryName(item.RelativePath);
			while (!string.IsNullOrWhiteSpace(parent))
			{
				if (files.Contains(parent))
				{
					throw new InvalidDataException(Message("迁移包路径同时被声明为文件和目录：") + parent);
				}
				parent = Path.GetDirectoryName(parent);
			}
		}

		if (!paths.TryGetValue("manifest.json", out ArchiveItem manifest) || manifest.IsDirectory)
		{
			throw new InvalidDataException(Message("迁移包缺少 manifest.json。"));
		}
		if (manifest.Entry.Length > MaximumManifestBytes)
		{
			throw new InvalidDataException(Message("迁移包的 manifest.json 超过 16 MB，已停止读取。"));
		}
		result.ManifestEntry = manifest.Entry;
		return result;
	}

	private static byte[] ReadManifestBytes(ZipArchiveEntry entry)
	{
		if (entry.Length < 0L || entry.Length > MaximumManifestBytes || entry.Length > int.MaxValue)
		{
			throw new InvalidDataException(Message("迁移包的 manifest.json 超过 16 MB，已停止读取。"));
		}
		using Stream input = entry.Open();
		using MemoryStream output = new MemoryStream((int)entry.Length);
		byte[] buffer = new byte[81920];
		long written = 0L;
		while (true)
		{
			int read = input.Read(buffer, 0, buffer.Length);
			if (read == 0)
			{
				break;
			}
			written += read;
			if (written > entry.Length || written > MaximumManifestBytes)
			{
				throw new InvalidDataException(Message("迁移包的 manifest.json 实际展开长度超过 16 MB，已停止读取。"));
			}
			output.Write(buffer, 0, read);
		}
		if (written != entry.Length)
		{
			throw new InvalidDataException(Message("迁移包的 manifest.json 读取不完整。"));
		}
		return output.ToArray();
	}

	private static PackManifest DeserializeManifest(byte[] bytes)
	{
		try
		{
			string json = new UTF8Encoding(false, true).GetString(bytes);
			if (json.Length > 0 && json[0] == '\uFEFF')
			{
				json = json.Substring(1);
			}
			PackManifest manifest = JsonSerialization.NewSerializer().Deserialize<PackManifest>(json);
			if (manifest == null)
			{
				throw new InvalidDataException(Message("迁移包清单格式无效。"));
			}
			return manifest;
		}
		catch (InvalidDataException)
		{
			throw;
		}
		catch (Exception ex)
		{
			throw new InvalidDataException(Message("迁移包清单格式无效。"), ex);
		}
	}

	private static void CopyEntryCounted(ZipArchiveEntry entry, string destination, ref long totalWritten)
	{
		long entryWritten = 0L;
		try
		{
			using Stream input = entry.Open();
			using FileStream output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
			byte[] buffer = new byte[81920];
			while (true)
			{
				int read = input.Read(buffer, 0, buffer.Length);
				if (read == 0)
				{
					break;
				}
				entryWritten += read;
				if (entryWritten > entry.Length || entryWritten > MaximumSingleEntryBytes || totalWritten > MaximumExpandedBytes - read)
				{
					throw new InvalidDataException(Message("迁移包条目实际展开长度超过限制，已停止解压：") + entry.FullName);
				}
				totalWritten += read;
				output.Write(buffer, 0, read);
			}
			if (entryWritten != entry.Length)
			{
				throw new InvalidDataException(Message("迁移包条目读取不完整：") + entry.FullName);
			}
		}
		catch
		{
			TryDeleteFile(destination);
			throw;
		}
	}

	private static string NormalizeRelativePath(string value, bool directory)
	{
		if (string.IsNullOrWhiteSpace(value) || value.IndexOf('\0') >= 0)
		{
			throw new InvalidDataException(Message("迁移包包含无效路径：") + value);
		}
		string normalized = value.Replace('/', Path.DirectorySeparatorChar)
			.Replace('\\', Path.DirectorySeparatorChar);
		if (directory)
		{
			normalized = normalized.TrimEnd(Path.DirectorySeparatorChar);
		}
		if (string.IsNullOrWhiteSpace(normalized) || Path.IsPathRooted(normalized))
		{
			throw new InvalidDataException(Message("迁移包包含绝对路径：") + value);
		}
		string[] segments = normalized.Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.None);
		foreach (string segment in segments)
		{
			if (string.IsNullOrWhiteSpace(segment) || segment == "." || segment == ".." ||
				segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
				segment.EndsWith(".", StringComparison.Ordinal) ||
				segment.EndsWith(" ", StringComparison.Ordinal) ||
				IsReservedDeviceName(segment))
			{
				throw new InvalidDataException(Message("迁移包包含无效路径：") + value);
			}
		}
		return string.Join(Path.DirectorySeparatorChar.ToString(), segments);
	}

	private static string ResolveDestination(string root, string relative)
	{
		string destination = Path.GetFullPath(Path.Combine(root, relative));
		string prefix = root + Path.DirectorySeparatorChar;
		if (!destination.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidDataException(Message("迁移包包含越界路径，已拒绝解压。"));
		}
		return destination;
	}

	private static void RejectLink(ZipArchiveEntry entry)
	{
		int unixMode = (entry.ExternalAttributes >> 16) & 61440;
		bool unixSymbolicLink = unixMode == 40960;
		bool windowsReparsePoint = (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0;
		if (unixSymbolicLink || windowsReparsePoint)
		{
			throw new InvalidDataException(Message("迁移包包含重解析点或符号链接，已拒绝读取：") + entry.FullName);
		}
	}

	private static void EnsureNoReparsePoint(string root, string path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return;
		}
		string normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
		string current = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
		while (string.Equals(current, normalizedRoot, StringComparison.OrdinalIgnoreCase) ||
			current.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
		{
			if (Directory.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
			{
				throw new IOException(Message("迁移包解压目录包含重解析点，已拒绝写入：") + current);
			}
			if (string.Equals(current, normalizedRoot, StringComparison.OrdinalIgnoreCase))
			{
				break;
			}
			current = Path.GetDirectoryName(current);
			if (string.IsNullOrWhiteSpace(current))
			{
				break;
			}
		}
	}

	private static void EnsureDestinationDiskSpace(string destination, long expandedBytes)
	{
		long overhead = Math.Min(256L * 1024L * 1024L, Math.Max(16L * 1024L * 1024L, expandedBytes / 20L));
		long required = expandedBytes > long.MaxValue - overhead ? long.MaxValue : expandedBytes + overhead;
		try
		{
			string driveRoot = Path.GetPathRoot(Path.GetFullPath(destination));
			DriveInfo drive = new DriveInfo(driveRoot);
			if (drive.IsReady && drive.AvailableFreeSpace < required)
			{
				throw new IOException(
					Message("系统临时盘空间不足，无法安全解压此迁移包。需要约 ") +
					TextHelpers.FormatBytes(required) + Message("，当前可用 ") +
					TextHelpers.FormatBytes(drive.AvailableFreeSpace) + "。");
			}
		}
		catch (IOException)
		{
			throw;
		}
		catch
		{
			// Some virtual/removable volumes do not expose capacity. The hard
			// byte limits and counted extraction still apply in that case.
		}
	}

	private static bool IsWithin(string candidate, string root)
	{
		string normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
		string normalizedCandidate = Path.GetFullPath(candidate);
		return normalizedCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsReservedDeviceName(string segment)
	{
		string name = Path.GetFileNameWithoutExtension(segment).ToUpperInvariant();
		if (name == "CON" || name == "PRN" || name == "AUX" || name == "NUL")
		{
			return true;
		}
		return name.Length == 4 &&
			(name.StartsWith("COM", StringComparison.Ordinal) || name.StartsWith("LPT", StringComparison.Ordinal)) &&
			name[3] >= '1' && name[3] <= '9';
	}

	private static void TryDeleteFile(string path)
	{
		try
		{
			if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
			{
				File.Delete(path);
			}
		}
		catch
		{
		}
	}

	private static string Message(string chinese)
	{
		return UiLanguage.T(chinese);
	}
}
