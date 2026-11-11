using System.Windows.Automation;

namespace NestedTray;
internal static class NativeOverflow
{
    private static nint measuredWindow;
    private static Rectangle measuredHost,measuredSurface;
    private static int measuredPadding;
    internal static nint Window=>Native.FindWindowW("TopLevelWindowForOverflowXamlIsland",null);
    internal static bool Visible=>Window is var window&&window!=0&&Native.IsWindowVisible(window);
    internal static Rectangle SurfaceFromCells(IEnumerable<Rectangle> cells,int padding)
    {
        var usable=cells.Where(rect=>rect.Width>0&&rect.Height>0).ToArray();
        if(usable.Length==0)return Rectangle.Empty;
        var bounds=Rectangle.FromLTRB(usable.Min(rect=>rect.Left),usable.Min(rect=>rect.Top),usable.Max(rect=>rect.Right),usable.Max(rect=>rect.Bottom));
        bounds.Inflate(padding,padding);return bounds;
    }
    internal static Rectangle SurfaceBounds(nint window,int padding,bool queryAutomation=true)
    {
        if(!Native.GetWindowRect(window,out var raw))return Rectangle.Empty;
        var host=Native.Rect(raw);
        if(queryAutomation)try
        {
            var nodes=AutomationElement.FromHandle(window).FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,"NotifyItemIcon"));
            var cells=new List<Rectangle>();
            foreach(AutomationElement node in nodes)
            {
                var current=node.Current;var rect=current.BoundingRectangle;
                if(current.IsOffscreen||rect.IsEmpty||rect.Width<20||rect.Width>100||Math.Abs(rect.Width-rect.Height)>1)continue;
                cells.Add(Rectangle.FromLTRB((int)Math.Floor(rect.Left),(int)Math.Floor(rect.Top),(int)Math.Ceiling(rect.Right),(int)Math.Ceiling(rect.Bottom)));
            }
            var surface=SurfaceFromCells(cells,padding);
            if(!surface.IsEmpty&&host.Contains(surface))
            {measuredWindow=window;measuredHost=host;measuredSurface=surface;measuredPadding=padding;return surface;}
        }
        catch(System.Runtime.InteropServices.COMException){}catch(ElementNotAvailableException){}
        if(measuredWindow==window&&measuredHost.Size==host.Size&&measuredPadding==padding)
        {var cached=measuredSurface;cached.Offset(host.Left-measuredHost.Left,host.Top-measuredHost.Top);return cached;}
        // XAML's host includes transparent margins; prefer the visible DWM frame
        // when the icon cells are not available through accessibility.
        if(Native.DwmGetWindowAttribute(window,9,out var visible,16)==0)
        {
            var frame=Native.Rect(visible);if(frame.Width>0&&frame.Height>0)return frame;
        }
        return host;
    }
    internal static nint TaskbarForBounds(Rectangle entry,nint primary)
    {
        var monitor=Screen.FromRectangle(entry).DeviceName;
        if(Native.IsWindow(primary)&&Screen.FromHandle(primary).DeviceName==monitor)return primary;
        nint result=primary;
        Native.EnumWindows((window,_)=>
        {
            var name=new System.Text.StringBuilder(80);Native.GetClassNameW(window,name,name.Capacity);
            if(name.ToString()=="Shell_SecondaryTrayWnd"&&Screen.FromHandle(window).DeviceName==monitor){result=window;return false;}
            return true;
        },0);
        return result;
    }
    internal static Rectangle ChevronBounds(nint explorer)
    {
        try
        {
            var nodes=AutomationElement.FromHandle(explorer).FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,"SystemTrayIcon"));
            foreach(AutomationElement node in nodes)if(node.Current.Name is "显示隐藏的图标" or "Show hidden icons")
            {var bounds=node.Current.BoundingRectangle;return new Rectangle((int)bounds.X,(int)bounds.Y,(int)bounds.Width,(int)bounds.Height);}
        }
        catch(System.Runtime.InteropServices.COMException){}catch(ElementNotAvailableException){}
        return Rectangle.Empty;
    }
    internal static bool TryOpen(nint explorer)=>Visible||InvokeChevron(explorer);
    internal static bool TryClose(nint explorer)=>!Visible||InvokeChevron(explorer);
    private static bool InvokeChevron(nint explorer)
    {
        try
        {
            var nodes=AutomationElement.FromHandle(explorer).FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,"SystemTrayIcon"));
            foreach(AutomationElement node in nodes)
                if(node.Current.Name is "显示隐藏的图标" or "Show hidden icons")
                {((InvokePattern)node.GetCurrentPattern(InvokePattern.Pattern)).Invoke();return true;}
        }
        catch(System.Runtime.InteropServices.COMException){}catch(ElementNotAvailableException){}
        return false;
    }
}
