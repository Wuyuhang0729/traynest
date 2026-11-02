namespace NestedTray;

internal static class AppIcon
{
    internal static Icon Load(int size=32)
    {
        using var stream=typeof(AppIcon).Assembly.GetManifestResourceStream("NestedTray.TrayNest.ico")
            ??throw new InvalidOperationException("TrayNest icon resource is missing.");
        using var icon=new Icon(stream,size,size);
        return (Icon)icon.Clone();
    }
}
