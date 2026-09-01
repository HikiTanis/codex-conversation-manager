namespace CodexConversationMigrator;

internal sealed class PackSession
{
	public string thread_id { get; set; }

	public string origin_thread_id { get; set; }

	public string title { get; set; }

	public string preview { get; set; }

	public string source { get; set; }

	public string updated_at { get; set; }

	public bool archived { get; set; }

	public bool compressed { get; set; }

	public bool is_subagent { get; set; }

	public string bundle_file { get; set; }

	public string project_key { get; set; }
}
