namespace CodexConversationManager;

internal sealed class DesktopProjectRegistrationResult
{
	public bool StateFileFound { get; set; }

	public string StatePath { get; set; }

	public string BackupPath { get; set; }

	public int ExpectedThreadCount { get; set; }

	public int VerifiedThreadCount { get; set; }

	public int RegisteredProjectCount { get; set; }
}
