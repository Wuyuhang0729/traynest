using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

// Experimental decoding of the packet observed from Shell_NotifyIcon on this build.
// This is an undocumented protocol, not a Microsoft-supported extension API.
internal sealed record TrayPacket(uint Operation, uint Flags, nint Window, uint Id, Guid Guid, uint Callback, nint Image, uint Version, string Tip)
{
    internal static TrayPacket? Parse(ulong tag, byte[] bytes)
    {
        // Fail closed for an unfamiliar layout; never guess pointer offsets.
        if (tag != 1 || bytes.Length != 1484 || BitConverter.ToUInt32(bytes, 0) != 0x34753423 || BitConverter.ToUInt32(bytes, 8) != 956) return null;
        uint operation = BitConverter.ToUInt32(bytes, 4);
        if (operation > 4) return null;
        uint flags = BitConverter.ToUInt32(bytes, 20);
        // Unused guidItem storage may contain nonzero bytes. Windows identifies
        // these icons by HWND + uID unless NIF_GUID explicitly makes it valid.
        Guid identity = (flags & 0x20) != 0 ? new Guid(bytes.AsSpan(944, 16)) : Guid.Empty;
        return new(operation, flags, unchecked((nint)(int)BitConverter.ToUInt32(bytes, 12)), BitConverter.ToUInt32(bytes, 16),
            identity, BitConverter.ToUInt32(bytes, 24), unchecked((nint)(int)BitConverter.ToUInt32(bytes, 28)),
            BitConverter.ToUInt32(bytes, 808), Encoding.Unicode.GetString(bytes, 32, 256).Split('\0')[0]);
    }
    [DllImport("user32.dll")] private static extern nint CopyIcon(nint original);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(nint icon);

    internal string? SnapshotImage(string file)
    {
        if (Image == 0 || (Flags & 2) == 0) return null;
        nint copied = CopyIcon(Image);
        if (copied == 0) return null;
        try
        {
            using var icon = System.Drawing.Icon.FromHandle(copied);
            using var bitmap = icon.ToBitmap();
            using var buffer = new MemoryStream();
            bitmap.Save(buffer, System.Drawing.Imaging.ImageFormat.Png);
            byte[] png = buffer.ToArray();
            File.WriteAllBytes(file, png);
            return Convert.ToHexString(SHA256.HashData(png));
        }
        finally { DestroyIcon(copied); }
    }
}
