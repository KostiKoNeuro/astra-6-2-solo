using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TaskbarTails
{
    internal static class Win
    {
        public const int WS_POPUP = unchecked((int)0x80000000);
        public const int TOPMOST = 8, TRANSPARENT = 0x20, TOOLWINDOW = 0x80, LAYERED = 0x80000, NOACTIVATE = 0x08000000;
        public const int MOUSEACTIVATE = 0x21, LBUTTONDOWN = 0x201, LBUTTONUP = 0x202, MOUSEMOVE = 0x200,
            RBUTTONUP = 0x205, CLIPBOARDUPDATE = 0x031D, TRAY = 0x8041, CLOSE = 0x10, DISPLAYCHANGE = 0x7E;
        [StructLayout(LayoutKind.Sequential)]
        public struct Pt
        {
            public int X, Y;
            public Pt(int x, int y) { X = x; Y = y; }
            public Pt(Point p) { X = p.X; Y = p.Y; }
            public Point Point { get { return new Point(X, Y); } }
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct Dim { public int Width, Height; public Dim(int w, int h) { Width = w; Height = h; } }
        [StructLayout(LayoutKind.Sequential, Pack=1)]
        public struct Blend { public byte Operation, Flags, Opacity, Alpha; }
        [StructLayout(LayoutKind.Sequential)]
        public struct DibInfo
        {
            public int Size, Width, Height;
            public ushort Planes, BitCount;
            public int Compression, SizeImage, XPels, YPels, ColorsUsed, ColorsImportant;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct LastInput { public uint Size, Tick; }
        [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
        public struct Notify
        {
            public int Size;
            public IntPtr Window;
            public uint Id, Flags, Callback;
            public IntPtr Icon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst=128)] public string Tip;
            public uint State, StateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst=256)] public string Info;
            public uint Version;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst=64)] public string InfoTitle;
            public uint InfoFlags;
            public Guid Guid;
            public IntPtr BalloonIcon;
        }
        [DllImport("user32.dll", SetLastError=true)] public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr dest, ref Pt pos, ref Dim size, IntPtr src, ref Pt origin, int key, ref Blend blend, int flags);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateDIBSection(IntPtr dc, ref DibInfo header, int usage, out IntPtr bits, IntPtr section, int offset);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr dc, IntPtr item);
        [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hwnd, int how);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int w, int h, uint flags);
        [DllImport("user32.dll")] public static extern IntPtr SetCapture(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool ReleaseCapture();
        [DllImport("user32.dll")] public static extern IntPtr GetCapture();
        [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hwnd, ref Pt pt);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(Pt where);
        [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hwnd, int idx);
        [DllImport("user32.dll")] public static extern bool GetCursorPos(out Pt pt);
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] public static extern bool GetLastInputInfo(ref LastInput input);
        [DllImport("user32.dll")] public static extern bool AddClipboardFormatListener(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern uint GetClipboardSequenceNumber();
        [DllImport("user32.dll")] public static extern bool OpenClipboard(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool CloseClipboard();
        [DllImport("user32.dll")] public static extern bool IsClipboardFormatAvailable(uint format);
        [DllImport("user32.dll")] public static extern IntPtr GetClipboardData(uint format);
        [DllImport("kernel32.dll")] public static extern IntPtr GlobalLock(IntPtr block);
        [DllImport("kernel32.dll")] public static extern bool GlobalUnlock(IntPtr block);
        [DllImport("kernel32.dll")] public static extern UIntPtr GlobalSize(IntPtr block);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern uint RegisterWindowMessage(string name);
        [DllImport("shell32.dll", CharSet=CharSet.Unicode)] public static extern bool Shell_NotifyIcon(uint msg, ref Notify info);
        [DllImport("user32.dll")] public static extern IntPtr MonitorFromPoint(Pt point, int flags);
        [DllImport("shcore.dll")] public static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wp, IntPtr lp);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wp, IntPtr lp);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindow(string cls, string title);
        [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
        public static double ScaleAt(Point point)
        {
            uint x, y;
            return GetDpiForMonitor(MonitorFromPoint(new Pt(point), 2), 0, out x, out y) == 0 ? x / 96.0 : 1;
        }
        public static Point MessagePoint(IntPtr lp)
        {
            long n = lp.ToInt64();
            return new Point((short)(n & 0xffff), (short)((n >> 16) & 0xffff));
        }
    }

    internal interface IDesk
    {
        double Now { get; }
        uint IdleMs { get; }
        Point Pointer { get; }
        bool LeftDown { get; }
        uint ClipboardRevision { get; }
        bool TryReadFind(out FindKind result);
    }

    internal sealed class Desktop : IDesk
    {
        readonly Stopwatch clock = Stopwatch.StartNew();
        public double Now { get { return clock.Elapsed.TotalSeconds; } }
        public uint IdleMs
        {
            get
            {
                Win.LastInput input = new Win.LastInput { Size = 8 };
                return Win.GetLastInputInfo(ref input) ? unchecked((uint)Environment.TickCount - input.Tick) : 0;
            }
        }
        public Point Pointer { get { Win.Pt p; Win.GetCursorPos(out p); return p.Point; } }
        public bool LeftDown { get { return Win.GetAsyncKeyState(1) < 0; } }
        public uint ClipboardRevision { get { return Win.GetClipboardSequenceNumber(); } }
        public bool TryReadFind(out FindKind result)
        {
            result = FindKind.None;
            const uint unicodeText = 13;
            if (!Win.IsClipboardFormatAvailable(unicodeText)) return true;
            if (!Win.OpenClipboard(IntPtr.Zero)) return false;
            string sample;
            try { if (!ReadUnicodeBlock(Win.GetClipboardData(unicodeText), out sample)) return false; }
            finally { Win.CloseClipboard(); }
            result = Finds.Classify(sample);
            return true;
        }
        internal static bool ReadUnicodeBlock(IntPtr block, out string sample)
        {
            sample = null;
            if (block == IntPtr.Zero) return true;
            int chars = (int)Math.Min(8192UL, Win.GlobalSize(block).ToUInt64() / 2);
            IntPtr ptr = Win.GlobalLock(block);
            if (ptr == IntPtr.Zero) return false;
            try
            {
                sample = Marshal.PtrToStringUni(ptr, chars);
                int end = sample.IndexOf('\0');
                if (end >= 0) sample = sample.Substring(0, end);
                return true;
            }
            finally { Win.GlobalUnlock(block); }
        }
    }

    internal sealed class TrayIcon : IDisposable
    {
        Win.Notify data;
        public bool Installed { get; private set; }
        public TrayIcon(IntPtr handle, Icon icon)
        {
            data.Size = Marshal.SizeOf(typeof(Win.Notify));
            data.Window = handle;
            data.Id = 1;
            data.Flags = 1 | 2 | 4;
            data.Callback = Win.TRAY;
            data.Icon = icon.Handle;
            data.Tip = "Pip - Taskbar Tails";
            Add();
        }
        public void Add() { Installed = Win.Shell_NotifyIcon(0, ref data); }
        public void SetStatus(string activity)
        {
            string tip = "Pip - " + activity + "\nTaskbar Tails";
            if (data.Tip == tip) return;
            data.Tip = tip;
            if (Installed) Installed = Win.Shell_NotifyIcon(1, ref data);
        }
        public void Dispose()
        {
            Win.Shell_NotifyIcon(2, ref data);
            Installed = false;
        }
    }
}
