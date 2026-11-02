namespace NestedTray;

internal enum PopupPlacement { Left, Above }

internal static class PopupPosition
{
    internal static Point Calculate(Rectangle anchor,Size panel,Rectangle work,PopupPlacement placement,int gap=8)
    {
        var left=new Point(anchor.Left-panel.Width-gap,anchor.Bottom-panel.Height);
        var above=new Point(anchor.Right-panel.Width,anchor.Top-panel.Height-gap);
        var position=placement==PopupPlacement.Above?above:left;
        // Keep the chosen preference, but use the other side when it is the only
        // one with enough space. Both directions remain inside the working area.
        if(placement==PopupPlacement.Left&&left.X<work.Left&&above.Y>=work.Top)position=above;
        if(placement==PopupPlacement.Above&&above.Y<work.Top&&left.X>=work.Left)position=left;
        return new(Math.Clamp(position.X,work.Left,Math.Max(work.Left,work.Right-panel.Width)),Math.Clamp(position.Y,work.Top,Math.Max(work.Top,work.Bottom-panel.Height)));
    }
}
