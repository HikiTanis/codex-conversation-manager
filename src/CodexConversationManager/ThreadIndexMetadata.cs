namespace CodexConversationManager;

internal sealed class ThreadIndexMetadata
{
	public string Id { get; set; }

	public string RolloutPath { get; set; }

	public long CreatedAtSeconds { get; set; }

	public long UpdatedAtSeconds { get; set; }

	public long CreatedAtMilliseconds { get; set; }

	public long UpdatedAtMilliseconds { get; set; }

	public string Source { get; set; }

	public string HistoryMode { get; set; } = CodexHistoryMode.Legacy;

	public string ThreadSource { get; set; }

	public string ParentThreadId { get; set; }

	public string ModelProvider { get; set; }

	public string Cwd { get; set; }

	public string CliVersion { get; set; }

	public string Title { get; set; }

	public string Preview { get; set; }

	public string FirstUserMessage { get; set; }

	public bool HasUserEvent { get; set; }

	public bool Archived { get; set; }

	public string GitSha { get; set; }

	public string GitBranch { get; set; }

	public string GitOriginUrl { get; set; }
}
