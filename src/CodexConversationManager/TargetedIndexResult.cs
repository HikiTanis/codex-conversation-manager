namespace CodexConversationManager;

internal sealed class TargetedIndexResult
{
	public string DatabasePath { get; set; }

	public string BackupPath { get; set; }

	public string BackfillState { get; set; }

	public int InsertedCount { get; set; }

	public int UpdatedCount { get; set; }

	public int IndexedCount { get; set; }

	public int VisibilityVerifiedCount { get; set; }

	public int PaginatedCount { get; set; }

	public bool DesktopStateFound { get; set; }

	public int DesktopAssignmentExpectedCount { get; set; }

	public int DesktopAssignmentVerifiedCount { get; set; }

	public int DesktopProjectCount { get; set; }

	public string DesktopStateBackupPath { get; set; }
}
