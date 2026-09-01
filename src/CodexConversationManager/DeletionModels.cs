using System;

namespace CodexConversationManager;

internal enum ConversationDeleteMode
{
	MoveToTrash,
	Permanent
}

internal enum ProjectDeleteMode
{
	None,
	RecycleBin,
	Permanent
}

internal sealed class DeleteOptions
{
	public ConversationDeleteMode ConversationMode { get; set; }

	public ProjectDeleteMode ProjectMode { get; set; }
}

internal enum TrashAction
{
	None,
	Restore,
	DeletePermanently,
	DeleteProject
}

internal sealed class TrashActionRequest
{
	public TrashAction Action { get; set; }

	public TrashSessionInfo Item { get; set; }
}

internal sealed class TrashSessionInfo
{
	public string ThreadId { get; set; }

	public string Title { get; set; }

	public string OriginalPath { get; set; }

	public string BackupPath { get; set; }

	public string SidecarPath { get; set; }

	public string ProjectPath { get; set; }

	public string ProjectDeleteMode { get; set; }

	public DateTimeOffset DeletedAt { get; set; }

	public long SizeBytes { get; set; }

	public string Preview { get; set; }

	public string Source { get; set; }

	public string ModelProvider { get; set; }

	public string CliVersion { get; set; }

	public string CreatedAt { get; set; }

	public string UpdatedAt { get; set; }

	public bool Archived { get; set; }

	public bool Compressed { get; set; }

	public bool IsSubagent { get; set; }

	public string ParentThreadId { get; set; }

	public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? UiLanguage.T("未命名会话") : Title;

	public string DisplayDeletedAt => DeletedAt == default(DateTimeOffset) ? UiLanguage.T("时间未知") : DeletedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");

	public string DisplaySize => SizeBytes >= 1048576L ? (SizeBytes / 1048576.0).ToString("0.##") + " MB" : Math.Max(0L, SizeBytes / 1024L) + " KB";

	public string DisplayProject
	{
		get
		{
			if (string.IsNullOrWhiteSpace(ProjectPath))
			{
				return UiLanguage.T("未记录项目路径");
			}
			if (!string.IsNullOrWhiteSpace(ProjectDeleteMode))
			{
				return ProjectPath + UiLanguage.T("（已处理）");
			}
			if (!System.IO.Directory.Exists(ProjectPath))
			{
				return ProjectPath + UiLanguage.T("（不存在）");
			}
			return ProjectPath;
		}
	}
}

internal sealed class DeleteOperationResult
{
	public DeletedSessionResult Conversation { get; set; }

	public string ProjectPath { get; set; }

	public ProjectDeleteMode ProjectMode { get; set; }

	public string ProjectError { get; set; }

	public bool ProjectSucceeded => ProjectMode == ProjectDeleteMode.None || string.IsNullOrWhiteSpace(ProjectError);
}
