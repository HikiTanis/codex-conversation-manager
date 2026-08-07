using System;
using System.Collections.Generic;
using System.Linq;

namespace CodexConversationMigrator;

internal sealed class ProjectGroup : NotifyObject
{
	private bool isBatchSelected;

	private bool storageScanStarted;

	private string projectStorageSummary = UiLanguage.T("项目文件：尚未统计");

	public string ProjectId { get; set; }

	public string DisplayName { get; set; }

	public string ProjectPath { get; set; }

	public int SortIndex { get; set; }

	public List<SessionInfo> Sessions { get; set; }

	public bool IsBatchSelected
	{
		get
		{
			return isBatchSelected;
		}
		set
		{
			if (isBatchSelected != value)
			{
				isBatchSelected = value;
				RaisePropertyChanged("IsBatchSelected");
			}
		}
	}

	public bool CanBackupFiles => !string.IsNullOrWhiteSpace(ProjectPath) && System.IO.Directory.Exists(ProjectPath);

	public bool StorageScanStarted => storageScanStarted;

	public string ProjectStorageSummary => projectStorageSummary;

	public void BeginStorageScan()
	{
		if (storageScanStarted)
		{
			return;
		}
		storageScanStarted = true;
		projectStorageSummary = UiLanguage.T("项目文件：正在统计……");
		RaisePropertyChanged("ProjectStorageSummary");
	}

	public void CompleteStorageScan(ProjectStorageSummary summary)
	{
		if (summary == null)
		{
			projectStorageSummary = UiLanguage.T("项目文件：无法统计");
		}
		else if (!string.IsNullOrWhiteSpace(summary.Error) && summary.FileCount == 0)
		{
			projectStorageSummary = UiLanguage.T("项目文件：无法统计 · " + summary.Error);
		}
		else
		{
			projectStorageSummary = UiLanguage.T("项目文件：" + TextHelpers.FormatBytes(summary.TotalBytes) + " · " + summary.FileCount.ToString("N0") + " 个文件");
			if (summary.SkippedCount > 0)
			{
				projectStorageSummary += UiLanguage.IsEnglish ? " · " + summary.SkippedCount.ToString("N0") + " skipped" : " · 跳过 " + summary.SkippedCount.ToString("N0") + " 项";
			}
		}
		RaisePropertyChanged("ProjectStorageSummary");
	}

	public int MainCount
	{
		get
		{
			if (Sessions != null)
			{
				return Sessions.Count((SessionInfo x) => !x.IsSubagent);
			}
			return 0;
		}
	}

	public int InternalCount
	{
		get
		{
			if (Sessions != null)
			{
				return Sessions.Count((SessionInfo x) => x.IsSubagent);
			}
			return 0;
		}
	}

	public DateTime LastUpdated
	{
		get
		{
			if (Sessions != null && Sessions.Count != 0)
			{
				return Sessions.Max((SessionInfo x) => x.UpdatedDate);
			}
			return DateTime.MinValue;
		}
	}

	public string ListMeta => UiLanguage.IsEnglish
		? $"{MainCount} main · {InternalCount} subagents"
		: $"{MainCount} 主对话 · {InternalCount} 子代理";

	public string HeaderMeta => UiLanguage.IsEnglish
		? string.Format("{0} main conversations · {1} subagent conversations · Last updated {2}", MainCount, InternalCount, (LastUpdated == DateTime.MinValue) ? "Unknown" : LastUpdated.ToString("yyyy-MM-dd HH:mm"))
		: string.Format("{0} 个主对话 · {1} 个子代理对话 · 最近更新 {2}", MainCount, InternalCount, (LastUpdated == DateTime.MinValue) ? "未知" : LastUpdated.ToString("yyyy-MM-dd HH:mm"));

	public override string ToString()
	{
		return DisplayName;
	}
}
