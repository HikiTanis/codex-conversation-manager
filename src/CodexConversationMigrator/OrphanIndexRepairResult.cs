using System;
using System.Collections.Generic;
using System.Linq;

namespace CodexConversationMigrator;

internal sealed class OrphanIndexRepairResult
{
	public int DetectedCount { get; set; }

	public int RepairedCount { get; set; }

	public bool DesktopRunning { get; set; }

	public string IndexBackupPath { get; set; }

	public string DesktopStateBackupPath { get; set; }

	public string DesktopCatalogBackupPath { get; set; }

	public int RemovedDesktopCatalogCount { get; set; }

	public int ClearedDesktopCacheCount { get; set; }
}

internal sealed class LiveDescendantInfo
{
	public string RootThreadId { get; set; }

	public string ThreadId { get; set; }

	public string Title { get; set; }

	public string Cwd { get; set; }

	public string RolloutPath { get; set; }
}

internal sealed class LiveDescendantRepairException : InvalidOperationException
{
	public LiveDescendantRepairException(string message, IEnumerable<LiveDescendantInfo> descendants)
		: base(message)
	{
		Descendants = (descendants ?? Enumerable.Empty<LiveDescendantInfo>()).ToList();
	}

	public List<LiveDescendantInfo> Descendants { get; }
}
