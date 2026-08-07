using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace CodexConversationMigrator;

internal static class ConversationLineageSelfTest
{
	public static void Run(string root)
	{
		Directory.CreateDirectory(root);
		string originalHome = Environment.GetEnvironmentVariable("CODEX_HOME");
		try
		{
			string sourceProject = Path.Combine(root, "source-project");
			string targetProject = Path.Combine(root, "target-project");
			Directory.CreateDirectory(sourceProject);
			Directory.CreateDirectory(targetProject);
			string mainId = "11111111-1111-4111-8111-111111111111";
			string childId = "22222222-2222-4222-8222-222222222222";
			string sourceFiles = Path.Combine(root, "source-files");
			Directory.CreateDirectory(sourceFiles);
			SessionInfo main = CreateSession(sourceFiles, mainId, mainId, sourceProject, false);
			SessionInfo child = CreateSession(sourceFiles, childId, mainId, sourceProject, true);
			string sourceBundle = Path.Combine(root, "source.codexbundle");
			ExactBundleWriter.CreateBundle(new[] { main, child }, sourceBundle, null);

			string freshOne = Path.Combine(root, "fresh-one.codexbundle");
			string freshTwo = Path.Combine(root, "fresh-two.codexbundle");
			FreshIdRewriteResult first = BundleFreshIdRewriter.RewriteAsFresh(sourceBundle, freshOne);
			FreshIdRewriteResult second = BundleFreshIdRewriter.RewriteAsFresh(sourceBundle, freshTwo);
			if (first.IdMap.Count != 2 || second.IdMap.Count != 2 ||
				string.Equals(first.IdMap[mainId], second.IdMap[mainId], StringComparison.OrdinalIgnoreCase) ||
				string.Equals(first.IdMap[childId], second.IdMap[childId], StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidOperationException("independent copy did not generate a fresh ID set on every run");
			}
		AssertParentChildRewrite(freshOne, mainId, childId, first.IdMap[mainId], first.IdMap[childId]);

			string plannerHome = Path.Combine(root, "planner-home");
			Environment.SetEnvironmentVariable("CODEX_HOME", plannerHome);
			string localMainId = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
			WriteLocalSession(plannerHome, localMainId, mainId, targetProject, "local-main.jsonl");
			ConversationImportPlanner planner = new ConversationImportPlanner(plannerHome);
			ConversationImportPlan mergePlan = planner.CreatePlan(sourceBundle, Path.Combine(root, "smart-merge.codexbundle"), targetProject, false);
			if (mergePlan.MatchedCount != 1 || mergePlan.CreatedCount != 1 ||
				!string.Equals(mergePlan.IdMap[mainId], localMainId, StringComparison.OrdinalIgnoreCase) ||
				string.Equals(mergePlan.IdMap[childId], childId, StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidOperationException("project-scoped origin ID merge planning failed");
			}
			AssertParentChildRewrite(mergePlan.EffectiveBundlePath, mainId, childId, localMainId, mergePlan.IdMap[childId]);

			ConversationImportPlan copyPlanOne = planner.CreatePlan(sourceBundle, Path.Combine(root, "copy-one.codexbundle"), targetProject, true);
			ConversationImportPlan copyPlanTwo = planner.CreatePlan(sourceBundle, Path.Combine(root, "copy-two.codexbundle"), targetProject, true);
			if (copyPlanOne.CreatedCount != 2 || copyPlanTwo.CreatedCount != 2 ||
				copyPlanOne.IdMap.Any(pair => string.Equals(pair.Key, pair.Value, StringComparison.OrdinalIgnoreCase)) ||
				copyPlanOne.IdMap.Any(pair => string.Equals(pair.Value, copyPlanTwo.IdMap[pair.Key], StringComparison.OrdinalIgnoreCase)))
			{
				throw new InvalidOperationException("planner independent-copy isolation failed");
			}

			string ambiguousHome = Path.Combine(root, "ambiguous-home");
			Environment.SetEnvironmentVariable("CODEX_HOME", ambiguousHome);
			WriteLocalSession(ambiguousHome, "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb", mainId, targetProject, "one.jsonl");
			WriteLocalSession(ambiguousHome, "cccccccc-cccc-4ccc-8ccc-cccccccccccc", mainId, targetProject, "two.jsonl");
			bool ambiguityBlocked = false;
			try
			{
				new ConversationImportPlanner(ambiguousHome).CreatePlan(sourceBundle, Path.Combine(root, "ambiguous.codexbundle"), targetProject, false);
			}
			catch (InvalidOperationException ex)
			{
				ambiguityBlocked = ex.Message.Contains("多个");
			}
			if (!ambiguityBlocked)
			{
				throw new InvalidOperationException("ambiguous origin ID merge was not blocked");
			}
		}
		finally
		{
			Environment.SetEnvironmentVariable("CODEX_HOME", originalHome);
		}
	}

	private static SessionInfo CreateSession(string directory, string currentId, string sessionId, string cwd, bool subagent)
	{
		string path = Path.Combine(directory, currentId + ".jsonl");
		WriteSessionFile(path, currentId, sessionId, currentId, cwd, subagent);
		return new SessionInfo
		{
			ThreadId = currentId,
			OriginThreadId = currentId,
			SessionPath = path,
			RelativePath = "2026/08/05/" + Path.GetFileName(path),
			Cwd = cwd,
			Title = subagent ? "child" : "main",
			Preview = subagent ? "child" : "main",
			Source = subagent ? "subagent" : "app",
			CreatedAt = "2026-08-05T01:00:00Z",
			UpdatedAt = "2026-08-05T01:01:00Z",
			UpdatedDate = new DateTime(2026, 8, 5, 1, 1, 0, DateTimeKind.Utc),
			IsSubagent = subagent,
			ParentThreadId = subagent ? sessionId : string.Empty
		};
	}

	private static void WriteLocalSession(string codexHome, string currentId, string originId, string cwd, string fileName)
	{
		string directory = Path.Combine(codexHome, "sessions", "2026", "08", "05");
		Directory.CreateDirectory(directory);
		WriteSessionFile(Path.Combine(directory, fileName), currentId, currentId, originId, cwd, false);
	}

	private static void WriteSessionFile(string path, string currentId, string sessionId, string originId, string cwd, bool subagent)
	{
		Dictionary<string, object> payload = new Dictionary<string, object>
		{
			{ "id", currentId },
			{ "session_id", sessionId },
			{ "timestamp", "2026-08-05T01:00:00Z" },
			{ "cwd", cwd },
			{ "source", subagent ? "subagent" : "app" },
			{ "thread_source", subagent ? "subagent" : "app" },
			{ ConversationLineage.OriginThreadIdKey, originId }
		};
		Dictionary<string, object> root = new Dictionary<string, object>
		{
			{ "timestamp", "2026-08-05T01:00:00Z" },
			{ "type", "session_meta" },
			{ "payload", payload }
		};
		Dictionary<string, object> message = new Dictionary<string, object>
		{
			{ "timestamp", "2026-08-05T01:01:00Z" },
			{ "type", "response_item" },
			{ "payload", new Dictionary<string, object> { { "type", "message" }, { "role", "user" }, { "content", new object[0] } } }
		};
		string contents = CctRunner.NewSerializer().Serialize(root) + Environment.NewLine + CctRunner.NewSerializer().Serialize(message) + Environment.NewLine;
		File.WriteAllText(path, contents, new UTF8Encoding(false));
	}

	private static void AssertParentChildRewrite(string bundlePath, string mainOriginId, string childOriginId, string expectedMainId, string expectedChildId)
	{
		using (ZipArchive archive = ZipFile.OpenRead(bundlePath))
		{
			CctBundleManifest manifest;
			using (StreamReader reader = new StreamReader(archive.GetEntry("manifest.json").Open(), Encoding.UTF8))
			{
				manifest = CctRunner.NewSerializer().Deserialize<CctBundleManifest>(reader.ReadToEnd());
			}
			CctBundleSession main = manifest.sessions.Single(item => string.Equals(item.origin_thread_id, mainOriginId, StringComparison.OrdinalIgnoreCase));
			CctBundleSession child = manifest.sessions.Single(item => string.Equals(item.origin_thread_id, childOriginId, StringComparison.OrdinalIgnoreCase));
			if (!string.Equals(main.thread_id, expectedMainId, StringComparison.OrdinalIgnoreCase) || !string.Equals(child.thread_id, expectedChildId, StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidOperationException("rewritten manifest lineage IDs are incorrect");
			}
			Dictionary<string, object> childPayload = ReadPayload(archive.GetEntry(child.bundle_path));
			if (!string.Equals(ConversationLineage.GetString(childPayload, "id"), expectedChildId, StringComparison.OrdinalIgnoreCase) ||
				!string.Equals(ConversationLineage.GetString(childPayload, "session_id"), expectedMainId, StringComparison.OrdinalIgnoreCase) ||
				!string.Equals(ConversationLineage.GetString(childPayload, ConversationLineage.OriginThreadIdKey), childOriginId, StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidOperationException("subagent parent/child IDs were not rewritten together");
			}
		}
	}

	private static Dictionary<string, object> ReadPayload(ZipArchiveEntry entry)
	{
		if (entry == null)
		{
			throw new InvalidDataException("rewritten session entry is missing");
		}
		using (StreamReader reader = new StreamReader(entry.Open(), Encoding.UTF8))
		{
			string firstLine = reader.ReadLine();
			Dictionary<string, object> root = CctRunner.NewSerializer().DeserializeObject(firstLine) as Dictionary<string, object>;
			return (Dictionary<string, object>)root["payload"];
		}
	}
}
