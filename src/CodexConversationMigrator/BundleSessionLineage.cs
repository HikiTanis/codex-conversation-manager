namespace CodexConversationMigrator;

internal sealed class BundleSessionLineage
{
	public string CurrentThreadId { get; set; }

	public string OriginThreadId { get; set; }

	public string OriginalCwd { get; set; }
}
