namespace CodexConversationMigrator;

internal sealed class OrphanIndexRepairResult
{
	public int DetectedCount { get; set; }

	public int RepairedCount { get; set; }

	public bool DesktopRunning { get; set; }

	public string IndexBackupPath { get; set; }

	public string DesktopStateBackupPath { get; set; }
}
