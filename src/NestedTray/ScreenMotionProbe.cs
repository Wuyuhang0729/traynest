using System.Diagnostics;

namespace NestedTray;

// Track a high-contrast 9x9 patch from each settled panel in actual desktop frames.
internal sealed class ScreenMotionProbe
{
    private readonly Rectangle area;
    private readonly (Point point, Color[] pixels)[] patches;
    internal ScreenMotionProbe(Rectangle collection, Rectangle system, int workBottom)
    {
        area=Rectangle.Union(collection,system);
        area.Height=workBottom-area.Top;
        using var reference=Capture();
        patches=new[]{Pick(reference,collection),Pick(reference,system)};
    }
    private Bitmap Capture()
    {
        var frame=new Bitmap(area.Width,area.Height);
        using var graphics=Graphics.FromImage(frame);
        graphics.CopyFromScreen(area.Location,Point.Empty,area.Size);
        return frame;
    }
    private (Point,Color[]) Pick(Bitmap frame,Rectangle panel)
    {
        panel.Offset(-area.X,-area.Y);
        Point best=new(panel.Left+6,panel.Top+6);
        int bestScore=-1;
        for(int y=panel.Top+6;y<panel.Bottom-14;y+=4)
        for(int x=panel.Left+6;x<panel.Right-14;x+=4)
        {
            int score=0;
            for(int n=0;n<8;n++)
                score+=Difference(frame.GetPixel(x+n,y),frame.GetPixel(x+n,y+8));
            if(score>bestScore){bestScore=score;best=new Point(x,y);}
        }
        var pixels=new Color[81];
        for(int i=0;i<pixels.Length;i++)pixels[i]=frame.GetPixel(best.X+i%9,best.Y+i/9);
        return(best,pixels);
    }
    private static int Difference(Color a,Color b)=>Math.Abs(a.R-b.R)+Math.Abs(a.G-b.G)+Math.Abs(a.B-b.B);
    private static int? Offset(Bitmap frame,(Point point,Color[] pixels) patch)
    {
        int best=-1,error=int.MaxValue;
        for(int y=patch.point.Y;y<=frame.Height-9;y++)
        {
            int score=0;
            for(int i=0;i<81&&score<error;i++)
                score+=Difference(patch.pixels[i],frame.GetPixel(patch.point.X+i%9,y+i/9));
            if(score<error){error=score;best=y-patch.point.Y;}
        }
        return error<81*38?best:null;
    }
    internal void Record(string path,Action trigger)
    {
        // Sampling runs separately so pixel matching cannot stall either panel's animation.
        var frames=new List<(double time,Bitmap bitmap)>();
        var clock=Stopwatch.StartNew();
        var capture=Task.Run(()=>{
            while(clock.ElapsedMilliseconds<520)
            {
                frames.Add((clock.Elapsed.TotalMilliseconds,Capture()));
                Thread.Sleep(10);
            }
        });
        trigger();
        while(!capture.IsCompleted){Application.DoEvents();Thread.Sleep(1);}
        capture.GetAwaiter().GetResult();
        using var output=new StreamWriter(path);
        output.WriteLine("time_ms,collection_offset_px,system_offset_px");
        string images=Path.ChangeExtension(path,null)+"-frames";Directory.CreateDirectory(images);int index=0;
        foreach(var (time,bitmap) in frames)
            using(bitmap){output.WriteLine(FormattableString.Invariant($"{time:F1},{Offset(bitmap,patches[0])},{Offset(bitmap,patches[1])}"));bitmap.Save(Path.Combine(images,$"{index++:D3}-{time:F1}ms.png"));}
    }
}
