namespace CodexConversationMigrator;

internal sealed class ConversationLineageRecord
{
	public string CurrentThreadId { get; set; }

	public string OriginThreadId { get; set; }

	public string Cwd { get; set; }

	public string SessionPath { get; set; }
}
