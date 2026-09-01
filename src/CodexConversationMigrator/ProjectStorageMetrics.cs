using System;
using System.Collections.Generic;
using System.IO;

namespace CodexConversationMigrator;

internal sealed class ProjectStorageSummary
{
	public long TotalBytes { get; set; }

	public int FileCount { get; set; }

	public int DirectoryCount { get; set; }

	public int SkippedCount { get; set; }

	public string Error { get; set; }
}

internal static class ProjectStorageMetrics
{
	public static ProjectStorageSummary Measure(string projectPath)
	{
		ProjectStorageSummary summary = new ProjectStorageSummary();
		if (string.IsNullOrWhiteSpace(projectPath))
		{
			summary.Error = "未记录项目路径";
			return summary;
		}
		string root;
		try
		{
			root = Path.GetFullPath(TextHelpers.StripExtendedPrefix(projectPath));
		}
		catch (Exception ex)
		{
			summary.Error = ex.Message;
			return summary;
		}
		if (!Directory.Exists(root))
		{
			summary.Error = "目录不存在";
			return summary;
		}

		Stack<string> pending = new Stack<string>();
		pending.Push(root);
		while (pending.Count > 0)
		{
			string directory = pending.Pop();
			IEnumerable<string> entries;
			try
			{
				entries = Directory.EnumerateFileSystemEntries(directory);
			}
			catch
			{
				summary.SkippedCount++;
				continue;
			}

			try
			{
				foreach (string entry in entries)
				{
					try
					{
						FileAttributes attributes = File.GetAttributes(entry);
						if ((attributes & FileAttributes.ReparsePoint) != 0)
						{
							summary.SkippedCount++;
							continue;
						}
						if ((attributes & FileAttributes.Directory) != 0)
						{
							summary.DirectoryCount++;
							pending.Push(entry);
						}
						else
						{
							summary.TotalBytes += new FileInfo(entry).Length;
							summary.FileCount++;
						}
					}
					catch
					{
						summary.SkippedCount++;
					}
				}
			}
			catch
			{
				summary.SkippedCount++;
			}
		}
		return summary;
	}
}
