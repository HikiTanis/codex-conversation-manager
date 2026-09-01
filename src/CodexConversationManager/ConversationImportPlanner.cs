using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CodexConversationManager;

internal sealed class ConversationImportPlanner
{
	private readonly List<ConversationLineageRecord> localRecords;

	private readonly HashSet<string> reservedThreadIds;

	public ConversationImportPlanner(string codexHome)
	{
		localRecords = ReadLocalRecords(codexHome);
		reservedThreadIds = new HashSet<string>(localRecords.Select(item => item.CurrentThreadId).Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.OrdinalIgnoreCase);
	}

	public ConversationImportPlan CreatePlan(string sourceBundlePath, string outputBundlePath, string targetPath, bool independentCopy)
	{
		List<BundleSessionLineage> sessions = BundleFreshIdRewriter.ReadLineages(sourceBundlePath);
		Dictionary<string, string> idMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		int matched = 0;
		int created = 0;
		foreach (BundleSessionLineage session in sessions)
		{
			if (string.IsNullOrWhiteSpace(session.CurrentThreadId))
			{
				throw new InvalidDataException("对话包包含空的 Thread ID。");
			}
			string desiredProject = string.IsNullOrWhiteSpace(targetPath) ? session.OriginalCwd : targetPath;
			string finalId;
			if (independentCopy)
			{
				finalId = NewThreadId();
				created++;
			}
			else
			{
				List<ConversationLineageRecord> candidates = FindCandidates(session.OriginThreadId, desiredProject);
				ConversationLineageRecord exactCurrent = candidates.FirstOrDefault(item => string.Equals(item.CurrentThreadId, session.CurrentThreadId, StringComparison.OrdinalIgnoreCase));
				if (exactCurrent != null)
				{
					finalId = exactCurrent.CurrentThreadId;
					matched++;
				}
				else if (candidates.Count == 1)
				{
					finalId = candidates[0].CurrentThreadId;
					matched++;
				}
				else if (candidates.Count > 1)
				{
					throw new InvalidOperationException("目标项目中存在多个具有相同原始编号的独立副本，无法判断应合并到哪一个。请改用“独立复制”，或先删除不需要的副本。");
				}
				else
				{
					finalId = NewThreadId();
					created++;
				}
			}
			idMap[session.CurrentThreadId] = finalId;
			if (!localRecords.Any(item => string.Equals(item.CurrentThreadId, finalId, StringComparison.OrdinalIgnoreCase)))
			{
				localRecords.Add(new ConversationLineageRecord
				{
					CurrentThreadId = finalId,
					OriginThreadId = session.OriginThreadId,
					Cwd = desiredProject,
					SessionPath = outputBundlePath
				});
			}
		}
		FreshIdRewriteResult rewrite = BundleFreshIdRewriter.RewriteWithIdMap(sourceBundlePath, outputBundlePath, idMap);
		return new ConversationImportPlan
		{
			SourceBundlePath = sourceBundlePath,
			EffectiveBundlePath = outputBundlePath,
			TargetPath = targetPath,
			IdMap = rewrite.IdMap,
			MatchedCount = matched,
			CreatedCount = created
		};
	}

	private List<ConversationLineageRecord> FindCandidates(string originThreadId, string desiredProject)
	{
		IEnumerable<ConversationLineageRecord> candidates = localRecords.Where(item => string.Equals(item.OriginThreadId, originThreadId, StringComparison.OrdinalIgnoreCase));
		if (!string.IsNullOrWhiteSpace(desiredProject))
		{
			string canonical = TextHelpers.CanonicalPath(desiredProject);
			candidates = candidates.Where(item => string.Equals(TextHelpers.CanonicalPath(item.Cwd), canonical, StringComparison.OrdinalIgnoreCase));
		}
		return candidates.GroupBy(item => item.CurrentThreadId, StringComparer.OrdinalIgnoreCase).Select(group => group.First()).ToList();
	}

	private string NewThreadId()
	{
		string value;
		do
		{
			value = Guid.NewGuid().ToString("D");
		}
		while (!reservedThreadIds.Add(value));
		return value;
	}

	private static List<ConversationLineageRecord> ReadLocalRecords(string codexHome)
	{
		Dictionary<string, string> indexedCwds;
		try
		{
			indexedCwds = CodexCatalog.ReadIndexedThreadCwds();
		}
		catch
		{
			indexedCwds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		}
		List<ConversationLineageRecord> records = new List<ConversationLineageRecord>();
		foreach (string path in TargetedThreadIndexer.SnapshotSessionFiles(codexHome))
		{
			if (!ConversationLineage.TryReadPayload(path, out Dictionary<string, object> payload))
			{
				continue;
			}
			string currentId = ConversationLineage.ResolveCurrentThreadId(payload, string.Empty);
			if (string.IsNullOrWhiteSpace(currentId))
			{
				continue;
			}
			string cwd = ConversationLineage.GetString(payload, "cwd");
			if (indexedCwds.TryGetValue(currentId, out string indexedCwd) && !string.IsNullOrWhiteSpace(indexedCwd))
			{
				cwd = indexedCwd;
			}
			records.Add(new ConversationLineageRecord
			{
				CurrentThreadId = currentId,
				OriginThreadId = ConversationLineage.ResolveOriginThreadId(payload, currentId),
				Cwd = TextHelpers.StripExtendedPrefix(cwd),
				SessionPath = path
			});
		}
		return records;
	}
}
