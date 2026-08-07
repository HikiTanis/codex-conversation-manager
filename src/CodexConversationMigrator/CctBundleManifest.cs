using System.Collections.Generic;

namespace CodexConversationMigrator;

internal sealed class CctBundleManifest
{
	public string format_version { get; set; }

	public string created_at { get; set; }

	public string created_by_device { get; set; }

	public string source_os { get; set; }

	public string source_codex_home { get; set; }

	public string codex_version { get; set; }

	public List<CctBundleSession> sessions { get; set; }
}
