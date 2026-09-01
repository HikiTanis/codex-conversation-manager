using System.Collections.Generic;

namespace CodexConversationManager;

internal sealed class ConversationReadResult
{
	public List<ConversationMessage> Messages { get; set; }

	public string SessionPath { get; set; }
}
