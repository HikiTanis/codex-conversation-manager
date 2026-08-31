using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace CodexConversationMigrator;

internal static class WinSqliteReader
{
	private const int SQLITE_OK = 0;

	private const int SQLITE_ROW = 100;

	private const int SQLITE_DONE = 101;

	private const int SQLITE_OPEN_READONLY = 1;

	[DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern int sqlite3_open_v2(byte[] filename, out IntPtr db, int flags, IntPtr vfs);

	[DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern int sqlite3_close_v2(IntPtr db);

	[DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern int sqlite3_prepare_v2(IntPtr db, byte[] sql, int length, out IntPtr statement, IntPtr tail);

	[DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern int sqlite3_step(IntPtr statement);

	[DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern int sqlite3_finalize(IntPtr statement);

	[DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr sqlite3_column_text(IntPtr statement, int index);

	[DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern int sqlite3_column_bytes(IntPtr statement, int index);

	[DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern long sqlite3_column_int64(IntPtr statement, int index);

	[DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr sqlite3_errmsg(IntPtr db);

	public static List<DbThread> ReadThreads(string databasePath)
	{
		IntPtr db = IntPtr.Zero;
		IntPtr statement = IntPtr.Zero;
		List<DbThread> list = new List<DbThread>();
		try
		{
			if (sqlite3_open_v2(Utf8(databasePath), out db, SQLITE_OPEN_READONLY, IntPtr.Zero) != SQLITE_OK)
			{
				throw new InvalidDataException("无法只读打开 Codex 索引：" + Error(db));
			}
			if (!TableExists(db, "threads"))
			{
				return list;
			}
			HashSet<string> threadColumns = ReadColumns(db, "threads");
			if (!threadColumns.Contains("id"))
			{
				return list;
			}
			HashSet<string> edgeColumns = TableExists(db, "thread_spawn_edges")
				? ReadColumns(db, "thread_spawn_edges")
				: new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			string sql = "select " + string.Join(",", new[]
			{
				TextColumn(threadColumns, "t", "id"),
				TextColumn(threadColumns, "t", "cwd"),
				TextColumn(threadColumns, "t", "rollout_path", "session_path", "path"),
				TextColumn(threadColumns, "t", "title", "name", "first_user_message", "preview"),
				TextColumn(threadColumns, "t", "source", "source_kind"),
				TextColumn(threadColumns, "t", "thread_source"),
				NumericColumn(threadColumns, "t", "archived"),
				UpdatedAtMilliseconds(threadColumns, "t"),
				ParentThreadId(threadColumns, edgeColumns),
				HistoryMode(threadColumns, "t")
			}) + " from [threads] t";
			if (sqlite3_prepare_v2(db, Utf8(sql), -1, out statement, IntPtr.Zero) != SQLITE_OK)
			{
				throw new InvalidDataException("无法读取 Codex threads：" + Error(db));
			}
			while (true)
			{
				switch (sqlite3_step(statement))
				{
				default:
					throw new InvalidDataException("读取 Codex threads 失败：" + Error(db));
				case 100:
					break;
				case 101:
					return list;
				}
				list.Add(new DbThread
				{
					Id = ColumnText(statement, 0),
					RawCwd = ColumnText(statement, 1),
					Cwd = TextHelpers.StripExtendedPrefix(ColumnText(statement, 1)),
					RolloutPath = ColumnText(statement, 2),
					Title = ColumnText(statement, 3),
					Source = ColumnText(statement, 4),
					ThreadSource = ColumnText(statement, 5),
					Archived = (sqlite3_column_int64(statement, 6) != 0),
					UpdatedAtMilliseconds = sqlite3_column_int64(statement, 7),
					ParentThreadId = ColumnText(statement, 8),
					HistoryMode = ColumnText(statement, 9)
				});
			}
		}
		finally
		{
			if (statement != IntPtr.Zero)
			{
				sqlite3_finalize(statement);
			}
			if (db != IntPtr.Zero)
			{
				sqlite3_close_v2(db);
			}
		}
	}

	private static bool TableExists(IntPtr db, string tableName)
	{
		IntPtr statement = IntPtr.Zero;
		try
		{
			string sql = "select count(*) from sqlite_master where type='table' and name='" + (tableName ?? string.Empty).Replace("'", "''") + "'";
			if (sqlite3_prepare_v2(db, Utf8(sql), -1, out statement, IntPtr.Zero) != 0)
			{
				return false;
			}
			return sqlite3_step(statement) == SQLITE_ROW && sqlite3_column_int64(statement, 0) > 0L;
		}
		finally
		{
			if (statement != IntPtr.Zero)
			{
				sqlite3_finalize(statement);
			}
		}
	}
	private static HashSet<string> ReadColumns(IntPtr db, string tableName)
	{
		HashSet<string> columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		IntPtr statement = IntPtr.Zero;
		try
		{
			string sql = "pragma table_info([" + (tableName ?? string.Empty).Replace("]", "]]") + "])";
			if (sqlite3_prepare_v2(db, Utf8(sql), -1, out statement, IntPtr.Zero) != 0)
			{
				return columns;
			}
			while (sqlite3_step(statement) == SQLITE_ROW)
			{
				string name = ColumnText(statement, 1);
				if (!string.IsNullOrWhiteSpace(name))
				{
					columns.Add(name);
				}
			}
			return columns;
		}
		finally
		{
			if (statement != IntPtr.Zero)
			{
				sqlite3_finalize(statement);
			}
		}
	}

	private static string TextColumn(ISet<string> columns, string alias, params string[] names)
	{
		List<string> candidates = new List<string>();
		foreach (string name in names)
		{
			if (columns.Contains(name))
			{
				candidates.Add("nullif(" + alias + ".[" + name + "],'')");
			}
		}
		return candidates.Count == 0
			? "''"
			: "coalesce(" + string.Join(",", candidates.ToArray()) + ",'')";
	}

	private static string NumericColumn(ISet<string> columns, string alias, string name)
	{
		return columns.Contains(name) ? "coalesce(" + alias + ".[" + name + "],0)" : "0";
	}

	private static string UpdatedAtMilliseconds(ISet<string> columns, string alias)
	{
		List<string> candidates = new List<string>();
		AddTimestamp(candidates, columns, alias, "updated_at_ms", false);
		AddTimestamp(candidates, columns, alias, "updated_at", true);
		AddTimestamp(candidates, columns, alias, "recency_at_ms", false);
		AddTimestamp(candidates, columns, alias, "recency_at", true);
		AddTimestamp(candidates, columns, alias, "created_at_ms", false);
		AddTimestamp(candidates, columns, alias, "created_at", true);
		return candidates.Count == 0
			? "0"
			: "coalesce(" + string.Join(",", candidates.ToArray()) + ",0)";
	}

	private static void AddTimestamp(ICollection<string> output, ISet<string> columns,
		string alias, string name, bool seconds)
	{
		if (!columns.Contains(name)) return;
		string value = alias + ".[" + name + "]";
		if (seconds) value = "(" + value + "*1000)";
		output.Add("nullif(" + value + ",0)");
	}

	private static string ParentThreadId(ISet<string> threadColumns,
		ISet<string> edgeColumns)
	{
		List<string> candidates = new List<string>();
		if (threadColumns.Contains("parent_thread_id"))
		{
			candidates.Add("nullif(t.[parent_thread_id],'')");
		}
		if (edgeColumns.Contains("parent_thread_id") && edgeColumns.Contains("child_thread_id"))
		{
			candidates.Add("(select nullif(e.[parent_thread_id],'') from [thread_spawn_edges] e " +
				"where e.[child_thread_id]=t.[id] limit 1)");
		}
		return candidates.Count == 0
			? "''"
			: "coalesce(" + string.Join(",", candidates.ToArray()) + ",'')";
	}

	private static string HistoryMode(ISet<string> columns, string alias)
	{
		return columns.Contains("history_mode")
			? "coalesce(nullif(" + alias + ".[history_mode],''),'legacy')"
			: "'legacy'";
	}

	private static byte[] Utf8(string value)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
		byte[] array = new byte[bytes.Length + 1];
		Buffer.BlockCopy(bytes, 0, array, 0, bytes.Length);
		return array;
	}

	private static string ColumnText(IntPtr statement, int index)
	{
		IntPtr intPtr = sqlite3_column_text(statement, index);
		int num = sqlite3_column_bytes(statement, index);
		if (intPtr == IntPtr.Zero || num <= 0)
		{
			return string.Empty;
		}
		byte[] array = new byte[num];
		Marshal.Copy(intPtr, array, 0, num);
		return Encoding.UTF8.GetString(array);
	}

	private static string Error(IntPtr database)
	{
		if (database == IntPtr.Zero)
		{
			return "SQLite 未返回详细信息";
		}
		IntPtr intPtr = sqlite3_errmsg(database);
		if (intPtr == IntPtr.Zero)
		{
			return "SQLite 未返回详细信息";
		}
		int i;
		for (i = 0; Marshal.ReadByte(intPtr, i) != 0 && i < 8192; i++)
		{
		}
		byte[] array = new byte[i];
		Marshal.Copy(intPtr, array, 0, i);
		return Encoding.UTF8.GetString(array);
	}
}
