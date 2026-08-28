using System;

namespace CodexConversationMigrator;

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
			if (!string.Equals(ThreadSource, "subagent", StringComparison.OrdinalIgnoreCase))
			{
				return (Source ?? string.Empty).IndexOf("\"subagent\"", StringComparison.OrdinalIgnoreCase) >= 0;
			}
			return true;
		}
	}
}
