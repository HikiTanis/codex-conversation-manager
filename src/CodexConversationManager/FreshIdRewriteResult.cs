using System.Collections.Generic;

namespace CodexConversationManager;

internal sealed class FreshIdRewriteResult
{
	public int RewrittenCount { get; set; }

	public Dictionary<string, string> IdMap { get; set; }
}
