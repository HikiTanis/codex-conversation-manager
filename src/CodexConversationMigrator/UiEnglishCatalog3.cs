using System;
using System.Collections.Generic;

namespace CodexConversationMigrator;

internal static class UiEnglishCatalog3
{
	public static readonly Dictionary<string, string> Entries = new Dictionary<string, string>(StringComparer.Ordinal)
	{
		{ "已载入 ", "Loaded " },
		{ " 个Main conversations", " main conversations" },
		{ " 个Subagent conversation", " subagent conversations" },
		{ "已复制项目路径：", "Project path copied: " },
		{ "已复制 Thread ID：", "Thread ID copied: " },
		{ "已还原 ", "Restored " },
		{ " 个项目到：", " projects to:" },
		{ "已将旧版迁移产生的 ", "Moved " },
		{ " 个内容完全相同副本移到可恢复目录：", " identical legacy migration copies to a recoverable folder:" },
		{ " 个Main conversations · ", " main conversations · " },
		{ " 个Subagents", " subagents" },

		{ "正在封装第 ", "Packaging project " },
		{ " 个项目的对话：", " project conversations: " },
		{ "项目 ", "Project " },
		{ " · 对话 ", " · conversation " },
		{ " · 文件 ", " · file " },
		{ " · 正在备份对话 ", " · backing up conversation " },
		{ "正在压缩第 ", "Compressing project " },
		{ " 个项目文件：", " project files: " },
		{ "正在校验并封装第 ", "Validating and packaging record " },
		{ " 条记录：", " records: " },
		{ "跳过重解析点：", "Skipped reparse points: " },

		{ "检查第 ", "Inspecting bundle " },
		{ "导入第 ", "Importing bundle " },
		{ " 个对话包 · ", " · " },
		{ " 个对话包……", "…" },
		{ "确认操作记录后即可正式导入。", "Review the operation log, then start the import." },
		{ "匹配：", "Matched: " },
		{ "生成全新编号：", "New IDs to create: " },

		{ "创建 ", "created " },
		{ "包含项目：", "Projects: " },
		{ "原项目：", "Source project: " },
		{ " 个，", ", " },
		{ "，", ", " },
		{ "。", "." },
		{ "；", "; " },
		{ "：", ": " },
		{ "（", " (" },
		{ "）", ")" },
		{ " 条", "" },
		{ " 个", "" },

		{ "项目：", "Project: " },
		{ "项目文件还原完成：", "Project file restoration complete: " },
		{ "项目文件已还原到：", "Project files restored to:" },
		{ "项目载荷校验通过：", "Project payload validation passed: " },
		{ "新增索引：", "Inserted index entries: " },
		{ "更新索引：", "Updated index entries: " },
		{ "新增索引 ", "Inserted index entries: " },
		{ "更新索引 ", "Updated index entries: " },
		{ "路径。", "path." },
		{ "路径：", "path: " },
		{ "正在统计", "Measuring" },
		{ "正在", "Working" },
		{ "导入", "Import" },
		{ "检查", "Inspect" },
		{ "已", "Completed" }
	};
}
