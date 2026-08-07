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
		try
		{
			if (sqlite3_open_v2(Utf8(databasePath), out db, 1, IntPtr.Zero) != 0)
			{
				throw new InvalidDataException("无法只读打开 Codex 索引：" + Error(db));
			}
			string value = "select id,cwd,rollout_path,title,source,coalesce(thread_source,''),archived,coalesce(updated_at_ms,updated_at*1000) from threads";
			if (sqlite3_prepare_v2(db, Utf8(value), -1, out statement, IntPtr.Zero) != 0)
			{
				throw new InvalidDataException("无法读取 Codex threads：" + Error(db));
			}
			List<DbThread> list = new List<DbThread>();
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
					UpdatedAtMilliseconds = sqlite3_column_int64(statement, 7)
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
