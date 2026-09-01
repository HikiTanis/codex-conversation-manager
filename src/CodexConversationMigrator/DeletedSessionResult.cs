using System.Collections.Generic;

namespace CodexConversationMigrator;

internal sealed class DeletedSessionResult
{
	public string OriginalPath { get; set; }

	public string BackupPath { get; set; }

	public bool PermanentlyDeleted { get; set; }

	public List<string> BackupPaths { get; set; } = new List<string>();

	public int AffectedConversationCount { get; set; } = 1;
}
