using System.Collections.Generic;

namespace CodexConversationManager;

internal sealed class ProjectDefinition
{
	public string Id { get; set; }

	public string Name { get; set; }

	public List<string> RootPaths { get; set; }

	public int SortIndex { get; set; }
}
