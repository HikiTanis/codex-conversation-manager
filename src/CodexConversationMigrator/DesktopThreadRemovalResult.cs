namespace CodexConversationMigrator;

internal sealed class DesktopThreadRemovalResult
{
	public bool StateFileFound { get; set; }

	public string StatePath { get; set; }

	public string BackupPath { get; set; }

	public int RequestedCount { get; set; }

	public int RemovedReferenceCount { get; set; }
}
