using System.Collections.Generic;

namespace CodexConversationMigrator;

internal sealed class ConversationReadResult
{
	public List<ConversationMessage> Messages { get; set; }

	public string SessionPath { get; set; }
}
