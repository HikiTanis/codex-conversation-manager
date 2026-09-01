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

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
	private struct MonitorInfo
	{
		public int Size;

		public Rect Monitor;

		public Rect WorkArea;

		public uint Flags;
	}

	private const int WM_NCHITTEST = 132;

	private const int WM_GETICON = 127;

	private const uint MONITOR_DEFAULTTONEAREST = 2;

	private const int GWL_STYLE = -16;

	private const int GCLP_HICON = -14;

	private const int GCLP_HICONSM = -34;

	private const long WS_THICKFRAME = 262144L;

	private const long WS_MINIMIZEBOX = 131072L;

	private const long WS_MAXIMIZEBOX = 65536L;

	[DllImport("user32.dll")]
	private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);

	[DllImport("user32.dll")]
	private static extern bool GetClientRect(IntPtr hwnd, out Rect rect);

	[DllImport("user32.dll")]
	private static extern bool ClientToScreen(IntPtr hwnd, ref System.Drawing.Point point);

	[DllImport("user32.dll")]
	private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

	[DllImport("user32.dll", CharSet = CharSet.Auto)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo monitorInfo);

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

	public static bool VerifyMaximizedWorkArea(IntPtr hwnd, out string report)
	{
		if (!GetClientRect(hwnd, out Rect clientRect))
		{
			throw new InvalidOperationException("GetClientRect failed");
		}
		System.Drawing.Point clientOrigin = new System.Drawing.Point(0, 0);
		if (!ClientToScreen(hwnd, ref clientOrigin))
		{
			throw new InvalidOperationException("ClientToScreen failed");
		}
		IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
		MonitorInfo monitorInfo = new MonitorInfo
		{
			Size = Marshal.SizeOf(typeof(MonitorInfo))
		};
		if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref monitorInfo))
		{
			throw new InvalidOperationException("GetMonitorInfo failed");
		}
		int clientLeft = clientOrigin.X;
		int clientTop = clientOrigin.Y;
		int clientRight = clientLeft + clientRect.Right - clientRect.Left;
		int clientBottom = clientTop + clientRect.Bottom - clientRect.Top;
		Rect workArea = monitorInfo.WorkArea;
		const int edgeTolerance = 2;
		const int fillTolerance = 20;
		bool inside = clientLeft >= workArea.Left - edgeTolerance &&
			clientTop >= workArea.Top - edgeTolerance &&
			clientRight <= workArea.Right + edgeTolerance &&
			clientBottom <= workArea.Bottom + edgeTolerance;
		bool fills = clientRight - clientLeft >= workArea.Right - workArea.Left - fillTolerance &&
			clientBottom - clientTop >= workArea.Bottom - workArea.Top - fillTolerance;
		bool result = inside && fills;
		report = "ClientBounds=" + clientLeft + "," + clientTop + "," + clientRight + "," + clientBottom +
			"\r\nWorkArea=" + workArea.Left + "," + workArea.Top + "," + workArea.Right + "," + workArea.Bottom +
			"\r\nMaximizedWorkAreaOk=" + result;
		return result;
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
