using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace NestedTray;
internal enum PanelAppearance { FollowSystem, Acrylic, Light, Dark }

internal static class PanelTheme
{
    internal static bool SystemLight
    {
        get
        {
            try{using var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");return key?.GetValue("SystemUsesLightTheme") is not int value||value!=0;}
            catch(System.Security.SecurityException){return true;}
        }
    }
    internal static bool TransparencyEnabled
    {
        get
        {
            try{using var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");return key?.GetValue("EnableTransparency") is not int value||value!=0;}
            catch(System.Security.SecurityException){return false;}
        }
    }
    internal static bool Light(PanelAppearance mode)=>mode==PanelAppearance.Light||mode!=PanelAppearance.Dark&&SystemLight;
    // Black GDI pixels have zero alpha on an extended DWM frame. Preserve
    // that surface instead of filling it with opaque GDI+ black pixels.
    internal static void PaintGlass(Graphics graphics,Rectangle bounds)
    {
        nint dc=graphics.GetHdc();
        try{var rect=new NativeTray.Rect{Left=bounds.Left,Top=bounds.Top,Right=bounds.Right,Bottom=bounds.Bottom};FillRect(dc,ref rect,GetStockObject(4));}
        finally{graphics.ReleaseHdc(dc);}
    }
    [DllImport("user32.dll")]private static extern int FillRect(nint dc,ref NativeTray.Rect rect,nint brush);
    [DllImport("gdi32.dll")]private static extern nint GetStockObject(int index);
    [StructLayout(LayoutKind.Sequential)]internal struct Margins {internal int Left,Right,Top,Bottom;}
    [DllImport("dwmapi.dll")]internal static extern int DwmExtendFrameIntoClientArea(nint window,ref Margins margins);
    [StructLayout(LayoutKind.Sequential)]private struct AccentPolicy {internal int State;internal uint Flags,Color;internal int Animation;}
    [StructLayout(LayoutKind.Sequential)]private struct CompositionData {internal int Attribute;internal nint Data;internal uint Size;}
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    [return:MarshalAs(UnmanagedType.Bool)]private delegate bool SetComposition(nint window,ref CompositionData data);
    private static readonly SetComposition? setComposition=ResolveComposition();
    private static SetComposition? ResolveComposition()
    {
        nint module=Native.GetModuleHandleW("user32.dll");
        return module!=0&&NativeLibrary.TryGetExport(module,"SetWindowCompositionAttribute",out var proc)?Marshal.GetDelegateForFunctionPointer<SetComposition>(proc):null;
    }
    // The documented system backdrop has a fixed tint. Use a lighter native
    // acrylic tint when available, retaining the system backdrop as a fallback.
    internal static bool AcrylicTint(nint window,bool enabled,bool light)
        =>AccentTint(window,enabled?4:0,light?0x01FFFFFFu:0x18000000u);
    private static bool AccentTint(nint window,int state,uint color)
    {
        if(setComposition==null)return false;
        var policy=new AccentPolicy{State=state,Flags=0,Color=color};
        nint pointer=Marshal.AllocHGlobal(Marshal.SizeOf<AccentPolicy>());
        try
        {
            Marshal.StructureToPtr(policy,pointer,false);
            var data=new CompositionData{Attribute=19,Data=pointer,Size=(uint)Marshal.SizeOf<AccentPolicy>()};
            return setComposition(window,ref data);
        }
        finally{Marshal.FreeHGlobal(pointer);}
    }
}

internal sealed partial class TrayPanel
{
    internal PanelAppearance Appearance {get;private set;}=PanelAppearance.FollowSystem;
    internal bool LightSurface {get;private set;}
    internal bool AcrylicActive {get;private set;}
    internal int BackdropResult {get;private set;}
    internal bool CustomAcrylicTint {get;private set;}
    private Color surfaceColor,foregroundColor;
    private (bool Light,bool Glass,bool HighContrast)? appliedTheme;
    internal void SetAppearance(PanelAppearance mode)
    {
        Appearance=Enum.IsDefined(mode)?mode:PanelAppearance.FollowSystem;
        ApplyAppearance();
    }
    private void ApplyAppearance(bool force=false)
    {
        if(!IsHandleCreated)return;
        bool light=PanelTheme.Light(Appearance);
        bool glass=Appearance==PanelAppearance.Acrylic&&!SystemInformation.HighContrast&&PanelTheme.TransparencyEnabled&&Environment.OSVersion.Version.Build>=22621;
        var signature=(light,glass,SystemInformation.HighContrast);
        if(!force&&appliedTheme==signature)return;
        appliedTheme=signature;LightSurface=light;
        int dark=LightSurface?0:1;Native.DwmSetWindowAttribute(Handle,20,ref dark,4);
        if(CustomAcrylicTint){PanelTheme.AcrylicTint(Handle,false,LightSurface);CustomAcrylicTint=false;}
        int backdrop=1;
        BackdropResult=Native.DwmSetWindowAttribute(Handle,38,ref backdrop,4);
        if(glass)CustomAcrylicTint=PanelTheme.AcrylicTint(Handle,true,LightSurface);
        if(glass&&!CustomAcrylicTint){backdrop=3;BackdropResult=Native.DwmSetWindowAttribute(Handle,38,ref backdrop,4);}
        AcrylicActive=glass&&(CustomAcrylicTint||BackdropResult>=0);
        var margins=new PanelTheme.Margins{Left=AcrylicActive?-1:0};
        if(PanelTheme.DwmExtendFrameIntoClientArea(Handle,ref margins)<0&&AcrylicActive)
        {AcrylicActive=false;if(CustomAcrylicTint)PanelTheme.AcrylicTint(Handle,false,LightSurface);CustomAcrylicTint=false;backdrop=1;Native.DwmSetWindowAttribute(Handle,38,ref backdrop,4);}
        surfaceColor=SystemInformation.HighContrast?SystemColors.Window:LightSurface?Color.FromArgb(250,250,250):Color.FromArgb(32,34,40);
        foregroundColor=SystemInformation.HighContrast?SystemColors.WindowText:LightSurface?Color.FromArgb(30,30,30):Color.FromArgb(240,240,240);
        UpdateAppearanceColors();
    }
    private void UpdateAppearanceColors()
    {
        Color background=AcrylicActive?Color.Black:NativeDragFeedback?(LightSurface?Color.FromArgb(225,239,252):Color.FromArgb(32,58,78)):surfaceColor;
        BackColor=background;ForeColor=foregroundColor;
        grid.GlassSurface=AcrylicActive;grid.BackColor=background;
        empty.GlassSurface=AcrylicActive;empty.BackColor=background;empty.ForeColor=foregroundColor;
        fade.BackColor=surfaceColor;fade.ForeColor=foregroundColor;
        foreach(var tile in tiles.Values.OfType<IconTileButton>())
        {tile.BackColor=background;tile.ForeColor=foregroundColor;tile.LightSurface=LightSurface;tile.GlassSurface=AcrylicActive;tile.Invalidate();}
        Invalidate(true);
    }
    protected override void OnPaintBackground(PaintEventArgs e)
    {if(AcrylicActive)PanelTheme.PaintGlass(e.Graphics,ClientRectangle);else base.OnPaintBackground(e);}
    private sealed class SurfaceLabel:Label
    {
        internal bool GlassSurface;
        protected override void OnPaintBackground(PaintEventArgs e)
        {if(GlassSurface)PanelTheme.PaintGlass(e.Graphics,ClientRectangle);else base.OnPaintBackground(e);}
        protected override void OnPaint(PaintEventArgs e)
        {if(!GlassSurface||!GlassText.Draw(this,e.Graphics,Text,ForeColor))base.OnPaint(e);}
    }
}
