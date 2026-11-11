using System.Windows.Automation;
namespace NestedTray;
internal static class OverflowBlankCell
{
    private static int querying;
    internal static Task<Rectangle> FindAsync(Point point,nint window)
    {
        if(Interlocked.CompareExchange(ref querying,1,0)!=0)return Task.FromResult(Rectangle.Empty);
        return Task.Run(()=>{try{return Find(point,window);}finally{Volatile.Write(ref querying,0);}});
    }
    // Only complete observed grid cells qualify. Unknown geometry fails closed.
    internal static Rectangle Find(Point point,nint window)
    {
        try
        {
            if(window==0||!Native.IsWindowVisible(window)||!Native.GetWindowRect(window,out var raw))return Rectangle.Empty;
            var nodes=AutomationElement.FromHandle(window).FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,"NotifyItemIcon"));
            var cells=new List<Rectangle>();
            foreach(AutomationElement node in nodes)
            {
                var item=node.Current;var rect=item.BoundingRectangle;
                if(item.IsOffscreen||rect.IsEmpty)continue;
                cells.Add(Rectangle.FromLTRB((int)Math.Round(rect.Left),(int)Math.Round(rect.Top),(int)Math.Round(rect.Right),(int)Math.Round(rect.Bottom)));
            }
            return Find(point,Native.Rect(raw),cells);
        }
        catch(System.Runtime.InteropServices.COMException){return Rectangle.Empty;}
        catch(ElementNotAvailableException){return Rectangle.Empty;}
        catch(InvalidOperationException){return Rectangle.Empty;}
    }
    internal static Rectangle Find(Point point,Rectangle host,IReadOnlyList<Rectangle> cells)
    {
        if(cells.Count<2)return Rectangle.Empty;
        var first=cells[0];int size=first.Width;
        if(size<20||size>150||cells.Any(c=>Math.Abs(c.Width-size)>1||Math.Abs(c.Height-size)>1||!host.Contains(c)))return Rectangle.Empty;
        int left=cells.Min(c=>c.Left),top=cells.Min(c=>c.Top);
        if(cells.Any(c=>(c.Left-left)%size!=0||(c.Top-top)%size!=0))return Rectangle.Empty;
        int columns=(cells.Max(c=>c.Right)-left)/size,rows=(cells.Max(c=>c.Bottom)-top)/size;
        if(point.X<left||point.Y<top||point.X>=left+columns*size||point.Y>=top+rows*size)return Rectangle.Empty;
        var slot=new Rectangle(left+(point.X-left)/size*size,top+(point.Y-top)/size*size,size,size);
        if(cells.Any(c=>c.IntersectsWith(slot)))return Rectangle.Empty;
        var inside=slot;inside.Inflate(-2,-2);return inside.Contains(point)?slot:Rectangle.Empty;
    }
}
