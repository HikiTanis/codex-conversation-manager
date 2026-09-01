namespace CodexConversationManager;

internal sealed class ConversationMessage
{
	private string text;

	public string RoleLabel { get; set; }

	public string Text
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(DeferredPath) && DeferredLength > 0)
			{
				return ConversationReader.ReadDeferredText(this);
			}
			return text ?? string.Empty;
		}
		set
		{
			text = value ?? string.Empty;
			DeferredPath = string.Empty;
			DeferredOffset = 0L;
			DeferredLength = 0;
		}
	}

	public string DisplayTime { get; set; }

	public bool IsUser { get; set; }

	public bool IsNotice { get; set; }

	internal string DeferredPath { get; private set; }

	internal long DeferredOffset { get; private set; }

	internal int DeferredLength { get; private set; }

	internal void SetDeferredText(string path, long offset, int length)
	{
		text = string.Empty;
		DeferredPath = path ?? string.Empty;
		DeferredOffset = offset;
		DeferredLength = System.Math.Max(0, length);
	}
}
