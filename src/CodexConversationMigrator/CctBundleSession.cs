namespace CodexConversationMigrator;

internal sealed class CctBundleSession
{
	public string thread_id { get; set; }

	public string origin_thread_id { get; set; }

	public string original_path { get; set; }

	public string bundle_path { get; set; }

	public string original_cwd { get; set; }

	public string preview { get; set; }

	public string first_user_message { get; set; }

	public string created_at { get; set; }

	public string updated_at { get; set; }

	public string source { get; set; }

	public string model_provider { get; set; }

	public bool archived { get; set; }

	public bool compressed { get; set; }

	public long size_bytes { get; set; }

	public string sha256 { get; set; }
}
