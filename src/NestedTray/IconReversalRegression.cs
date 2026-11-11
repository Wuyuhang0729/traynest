using System.Diagnostics;
using System.Text.Json;

namespace NestedTray;

internal static class IconReversalRegression
{
    internal static int Run(string output)
    {
        Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("NESTEDTRAY_TEST_PROFILE",Path.GetFullPath(output));
        var background=Color.FromArgb(30,55,81);
        using var board=new Form{BackColor=background,FormBorderStyle=FormBorderStyle.None,
            StartPosition=FormStartPosition.CenterScreen,Size=new Size(540,400),ShowInTaskbar=false};
        board.Paint+=(_,e)=>{ for(int x=50;x<260;x+=12)e.Graphics.FillRectangle(Brushes.Goldenrod,x,302,6,60); };
        board.Show();Application.DoEvents();
        using var panel=new TrayPanel{Placement=PopupPlacement.Above};
        int edge=board.Top+300;
        panel.EntryRectangle=new Rectangle(board.Left+50,edge,100,40);
        panel.SetAppearance(PanelAppearance.Acrylic);
        var records=Enumerable.Range(0,15).Select(i=>new IconRecord{Key=$"tile-{i}",Selected=true,Image=new Bitmap(16,16)}).ToArray();
        void Mark(IconRecord r,Color color){using var g=Graphics.FromImage(r.Image!);g.Clear(color);}
        Color[] colors=[Color.Magenta,Color.Red,Color.Cyan];
        for(int i=0;i<records.Length;i++)Mark(records[i],colors[i/5]);
        panel.FixedColumns=5;panel.Render(records);
        int canvasHeight=panel.Height;
        int[] rowSamples=new int[3];
        int exposed=0,missing=0,white=0,greenDuringMotion=0,coveredPattern=0;
        var sampleTimes=new List<long>();
        void Observe(int duration)
        {
            var timer=Stopwatch.StartNew();
            while(timer.ElapsedMilliseconds<duration)
            {
                Application.DoEvents();
                if(panel.Visible&&panel.IsAnimating)
                {
                    var area=new Rectangle(panel.Left,edge-canvasHeight-10,panel.Width,canvasHeight+40);
                    using var bitmap=new Bitmap(area.Width,area.Height);
                    using(var g=Graphics.FromImage(bitmap))g.CopyFromScreen(area.Location,Point.Empty,area.Size);
                    int[] counts=new int[3];int green=0;
                    for(int y=0;y<edge-area.Top;y++)
                        for(int x=8;x<Math.Min(52,bitmap.Width);x++)
                        {
                            var pixel=bitmap.GetPixel(x,y);
                            if(pixel.R>150&&pixel.B>150&&pixel.G<100)counts[0]++;
                            if(pixel.G>150&&pixel.R<100&&pixel.B<100){green++;counts[0]++;}
                            if(pixel.R>150&&pixel.G<100&&pixel.B<100)counts[1]++;
                            if(pixel.G>150&&pixel.B>150&&pixel.R<100)counts[2]++;
                        }
                    for(int row=0;row<3;row++)
                        if(edge-panel.Top>=panel.CellPixels*(row+.5)+14 && timer.ElapsedMilliseconds>32)
                        {rowSamples[row]++;exposed++;if(counts[row]<8)missing++;}
                    if(green>=8)greenDuringMotion++;
                    // Behind the clipping boundary the sharp stripes must stay visible.
                    int sharp=0;
                    for(int x=12;x<180;x+=12)
                        if(bitmap.GetPixel(x+2,edge-area.Top+15).R-bitmap.GetPixel(x+8,edge-area.Top+15).R>100)sharp++;
                    if(sharp<10)coveredPattern++;
                    for(int y=edge-area.Top-1;y<bitmap.Height;y++)
                    {
                        int bright=0;
                        for(int x=8;x<bitmap.Width-8;x+=2)
                        {
                            var p=bitmap.GetPixel(x,y);
                            if(Math.Min(p.R,Math.Min(p.G,p.B))>210)bright++;
                        }
                        if(bright>bitmap.Width/6){white++;break;}
                    }
                    sampleTimes.Add(timer.ElapsedMilliseconds);
                }
                Thread.Sleep(4);
            }
        }
        panel.Open();Observe(70);
        bool interruptOpen=panel.IsOpening;
        panel.Dismiss();Observe(90);
        bool interruptClose=panel.IsDismissing;
        panel.Open();Mark(records[0],Color.Lime);panel.Render(records);Observe(300);
        panel.Dismiss();Observe(135);panel.Open();Observe(300);
        bool passed=interruptOpen&&interruptClose&&exposed>=3&&greenDuringMotion>0&&rowSamples.All(n=>n>0)&&coveredPattern==0&&missing==0&&white==0&&panel.Visible&&!panel.IsAnimating;
        File.WriteAllText(Path.Combine(output,"icon-reversal.json"),JsonSerializer.Serialize(new{
            passed,interruptOpen,interruptClose,samples=sampleTimes.Count,rowSamples,coveredPattern,exposed,missing,white,greenDuringMotion
        }));
        foreach(var record in records)record.Dispose();
        return passed?0:1;
    }
}
