using System.Collections.Generic;

namespace CodexConversationMigrator;

internal sealed class PackProject
{
	public string project_key { get; set; }

	public string source_project { get; set; }

	public string source_project_name { get; set; }

	public string target_folder { get; set; }

	public List<string> bundles { get; set; }

	public ProjectPayloadInfo project_payload { get; set; }
}
