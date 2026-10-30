using System.Windows.Automation;

namespace NestedTray;
internal readonly record struct NativeTrayLayout(int Icon,int Cell,int Padding,Size Surface)
{
    internal static NativeTrayLayout Measure(nint overflow,uint dpi,bool queryAutomation=true)
    {
        double scale=Math.Max(96,dpi)/96.0;
        int icon=Native.GetSystemMetricsForDpi(49,Math.Max(96,dpi));
        int cell=(int)Math.Round(40*scale),padding=(int)Math.Round(4*scale);
        int columns=5,rows=4;
        if(overflow!=0&&Native.GetWindowRect(overflow,out var window))
        {
            // The native XAML host includes its shadow around the actual icon surface.
            int inset=(int)Math.Round(34*scale);
            columns=Math.Clamp((int)Math.Round((window.Right-window.Left-inset)/(double)cell),1,8);
            rows=Math.Clamp((int)Math.Round((window.Bottom-window.Top-inset)/(double)cell),1,12);
            if(queryAutomation&&Native.IsWindowVisible(overflow))
            {
                try
                {
                    var nodes=AutomationElement.FromHandle(overflow).FindAll(TreeScope.Descendants,Condition.TrueCondition);
                    var images=new List<System.Windows.Rect>();var cells=new List<System.Windows.Rect>();
                    foreach(AutomationElement node in nodes)
                    {
                        var current=node.Current;var rect=current.BoundingRectangle;
                        if(current.IsOffscreen||rect.IsEmpty)continue;
                        if(current.ControlType==ControlType.Image&&rect.Width>=8&&rect.Width<=64&&Math.Abs(rect.Width-rect.Height)<1)images.Add(rect);
                        if(current.AutomationId=="NotifyItemIcon"&&rect.Width>=20&&rect.Width<=100&&Math.Abs(rect.Width-rect.Height)<1)cells.Add(rect);
                    }
                    if(images.Count>0)icon=(int)Math.Round(images.Select(value=>value.Width).Order().ElementAt(images.Count/2));
                    if(cells.Count>0)cell=(int)Math.Round(cells.Select(value=>value.Width).Order().ElementAt(cells.Count/2));
                    if(images.Count>0)
                    {
                        columns=Math.Clamp((int)Math.Round((images.Max(value=>value.Left)-images.Min(value=>value.Left))/cell)+1,1,8);
                        rows=Math.Clamp((int)Math.Round((images.Max(value=>value.Top)-images.Min(value=>value.Top))/cell)+1,1,12);
                    }
                }
                catch(System.Runtime.InteropServices.COMException){}catch(ElementNotAvailableException){}
            }
        }
        return new(Math.Max(1,icon),cell,padding,new Size(columns*cell+padding*2,rows*cell+padding*2));
    }
}
