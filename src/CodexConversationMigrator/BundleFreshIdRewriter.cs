using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace CodexConversationMigrator;

internal static class BundleFreshIdRewriter
{
	private sealed class RewriteItem
	{
		public CctBundleSession Session { get; set; }

		public string OldId { get; set; }

		public string NewId { get; set; }

		public string OriginId { get; set; }

		public string OldBundlePath { get; set; }

		public string NewBundlePath { get; set; }
	}

	public static HashSet<string> FindIndexedPathMismatches(string bundlePath, IDictionary<string, string> indexedCwds, string targetPath)
	{
		HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		if (indexedCwds == null || indexedCwds.Count == 0 || string.IsNullOrWhiteSpace(targetPath))
		{
			return result;
		}
		CctBundleManifest manifest = ReadManifest(bundlePath);
		foreach (CctBundleSession session in manifest.sessions ?? new List<CctBundleSession>())
		{
			if (session != null && !string.IsNullOrWhiteSpace(session.thread_id) && indexedCwds.TryGetValue(session.thread_id, out string cwd) && !string.Equals(TextHelpers.CanonicalPath(cwd), TextHelpers.CanonicalPath(targetPath), StringComparison.OrdinalIgnoreCase))
			{
				result.Add(session.thread_id);
			}
		}
		return result;
	}

	public static List<BundleSessionLineage> ReadLineages(string bundlePath)
	{
		CctBundleManifest manifest = ReadManifest(bundlePath);
		List<BundleSessionLineage> result = new List<BundleSessionLineage>();
		using (ZipArchive archive = ZipFile.OpenRead(bundlePath))
		{
			foreach (CctBundleSession session in manifest.sessions ?? new List<CctBundleSession>())
			{
				if (session == null || string.IsNullOrWhiteSpace(session.thread_id))
				{
					continue;
				}
				string embeddedOrigin = string.Empty;
				ZipArchiveEntry entry = archive.GetEntry((session.bundle_path ?? string.Empty).Replace('\\', '/'));
				if (entry != null && !session.compressed && !(session.bundle_path ?? string.Empty).EndsWith(".zst", StringComparison.OrdinalIgnoreCase))
				{
					try
					{
						Dictionary<string, object> payload = ReadPayload(ReadEntry(entry), session.thread_id);
						embeddedOrigin = ConversationLineage.ResolveOriginThreadId(payload, session.thread_id);
					}
					catch
					{
					}
				}
				string origin = !string.IsNullOrWhiteSpace(session.origin_thread_id) ? session.origin_thread_id : embeddedOrigin;
				if (string.IsNullOrWhiteSpace(origin))
				{
					origin = session.thread_id;
				}
				result.Add(new BundleSessionLineage
				{
					CurrentThreadId = session.thread_id,
					OriginThreadId = origin,
					OriginalCwd = session.original_cwd
				});
			}
		}
		return result;
	}

	public static FreshIdRewriteResult RewriteAsFresh(string inputPath, string outputPath)
	{
		Dictionary<string, string> idMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (BundleSessionLineage session in ReadLineages(inputPath))
		{
			idMap[session.CurrentThreadId] = Guid.NewGuid().ToString("D");
		}
		return RewriteWithIdMap(inputPath, outputPath, idMap);
	}

	public static FreshIdRewriteResult Rewrite(string inputPath, string outputPath, ISet<string> threadIds)
	{
		Dictionary<string, string> idMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (BundleSessionLineage session in ReadLineages(inputPath))
		{
			idMap[session.CurrentThreadId] = threadIds != null && threadIds.Contains(session.CurrentThreadId) ? Guid.NewGuid().ToString("D") : session.CurrentThreadId;
		}
		return RewriteWithIdMap(inputPath, outputPath, idMap);
	}

	public static FreshIdRewriteResult RewriteWithIdMap(string inputPath, string outputPath, IDictionary<string, string> requestedIdMap)
	{
		CctBundleManifest manifest = ReadManifest(inputPath);
		List<BundleSessionLineage> lineages = ReadLineages(inputPath);
		Dictionary<string, BundleSessionLineage> lineageById = lineages.GroupBy(item => item.CurrentThreadId, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
		Dictionary<string, string> idMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		Dictionary<string, RewriteItem> itemsByPath = new Dictionary<string, RewriteItem>(StringComparer.OrdinalIgnoreCase);
		HashSet<string> finalIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (CctBundleSession session in manifest.sessions ?? new List<CctBundleSession>())
		{
			if (session == null || string.IsNullOrWhiteSpace(session.thread_id))
			{
				throw new InvalidDataException("对话包清单包含空的 Thread ID。");
			}
			if (session.compressed || (session.bundle_path ?? string.Empty).EndsWith(".zst", StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidDataException("对话使用 .zst 压缩格式，当前无法安全写入原始编号或重写父子编号：" + session.thread_id);
			}
			string oldId = session.thread_id;
			string newId = requestedIdMap != null && requestedIdMap.TryGetValue(oldId, out string mapped) && !string.IsNullOrWhiteSpace(mapped) ? mapped : oldId;
			if (!finalIds.Add(newId))
			{
				throw new InvalidDataException("多个会话被映射到了同一个 Thread ID：" + newId);
			}
			string origin = lineageById.TryGetValue(oldId, out BundleSessionLineage lineage) && !string.IsNullOrWhiteSpace(lineage.OriginThreadId) ? lineage.OriginThreadId : oldId;
			string oldBundlePath = (session.bundle_path ?? string.Empty).Replace('\\', '/');
			string newBundlePath = string.Equals(oldId, newId, StringComparison.OrdinalIgnoreCase) ? oldBundlePath : ReplaceIdInPath(oldBundlePath, oldId, newId);
			itemsByPath[oldBundlePath] = new RewriteItem
			{
				Session = session,
				OldId = oldId,
				NewId = newId,
				OriginId = origin,
				OldBundlePath = oldBundlePath,
				NewBundlePath = newBundlePath
			};
			idMap[oldId] = newId;
			session.origin_thread_id = origin;
			session.thread_id = newId;
			session.bundle_path = newBundlePath;
			if (!string.Equals(oldId, newId, StringComparison.OrdinalIgnoreCase))
			{
				session.original_path = ReplaceTextIgnoreCase(session.original_path, oldId, newId);
			}
		}
		string outputDirectory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
		if (!string.IsNullOrWhiteSpace(outputDirectory))
		{
			Directory.CreateDirectory(outputDirectory);
		}
		if (File.Exists(outputPath))
		{
			File.Delete(outputPath);
		}
		HashSet<string> rewrittenEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		using (ZipArchive source = ZipFile.OpenRead(inputPath))
		using (ZipArchive destination = ZipFile.Open(outputPath, ZipArchiveMode.Create))
		{
			Dictionary<string, string> checksums = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			foreach (ZipArchiveEntry entry in source.Entries)
			{
				string entryPath = entry.FullName.Replace('\\', '/');
				if (string.Equals(entryPath, "manifest.json", StringComparison.OrdinalIgnoreCase) || string.Equals(entryPath, "checksums.json", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				byte[] data = ReadEntry(entry);
				string destinationPath = entryPath;
				if (itemsByPath.TryGetValue(entryPath, out RewriteItem item))
				{
					data = RewriteSessionMetadata(data, idMap, item.OldId, item.NewId, item.OriginId);
					destinationPath = item.NewBundlePath;
					item.Session.size_bytes = data.LongLength;
					item.Session.sha256 = Sha256(data);
					rewrittenEntries.Add(entryPath);
				}
				checksums[destinationPath] = Sha256(data);
				WriteEntry(destination, destinationPath, data);
			}
			if (rewrittenEntries.Count != itemsByPath.Count)
			{
				throw new InvalidDataException("对话包清单与实际会话文件不一致，无法安全重写编号。");
			}
			byte[] manifestBytes = Encoding.UTF8.GetBytes(CctRunner.NewSerializer().Serialize(manifest));
			checksums["manifest.json"] = Sha256(manifestBytes);
			WriteEntry(destination, "manifest.json", manifestBytes);
			byte[] checksumBytes = Encoding.UTF8.GetBytes(CctRunner.NewSerializer().Serialize(checksums));
			WriteEntry(destination, "checksums.json", checksumBytes);
		}
		return new FreshIdRewriteResult
		{
			RewrittenCount = idMap.Count(pair => !string.Equals(pair.Key, pair.Value, StringComparison.OrdinalIgnoreCase)),
			IdMap = idMap
		};
	}

	private static CctBundleManifest ReadManifest(string bundlePath)
	{
		using (ZipArchive archive = ZipFile.OpenRead(bundlePath))
		{
			ZipArchiveEntry entry = archive.GetEntry("manifest.json");
			if (entry == null)
			{
				throw new InvalidDataException("对话包缺少 manifest.json。");
			}
			using (StreamReader reader = new StreamReader(entry.Open(), Encoding.UTF8))
			{
				CctBundleManifest manifest = CctRunner.NewSerializer().Deserialize<CctBundleManifest>(reader.ReadToEnd());
				if (manifest == null || manifest.sessions == null)
				{
					throw new InvalidDataException("对话包清单中没有 sessions。");
				}
				return manifest;
			}
		}
	}

	private static string ReplaceIdInPath(string path, string oldId, string newId)
	{
		string replaced = ReplaceTextIgnoreCase(path, oldId, newId);
		if (!string.Equals(replaced, path, StringComparison.Ordinal))
		{
			return replaced;
		}
		string extension = Path.GetExtension(path);
		return path.Substring(0, path.Length - extension.Length) + "-" + newId + extension;
	}

	private static string ReplaceTextIgnoreCase(string value, string oldText, string newText)
	{
		if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(oldText))
		{
			return value;
		}
		return Regex.Replace(value, Regex.Escape(oldText), newText.Replace("$", "$$"), RegexOptions.IgnoreCase);
	}

	private static Dictionary<string, object> ReadPayload(byte[] data, string threadId)
	{
		int newline = Array.IndexOf(data, (byte)10);
		if (newline < 0)
		{
			throw new InvalidDataException("会话文件缺少首行 session_meta：" + threadId);
		}
		int lineEnd = newline > 0 && data[newline - 1] == 13 ? newline - 1 : newline;
		string firstLine = Encoding.UTF8.GetString(data, 0, lineEnd);
		Dictionary<string, object> root = CctRunner.NewSerializer().DeserializeObject(firstLine) as Dictionary<string, object>;
		if (root == null || !root.TryGetValue("payload", out object value) || !(value is Dictionary<string, object> payload))
		{
			throw new InvalidDataException("会话首行缺少 payload：" + threadId);
		}
		return payload;
	}

	private static byte[] RewriteSessionMetadata(byte[] data, IDictionary<string, string> idMap, string oldId, string newId, string originId)
	{
		int newline = Array.IndexOf(data, (byte)10);
		if (newline < 0)
		{
			throw new InvalidDataException("会话文件缺少首行 session_meta：" + oldId);
		}
		int lineEnd = newline > 0 && data[newline - 1] == 13 ? newline - 1 : newline;
		string firstLine = Encoding.UTF8.GetString(data, 0, lineEnd);
		Dictionary<string, object> root = CctRunner.NewSerializer().DeserializeObject(firstLine) as Dictionary<string, object>;
		if (root == null || !root.TryGetValue("payload", out object value) || !(value is Dictionary<string, object> payload))
		{
			throw new InvalidDataException("会话首行缺少 payload：" + oldId);
		}
		ReplaceMappedStrings(payload, idMap);
		payload["id"] = newId;
		if (payload.ContainsKey("thread_id"))
		{
			payload["thread_id"] = newId;
		}
		payload[ConversationLineage.OriginThreadIdKey] = originId;
		byte[] firstLineBytes = Encoding.UTF8.GetBytes(CctRunner.NewSerializer().Serialize(root));
		using (MemoryStream output = new MemoryStream(firstLineBytes.Length + data.Length - lineEnd))
		{
			output.Write(firstLineBytes, 0, firstLineBytes.Length);
			output.Write(data, lineEnd, data.Length - lineEnd);
			return output.ToArray();
		}
	}

	private static void ReplaceMappedStrings(object value, IDictionary<string, string> idMap)
	{
		if (value is Dictionary<string, object> dictionary)
		{
			foreach (string key in dictionary.Keys.ToList())
			{
				if (string.Equals(key, ConversationLineage.OriginThreadIdKey, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				object child = dictionary[key];
				if (child is string text && idMap.TryGetValue(text, out string mapped))
				{
					dictionary[key] = mapped;
				}
				else
				{
					ReplaceMappedStrings(child, idMap);
				}
			}
		}
		else if (value is object[] array)
		{
			for (int index = 0; index < array.Length; index++)
			{
				if (array[index] is string text && idMap.TryGetValue(text, out string mapped))
				{
					array[index] = mapped;
				}
				else
				{
					ReplaceMappedStrings(array[index], idMap);
				}
			}
		}
	}

	private static byte[] ReadEntry(ZipArchiveEntry entry)
	{
		using (Stream stream = entry.Open())
		using (MemoryStream output = new MemoryStream())
		{
			stream.CopyTo(output);
			return output.ToArray();
		}
	}

	private static void WriteEntry(ZipArchive archive, string path, byte[] data)
	{
		ZipArchiveEntry entry = archive.CreateEntry(path.Replace('\\', '/'), CompressionLevel.Optimal);
		using (Stream stream = entry.Open())
		{
			stream.Write(data, 0, data.Length);
		}
	}

	private static string Sha256(byte[] data)
	{
		using (SHA256 sha = SHA256.Create())
		{
			byte[] hash = sha.ComputeHash(data);
			StringBuilder value = new StringBuilder(hash.Length * 2);
			foreach (byte item in hash)
			{
				value.Append(item.ToString("x2"));
			}
			return value.ToString();
		}
	}
}
