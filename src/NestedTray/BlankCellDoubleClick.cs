namespace NestedTray;
internal sealed class BlankCellDoubleClick
{
    private bool armed;private nint window;private Point first;private long releasedAt;
    internal bool Armed=>armed;
    internal void Reset(){armed=false;}
    internal bool CanPair(nint source,Point point,long now,int interval,Size area)=>armed&&source==window&&now>=releasedAt&&now-releasedAt<=interval&&Math.Abs(point.X-first.X)<=area.Width/2&&Math.Abs(point.Y-first.Y)<=area.Height/2;
    internal bool Observe(nint source,Point point,long now,int interval,Size area,out Point pairStart)
    {
        pairStart=first;
        bool pair=CanPair(source,point,now,interval,area);
        if(pair){armed=false;return true;}
        armed=true;window=source;first=point;releasedAt=now;return false;
    }
}
