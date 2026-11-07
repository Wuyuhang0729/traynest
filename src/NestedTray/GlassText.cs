using System.Runtime.InteropServices;

namespace NestedTray;
internal static class GlassText
{
    [StructLayout(LayoutKind.Sequential)]private struct PaintParameters{internal uint Size,Flags;internal nint Exclude,Blend;}
    [StructLayout(LayoutKind.Sequential)]private struct TextOptions
    {internal uint Size,Flags,Color,Border,Shadow;internal int ShadowType,ShadowX,ShadowY,BorderSize,FontProperty,ColorProperty,State,Overlay,Glow;internal nint Callback,Parameter;}
    [DllImport("uxtheme.dll")]private static extern int BufferedPaintInit();
    [DllImport("uxtheme.dll")]private static extern int BufferedPaintUnInit();
    [DllImport("uxtheme.dll")]private static extern nint BeginBufferedPaint(nint dc,ref NativeTray.Rect bounds,int format,ref PaintParameters parameters,out nint bufferDc);
    [DllImport("uxtheme.dll")]private static extern int EndBufferedPaint(nint buffer,[MarshalAs(UnmanagedType.Bool)]bool update);
    [DllImport("uxtheme.dll",CharSet=CharSet.Unicode)]private static extern nint OpenThemeData(nint window,string classes);
    [DllImport("uxtheme.dll")]private static extern int CloseThemeData(nint theme);
    [DllImport("uxtheme.dll",CharSet=CharSet.Unicode)]private static extern int DrawThemeTextEx(nint theme,nint dc,int part,int state,string text,int count,uint flags,ref NativeTray.Rect bounds,ref TextOptions options);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern int DrawTextW(nint dc,string text,int count,ref NativeTray.Rect bounds,uint flags);
    [DllImport("gdi32.dll")]private static extern nint SelectObject(nint dc,nint value);
    [DllImport("gdi32.dll")]private static extern bool DeleteObject(nint value);
    internal static bool Draw(Control control,Graphics graphics,string text,Color color)
    {
        if(control.Width<=0||control.Height<=0||BufferedPaintInit()<0)return false;
        nint dc=graphics.GetHdc(),buffer=0,font=0,previous=0,theme=0,bufferDc=0;bool success=false;
        try
        {
            var bounds=new NativeTray.Rect{Right=control.Width,Bottom=control.Height};
            var parameters=new PaintParameters{Size=(uint)Marshal.SizeOf<PaintParameters>(),Flags=1};
            buffer=BeginBufferedPaint(dc,ref bounds,2,ref parameters,out bufferDc);if(buffer==0)return false;
            font=control.Font.ToHfont();previous=SelectObject(bufferDc,font);theme=OpenThemeData(control.Handle,"WINDOW");if(theme==0)return false;
            const uint flags=0x1|0x10|0x800;var measure=bounds;int height=DrawTextW(bufferDc,text,-1,ref measure,flags|0x400);
            bounds.Top=Math.Max(0,(control.Height-height)/2);bounds.Bottom=bounds.Top+height;
            var options=new TextOptions{Size=(uint)Marshal.SizeOf<TextOptions>(),Flags=0x2001,Color=(uint)(color.R|(color.G<<8)|(color.B<<16))};
            success=DrawThemeTextEx(theme,bufferDc,0,0,text,-1,flags,ref bounds,ref options)>=0;return success;
        }
        finally
        {
            if(theme!=0)CloseThemeData(theme);if(previous!=0)SelectObject(bufferDc,previous);if(font!=0)DeleteObject(font);
            if(buffer!=0)EndBufferedPaint(buffer,success);graphics.ReleaseHdc(dc);BufferedPaintUnInit();
        }
    }
}
