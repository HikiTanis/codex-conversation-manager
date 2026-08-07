namespace CodexConversationMigrator;

internal sealed class CctResult
{
	public int ExitCode { get; set; }

	public string StdOut { get; set; }

	public string StdErr { get; set; }

	public string CommandLine { get; set; }
}
