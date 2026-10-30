using System.Runtime.InteropServices;

internal static class NativeTray
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct Data
    {
        public uint Size;
        public nint Window;
        public uint Id, Flags, CallbackMessage;
        public nint Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State, StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint Version;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string InfoTitle;
        public uint InfoFlags;
        public Guid Guid;
        public nint BalloonIcon;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct Identity
    {
        public uint Size;
        public nint Window;
        public uint Id;
        public Guid Guid;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "Shell_NotifyIconW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool Notify(uint operation, ref Data data);
    [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconGetRect")]
    internal static extern int GetRect(ref Identity identity, out Rect rect);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool PostMessage(nint hwnd, uint message, nuint wParam, nint lParam);

    internal static Data New(nint hwnd, uint id, Guid guid) => new()
    {
        Size = (uint)Marshal.SizeOf<Data>(), Window = hwnd, Id = id, Guid = guid,
        Tip = "", Info = "", InfoTitle = ""
    };
}

internal sealed record FixtureManifest(long Window, uint Id, Guid Guid, uint CallbackMessage, uint Version);
