using System.Collections.Generic;

namespace CodexConversationMigrator;

internal sealed class CatalogResult
{
	public List<ProjectGroup> Projects { get; set; }

	public int MainCount { get; set; }

	public int InternalCount { get; set; }

	public bool UsedCodexIndex { get; set; }

	public string Diagnostic { get; set; }
}
