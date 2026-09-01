using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CodexConversationManager;

internal static class WindowWorkArea
{
	private const int WmGetMinMaxInfo = 0x0024;

	private const uint MonitorDefaultToNearest = 0x00000002;

	[StructLayout(LayoutKind.Sequential)]
	private struct NativePoint
	{
		public int X;

		public int Y;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct MinMaxInfo
	{
		public NativePoint Reserved;

		public NativePoint MaxSize;

		public NativePoint MaxPosition;

		public NativePoint MinTrackSize;

		public NativePoint MaxTrackSize;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct NativeRect
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

		public NativeRect Monitor;

		public NativeRect WorkArea;

		public uint Flags;
	}

	[DllImport("user32.dll")]
	private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

	[DllImport("user32.dll", CharSet = CharSet.Auto)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo monitorInfo);

	internal static void Attach(Window window)
	{
		if (window == null)
		{
			throw new ArgumentNullException(nameof(window));
		}
		window.SourceInitialized += delegate
		{
			HwndSource source = PresentationSource.FromVisual(window) as HwndSource;
			source?.AddHook(WindowProcedure);
		};
	}

	private static IntPtr WindowProcedure(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
	{
		if (message == WmGetMinMaxInfo && lParam != IntPtr.Zero)
		{
			ApplyMonitorWorkArea(hwnd, lParam);
			handled = true;
		}
		return IntPtr.Zero;
	}

	private static void ApplyMonitorWorkArea(IntPtr hwnd, IntPtr lParam)
	{
		IntPtr monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
		if (monitor == IntPtr.Zero)
		{
			return;
		}
		MonitorInfo monitorInfo = new MonitorInfo
		{
			Size = Marshal.SizeOf(typeof(MonitorInfo))
		};
		if (!GetMonitorInfo(monitor, ref monitorInfo))
		{
			return;
		}
		MinMaxInfo minMaxInfo = Marshal.PtrToStructure<MinMaxInfo>(lParam);
		NativeRect workArea = monitorInfo.WorkArea;
		NativeRect monitorArea = monitorInfo.Monitor;
		minMaxInfo.MaxPosition.X = workArea.Left - monitorArea.Left;
		minMaxInfo.MaxPosition.Y = workArea.Top - monitorArea.Top;
		minMaxInfo.MaxSize.X = workArea.Right - workArea.Left;
		minMaxInfo.MaxSize.Y = workArea.Bottom - workArea.Top;
		minMaxInfo.MaxTrackSize = minMaxInfo.MaxSize;
		Marshal.StructureToPtr(minMaxInfo, lParam, fDeleteOld: true);
	}
}
