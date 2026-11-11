namespace NestedTray;
internal sealed class EntryArrowMotion
{
    private double from,target;
    private long started;
    private int duration;
    internal bool Active(long now)=>duration>0&&now-started<duration;
    internal double Angle(long now)
    {
        if(duration==0)return target;
        double t=Math.Clamp((now-started)/(double)duration,0,1),eased=t*t*(3-2*t);
        return from+(target-from)*eased;
    }
    internal void SetTarget(bool expanded,long now,bool animate)
    {
        from=Angle(now);target=expanded?180:0;started=now;
        duration=animate?(int)Math.Round(160*Math.Abs(target-from)/180):0;
    }
    internal void Reset(){from=target=0;started=0;duration=0;}
}
