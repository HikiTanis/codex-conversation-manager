using System.Collections.Generic;

namespace CodexConversationMigrator;

internal sealed class FreshIdRewriteResult
{
	public int RewrittenCount { get; set; }

	public Dictionary<string, string> IdMap { get; set; }
}
