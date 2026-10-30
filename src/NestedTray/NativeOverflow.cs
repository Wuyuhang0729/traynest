using System.Windows.Automation;

namespace NestedTray;
internal static class NativeOverflow
{
    internal static nint Window=>Native.FindWindowW("TopLevelWindowForOverflowXamlIsland",null);
    internal static bool Visible=>Window is var window&&window!=0&&Native.IsWindowVisible(window);
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
    internal static bool TryOpen(nint explorer)
    {
        if(Visible)return true;
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
