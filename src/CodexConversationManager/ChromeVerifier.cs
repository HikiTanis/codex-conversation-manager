using System;
using System.Runtime.InteropServices;

namespace CodexConversationManager;

internal static class ChromeVerifier
{
	private struct Rect
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;
	}

	private const int WM_NCHITTEST = 132;

	private const int WM_GETICON = 127;

	private const int GWL_STYLE = -16;

	private const int GCLP_HICON = -14;

	private const int GCLP_HICONSM = -34;

	private const long WS_THICKFRAME = 262144L;

	private const long WS_MINIMIZEBOX = 131072L;

	private const long WS_MAXIMIZEBOX = 65536L;

	[DllImport("user32.dll")]
	private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);

	[DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
	private static extern IntPtr GetWindowLongPtr64(IntPtr hwnd, int index);

	[DllImport("user32.dll", EntryPoint = "GetWindowLong")]
	private static extern IntPtr GetWindowLong32(IntPtr hwnd, int index);

	[DllImport("user32.dll", EntryPoint = "GetClassLongPtr")]
	private static extern IntPtr GetClassLongPtr64(IntPtr hwnd, int index);

	[DllImport("user32.dll", EntryPoint = "GetClassLong")]
	private static extern uint GetClassLong32(IntPtr hwnd, int index);

	[DllImport("user32.dll")]
	private static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);

	public static string Verify(IntPtr hwnd)
	{
		if (!GetWindowRect(hwnd, out var rect))
		{
			throw new InvalidOperationException("GetWindowRect failed");
		}
		long num = ((IntPtr.Size == 8) ? GetWindowLongPtr64(hwnd, -16) : GetWindowLong32(hwnd, -16)).ToInt64();
		int num2 = Hit(hwnd, rect.Left + 2, rect.Top + 2);
		int num3 = Hit(hwnd, rect.Right - 2, rect.Top + 2);
		int num4 = Hit(hwnd, rect.Left + 2, rect.Bottom - 2);
		int num5 = Hit(hwnd, rect.Right - 2, rect.Bottom - 2);
		int num6 = Hit(hwnd, rect.Left + 420, rect.Top + 28);
		bool hasIcon = HasWindowIcon(hwnd);
		bool flag = (num & 0x40000) != 0 && (num & 0x20000) != 0 && (num & 0x10000) != 0;
		bool flag2 = num2 == 13 && num3 == 14 && num4 == 16 && num5 == 17 && num6 == 2;
		return "StylesOk=" + flag + "\r\nTopLeft=" + num2 + "\r\nTopRight=" + num3 + "\r\nBottomLeft=" + num4 + "\r\nBottomRight=" + num5 + "\r\nCaption=" + num6 + "\r\nTaskbarIcon=" + hasIcon + "\r\nChromeOk=" + (flag && flag2 && hasIcon);
	}

	private static bool HasWindowIcon(IntPtr hwnd)
	{
		IntPtr icon = SendMessage(hwnd, WM_GETICON, new IntPtr(1), IntPtr.Zero);
		if (icon == IntPtr.Zero)
		{
			icon = SendMessage(hwnd, WM_GETICON, IntPtr.Zero, IntPtr.Zero);
		}
		if (icon == IntPtr.Zero)
		{
			icon = SendMessage(hwnd, WM_GETICON, new IntPtr(2), IntPtr.Zero);
		}
		if (icon == IntPtr.Zero)
		{
			icon = GetClassIcon(hwnd, GCLP_HICON);
		}
		if (icon == IntPtr.Zero)
		{
			icon = GetClassIcon(hwnd, GCLP_HICONSM);
		}
		return icon != IntPtr.Zero;
	}

	private static IntPtr GetClassIcon(IntPtr hwnd, int index)
	{
		return IntPtr.Size == 8 ? GetClassLongPtr64(hwnd, index) : new IntPtr(unchecked((int)GetClassLong32(hwnd, index)));
	}

	private static int Hit(IntPtr hwnd, int x, int y)
	{
		long value = ((long)(y & 0xFFFF) << 16) | (uint)(x & 0xFFFF);
		return SendMessage(hwnd, 132, IntPtr.Zero, new IntPtr(value)).ToInt32();
	}
}
