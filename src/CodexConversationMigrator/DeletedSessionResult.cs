namespace CodexConversationMigrator;

internal sealed class DeletedSessionResult
{
	public string OriginalPath { get; set; }

	public string BackupPath { get; set; }

	public bool PermanentlyDeleted { get; set; }
}
