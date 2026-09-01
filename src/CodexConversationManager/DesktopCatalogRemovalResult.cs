namespace CodexConversationManager;

internal sealed class DesktopCatalogRemovalResult
{
	public string DatabasePath { get; set; }

	public string BackupPath { get; set; }

	public int RequestedCount { get; set; }

	public int RemovedCatalogEntryCount { get; set; }

	public int RemovedTimelineEntryCount { get; set; }
}
