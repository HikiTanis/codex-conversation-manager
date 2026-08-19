namespace CodexConversationMigrator;

internal sealed class ThreadIndexRemovalResult
{
	public string DatabasePath { get; set; }

	public string BackupPath { get; set; }

	public int RequestedCount { get; set; }

	public int RemovedThreadCount { get; set; }

	public int RemovedEdgeCount { get; set; }
}
