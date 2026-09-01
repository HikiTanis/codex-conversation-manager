using System.Collections.Generic;

namespace CodexConversationManager;

internal sealed class ConversationImportPlan
{
	public string SourceBundlePath { get; set; }

	public string EffectiveBundlePath { get; set; }

	public string TargetPath { get; set; }

	public Dictionary<string, string> IdMap { get; set; }

	public int MatchedCount { get; set; }

	public int CreatedCount { get; set; }
}
