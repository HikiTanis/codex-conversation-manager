using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace CodexConversationMigrator;

internal static class MigrationDuplicateCleaner
{
	private sealed class LocalMetadata
	{
		public string Id { get; set; }

		public string Cwd { get; set; }
	}

	private sealed class MoveRecord
	{
		public string source { get; set; }

		public string backup { get; set; }

		public string original_thread_id { get; set; }

		public string duplicate_thread_id { get; set; }
	}

	public static DuplicateCleanupResult MoveVerifiedLegacyCopies(IEnumerable<string> bundlePaths, string codexHome, string targetPath)
	{
		DuplicateCleanupResult duplicateCleanupResult = new DuplicateCleanupResult();
		if (bundlePaths == null || string.IsNullOrWhiteSpace(codexHome) || string.IsNullOrWhiteSpace(targetPath))
		{
			return duplicateCleanupResult;
		}
		List<MoveRecord> list = new List<MoveRecord>();
		string text = string.Empty;
		foreach (string bundlePath in bundlePaths)
		{
			if (!File.Exists(bundlePath))
			{
				continue;
			}
			using ZipArchive zipArchive = ZipFile.OpenRead(bundlePath);
			ZipArchiveEntry entry = zipArchive.GetEntry("manifest.json");
			if (entry == null)
			{
				continue;
			}
			CctBundleManifest cctBundleManifest;
			using (StreamReader streamReader = new StreamReader(entry.Open(), Encoding.UTF8))
			{
				cctBundleManifest = CctRunner.NewSerializer().Deserialize<CctBundleManifest>(streamReader.ReadToEnd());
			}
			foreach (CctBundleSession item in cctBundleManifest.sessions ?? new List<CctBundleSession>())
			{
				if (item == null || item.compressed || string.IsNullOrWhiteSpace(item.thread_id) || string.IsNullOrWhiteSpace(item.bundle_path))
				{
					continue;
				}
				ZipArchiveEntry entry2 = zipArchive.GetEntry(item.bundle_path.Replace('\\', '/'));
				if (entry2 == null)
				{
					continue;
				}
				byte[] data = ReadEntry(entry2);
				string text2 = CanonicalSessionHash(data);
				if (string.IsNullOrWhiteSpace(text2))
				{
					continue;
				}
				foreach (string item2 in CandidateFiles(codexHome, item))
				{
					byte[] data2;
					LocalMetadata localMetadata;
					try
					{
						data2 = File.ReadAllBytes(item2);
						localMetadata = ReadMetadata(data2);
					}
					catch
					{
						continue;
					}
					if (localMetadata != null && !string.IsNullOrWhiteSpace(localMetadata.Id) && !string.Equals(localMetadata.Id, item.thread_id, StringComparison.OrdinalIgnoreCase) && IsVersionFourGuid(localMetadata.Id) && string.Equals(TextHelpers.CanonicalPath(localMetadata.Cwd), TextHelpers.CanonicalPath(targetPath), StringComparison.OrdinalIgnoreCase) && string.Equals(text2, CanonicalSessionHash(data2), StringComparison.Ordinal))
					{
						if (string.IsNullOrWhiteSpace(text))
						{
							text = Path.Combine(codexHome, "conversation-migrator-trash", "migration-duplicates-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
							Directory.CreateDirectory(text);
						}
						string path = SafeRelativePath(codexHome, item2);
						string text3 = Path.Combine(text, path);
						string directoryName = Path.GetDirectoryName(text3);
						if (!Directory.Exists(directoryName))
						{
							Directory.CreateDirectory(directoryName);
						}
						if (File.Exists(text3))
						{
							text3 = Path.Combine(directoryName, Path.GetFileNameWithoutExtension(text3) + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + Path.GetExtension(text3));
						}
						File.Move(item2, text3);
						list.Add(new MoveRecord
						{
							source = item2,
							backup = text3,
							original_thread_id = item.thread_id,
							duplicate_thread_id = localMetadata.Id
						});
					}
				}
			}
		}
		if (list.Count > 0)
		{
			string path2 = Path.Combine(text, "恢复清单.json");
			Dictionary<string, object> dictionary = new Dictionary<string, object>();
			dictionary.Add("created_at", DateTime.UtcNow.ToString("o"));
			dictionary.Add("reason", "Codex 对话迁移助手 2.1.6 生成的内容完全相同的新 ID 副本");
			dictionary.Add("files", list);
			Dictionary<string, object> obj2 = dictionary;
			File.WriteAllText(path2, CctRunner.NewSerializer().Serialize(obj2), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		}
		duplicateCleanupResult.MovedCount = list.Count;
		duplicateCleanupResult.TrashDirectory = text;
		return duplicateCleanupResult;
	}

	private static IEnumerable<string> CandidateFiles(string codexHome, CctBundleSession session)
	{
		string normalized = session.bundle_path.Replace('\\', '/');
		string relative = normalized;
		if (relative.StartsWith("sessions/", StringComparison.OrdinalIgnoreCase))
		{
			relative = relative.Substring("sessions/".Length);
		}
		else if (relative.StartsWith("archived_sessions/", StringComparison.OrdinalIgnoreCase))
		{
			relative = relative.Substring("archived_sessions/".Length);
		}
		string fileName = Path.GetFileName(relative);
		int idPosition = fileName.IndexOf(session.thread_id, StringComparison.OrdinalIgnoreCase);
		if (idPosition < 0)
		{
			yield break;
		}
		string prefix = fileName.Substring(0, idPosition);
		string relativeDirectory = Path.GetDirectoryName(relative.Replace('/', Path.DirectorySeparatorChar)) ?? string.Empty;
		try
		{
			string[] array = new string[2] { "sessions", "archived_sessions" };
			foreach (string rootName in array)
			{
				string directory = Path.Combine(codexHome, rootName, relativeDirectory);
				if (!Directory.Exists(directory))
				{
					continue;
				}
				try
				{
					string[] files = Directory.GetFiles(directory, prefix + "*.jsonl", SearchOption.TopDirectoryOnly);
					for (int j = 0; j < files.Length; j++)
					{
						yield return files[j];
					}
				}
				finally
				{
				}
			}
		}
		finally
		{
		}
	}

	private static LocalMetadata ReadMetadata(byte[] data)
	{
		int num = Array.IndexOf(data, (byte)10);
		if (num < 0)
		{
			return null;
		}
		int num2 = num;
		if (num2 > 0 && data[num2 - 1] == 13)
		{
			num2--;
		}
		object value;
		Dictionary<string, object> dictionary = ((CctRunner.NewSerializer().DeserializeObject(Encoding.UTF8.GetString(data, 0, num2)) is Dictionary<string, object> dictionary2 && dictionary2.TryGetValue("payload", out value)) ? (value as Dictionary<string, object>) : null);
		if (dictionary == null)
		{
			return null;
		}
		dictionary.TryGetValue("id", out var value2);
		if (value2 == null)
		{
			dictionary.TryGetValue("session_id", out value2);
		}
		dictionary.TryGetValue("cwd", out var value3);
		LocalMetadata localMetadata = new LocalMetadata();
		localMetadata.Id = Convert.ToString(value2, CultureInfo.InvariantCulture);
		localMetadata.Cwd = TextHelpers.StripExtendedPrefix(Convert.ToString(value3, CultureInfo.InvariantCulture));
		return localMetadata;
	}

	private static string CanonicalSessionHash(byte[] data)
	{
		int num = Array.IndexOf(data, (byte)10);
		if (num < 0)
		{
			return string.Empty;
		}
		int num2 = num;
		if (num2 > 0 && data[num2 - 1] == 13)
		{
			num2--;
		}
		Dictionary<string, object> dictionary;
		try
		{
			dictionary = CctRunner.NewSerializer().DeserializeObject(Encoding.UTF8.GetString(data, 0, num2)) as Dictionary<string, object>;
		}
		catch
		{
			return string.Empty;
		}
		object value;
		Dictionary<string, object> dictionary2 = ((dictionary != null && dictionary.TryGetValue("payload", out value)) ? (value as Dictionary<string, object>) : null);
		if (dictionary2 == null)
		{
			return string.Empty;
		}
		dictionary2["id"] = "{thread-id}";
		if (dictionary2.ContainsKey("session_id"))
		{
			dictionary2["session_id"] = "{thread-id}";
		}
		if (dictionary2.ContainsKey("cwd"))
		{
			dictionary2["cwd"] = "{project-path}";
		}
		byte[] bytes = Encoding.UTF8.GetBytes(CctRunner.NewSerializer().Serialize(OrderJsonValue(dictionary)) + "\n");
		using SHA256 sHA = SHA256.Create();
		using MemoryStream memoryStream = new MemoryStream();
		memoryStream.Write(bytes, 0, bytes.Length);
		if (num + 1 < data.Length)
		{
			memoryStream.Write(data, num + 1, data.Length - num - 1);
		}
		memoryStream.Position = 0L;
		byte[] array = sHA.ComputeHash(memoryStream);
		return BitConverter.ToString(array).Replace("-", string.Empty).ToLowerInvariant();
	}

	private static object OrderJsonValue(object value)
	{
		if (value is Dictionary<string, object> dictionary)
		{
			SortedDictionary<string, object> sortedDictionary = new SortedDictionary<string, object>(StringComparer.Ordinal);
			{
				foreach (KeyValuePair<string, object> item in dictionary)
				{
					sortedDictionary[item.Key] = OrderJsonValue(item.Value);
				}
				return sortedDictionary;
			}
		}
		if (value is object[] source)
		{
			return source.Select(OrderJsonValue).ToArray();
		}
		return value;
	}

	private static bool IsVersionFourGuid(string value)
	{
		string text = (value ?? string.Empty).Trim().ToLowerInvariant();
		if (Guid.TryParse(text, out var _) && text.Length == 36)
		{
			return text[14] == '4';
		}
		return false;
	}

	private static string SafeRelativePath(string codexHome, string path)
	{
		string text = Path.GetFullPath(codexHome).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
		string fullPath = Path.GetFullPath(path);
		if (!fullPath.StartsWith(text, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidDataException("重复会话文件不在 Codex 目录内，已停止清理。");
		}
		return fullPath.Substring(text.Length);
	}

	private static byte[] ReadEntry(ZipArchiveEntry entry)
	{
		using Stream stream = entry.Open();
		using MemoryStream memoryStream = new MemoryStream();
		stream.CopyTo(memoryStream);
		return memoryStream.ToArray();
	}
}
