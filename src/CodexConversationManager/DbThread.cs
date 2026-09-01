using System;

namespace CodexConversationManager;

internal sealed class DbThread
{
	public string Id { get; set; }

	public string Cwd { get; set; }
	public string RawCwd { get; set; }

	public string RolloutPath { get; set; }


	public string Title { get; set; }

	public string Source { get; set; }

	public string ThreadSource { get; set; }
	public string HistoryMode { get; set; }


	public string ParentThreadId { get; set; }

	public bool Archived { get; set; }

	public long UpdatedAtMilliseconds { get; set; }

	public bool IsSubagent
	{
		get
		{
			string source = (Source ?? string.Empty).Trim();
			return string.Equals(ThreadSource, "subagent", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(source, "subagent", StringComparison.OrdinalIgnoreCase) ||
				source.IndexOf("\"subagent\"", StringComparison.OrdinalIgnoreCase) >= 0;
		}
	}
}
