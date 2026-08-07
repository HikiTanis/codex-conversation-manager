namespace CodexConversationMigrator;

internal sealed class ConversationMessage
{
	public string RoleLabel { get; set; }

	public string Text { get; set; }

	public string DisplayTime { get; set; }

	public bool IsUser { get; set; }

	public bool IsNotice { get; set; }
}
