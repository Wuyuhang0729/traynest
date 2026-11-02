namespace NestedTray;
internal readonly record struct ContentTrayLayout(Size Surface,int Columns,int ContentRows,bool Scroll)
{
    internal static ContentTrayLayout Measure(int count,int cell,int padding,Size work,int preferredColumns=0)
    {
        int maxColumns=Math.Clamp((work.Width-16-padding*2)/cell,1,8);
        int maxRows=Math.Clamp((work.Height-16-padding*2)/cell,1,8);
        int minColumns=Math.Min(5,maxColumns),minRows=Math.Min(2,maxRows);
        int columns=preferredColumns>0?Math.Clamp(preferredColumns,1,maxColumns):Math.Clamp((int)Math.Ceiling(Math.Sqrt(Math.Max(1,count))),minColumns,maxColumns);
        int contentRows=(Math.Max(1,count)+columns-1)/columns;
        int rows=Math.Clamp(contentRows,minRows,maxRows);
        bool scroll=contentRows>rows;
        return new(new Size(columns*cell+padding*2,rows*cell+padding*2),columns,contentRows,scroll);
    }
}
