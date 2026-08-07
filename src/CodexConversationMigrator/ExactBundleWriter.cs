using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace CodexConversationMigrator;

internal static class ExactBundleWriter
{
	public static void CreateSingleSessionBundle(SessionInfo session, string outputPath)
	{
		CreateBundle(new SessionInfo[1] { session }, outputPath, null);
	}

	public static void CreateBundle(IEnumerable<SessionInfo> sourceSessions, string outputPath, Action<int, int, SessionInfo> progress)
	{
		if (sourceSessions == null)
		{
			throw new ArgumentNullException("sourceSessions");
		}
		List<SessionInfo> list = (from x in sourceSessions.Where((SessionInfo x) => x != null && !string.IsNullOrWhiteSpace(x.ThreadId)).GroupBy((SessionInfo x) => x.ThreadId, StringComparer.OrdinalIgnoreCase)
			select x.First()).ToList();
		if (list.Count == 0)
		{
			throw new InvalidOperationException("没有可封装的会话。");
		}
		string text = CodexCatalog.ResolveCodexHome();
		List<CctBundleSession> list2 = new List<CctBundleSession>();
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		int num = 0;
		foreach (SessionInfo item in list)
		{
			num++;
			progress?.Invoke(num, list.Count, item);
			string text2 = TextHelpers.StripExtendedPrefix(item.SessionPath);
			if (!File.Exists(text2) && File.Exists(item.SessionPath))
			{
				text2 = item.SessionPath;
			}
			if (string.IsNullOrWhiteSpace(text2) || !File.Exists(text2))
			{
				throw new FileNotFoundException("找不到该对话对应的 rollout 文件。", text2);
			}
			string text3 = item.RelativePath;
			if (string.IsNullOrWhiteSpace(text3))
			{
				text3 = DeriveRelativePath(text2, text);
			}
			text3 = text3.Replace('\\', '/').TrimStart('/');
			string text4 = "sessions/" + text3;
			if (dictionary.ContainsKey(text4))
			{
				text4 = "sessions/" + DateTime.Now.ToString("yyyy/MM/dd/") + item.ThreadId + "-" + Path.GetFileName(text2);
			}
			string sha = Sha256File(text2);
			FileInfo fileInfo = new FileInfo(text2);
			list2.Add(new CctBundleSession
			{
				thread_id = item.ThreadId,
				origin_thread_id = string.IsNullOrWhiteSpace(item.OriginThreadId) ? item.ThreadId : item.OriginThreadId,
				original_path = text2,
				bundle_path = text4,
				original_cwd = item.Cwd,
				preview = TextHelpers.CleanLine(item.Preview, 240, item.DisplayTitle),
				first_user_message = item.DisplayTitle,
				created_at = NormalizeUtc(item.CreatedAt, fileInfo.CreationTimeUtc),
				updated_at = ((item.UpdatedDate == DateTime.MinValue) ? fileInfo.LastWriteTimeUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'") : item.UpdatedDate.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'")),
				source = FriendlyManifestSource(item.Source),
				model_provider = (string.IsNullOrWhiteSpace(item.ModelProvider) ? "openai" : item.ModelProvider),
				archived = item.Archived,
				compressed = (item.Compressed || text2.EndsWith(".zst", StringComparison.OrdinalIgnoreCase)),
				size_bytes = fileInfo.Length,
				sha256 = sha
			});
			dictionary[text4] = text2;
		}
		CctBundleManifest cctBundleManifest = new CctBundleManifest();
		cctBundleManifest.format_version = "codex-sync-bundle-v1";
		cctBundleManifest.created_at = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
		cctBundleManifest.created_by_device = Environment.MachineName;
		cctBundleManifest.source_os = "windows";
		cctBundleManifest.source_codex_home = text;
		cctBundleManifest.codex_version = list.Select((SessionInfo x) => x.CliVersion).FirstOrDefault((string x) => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
		cctBundleManifest.sessions = list2;
		CctBundleManifest obj = cctBundleManifest;
		string text5 = Path.Combine(Path.GetTempPath(), "codex-exact-bundle-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text5);
		try
		{
			string text6 = Path.Combine(text5, "manifest.json");
			string contents = CctRunner.NewSerializer().Serialize(obj);
			File.WriteAllText(text6, contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			string value = Sha256File(text6);
			Dictionary<string, string> dictionary2 = new Dictionary<string, string>();
			dictionary2.Add("manifest.json", value);
			Dictionary<string, string> dictionary3 = dictionary2;
			foreach (CctBundleSession item2 in list2)
			{
				dictionary3[item2.bundle_path] = item2.sha256;
			}
			string text7 = Path.Combine(text5, "checksums.json");
			File.WriteAllText(text7, CctRunner.NewSerializer().Serialize(dictionary3), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			if (File.Exists(outputPath))
			{
				File.Delete(outputPath);
			}
			using ZipArchive destination = ZipFile.Open(outputPath, ZipArchiveMode.Create);
			foreach (KeyValuePair<string, string> item3 in dictionary)
			{
				destination.CreateEntryFromFile(item3.Value, item3.Key, CompressionLevel.Optimal);
			}
			destination.CreateEntryFromFile(text6, "manifest.json", CompressionLevel.Optimal);
			destination.CreateEntryFromFile(text7, "checksums.json", CompressionLevel.Optimal);
		}
		finally
		{
			try
			{
				if (Directory.Exists(text5))
				{
					Directory.Delete(text5, recursive: true);
				}
			}
			catch
			{
			}
		}
	}

	private static string DeriveRelativePath(string sourcePath, string codexHome)
	{
		string text = Path.Combine(codexHome, "sessions").TrimEnd('\\') + "\\";
		string text2 = Path.Combine(codexHome, "archived_sessions").TrimEnd('\\') + "\\";
		if (sourcePath.StartsWith(text, StringComparison.OrdinalIgnoreCase))
		{
			return sourcePath.Substring(text.Length);
		}
		if (sourcePath.StartsWith(text2, StringComparison.OrdinalIgnoreCase))
		{
			return sourcePath.Substring(text2.Length);
		}
		return DateTime.Now.ToString("yyyy/MM/dd/") + Path.GetFileName(sourcePath);
	}

	private static string FriendlyManifestSource(string source)
	{
		string text = (source ?? string.Empty).Trim();
		if (text.Length == 0)
		{
			return "app";
		}
		if (!text.StartsWith("{"))
		{
			return text;
		}
		if (text.IndexOf("subagent", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return "subagent";
		}
		if (text.IndexOf("vscode", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return "vscode";
		}
		if (text.IndexOf("cli", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return "cli";
		}
		return "app";
	}

	private static string NormalizeUtc(string value, DateTime fallbackUtc)
	{
		if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var result))
		{
			return result.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
		}
		return fallbackUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
	}

	private static string Sha256File(string path)
	{
		using SHA256 sHA = SHA256.Create();
		using FileStream inputStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
		byte[] array = sHA.ComputeHash(inputStream);
		StringBuilder stringBuilder = new StringBuilder(array.Length * 2);
		byte[] array2 = array;
		foreach (byte b in array2)
		{
			stringBuilder.Append(b.ToString("x2"));
		}
		return stringBuilder.ToString();
	}
}
