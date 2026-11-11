using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace NestedTray;
internal static class MotionFooterRegression
{
    [DllImport("dwmapi.dll")]private static extern int DwmFlush();
    internal static int Run(string directory)
    {
        Directory.CreateDirectory(directory);var checks=new Dictionary<string,bool>();var frames=new List<object>();var errors=new List<string>();
        var records=Enumerable.Range(0,14).Select(i=>new IconRecord{Key="motion-footer:"+i,Rule="motion-footer|"+i,Selected=true,Image=SystemIcons.Information.ToBitmap()}).ToArray();
        try
        {
            using var board=new Form{ShowInTaskbar=false,FormBorderStyle=FormBorderStyle.None,StartPosition=FormStartPosition.Manual,Bounds=new Rectangle(80,80,500,500),BackColor=Color.FromArgb(90,90,90),TopMost=true};board.Show();
            using var panel=new TrayPanel{UseSavedOrder=true,FixedColumns=5,Placement=PopupPlacement.Above,EntryRectangle=new Rectangle(300,460,16,16),DismissSettings=new(){DurationMs=600}};
            panel.Render(records);
            foreach(var mode in new[]{PanelAppearance.Acrylic,PanelAppearance.Dark,PanelAppearance.Light})
            {
                panel.SetAppearance(mode);int opening=0,closing=0;bool clean=true;
                void Sample(Point point,long timestamp)
                {
                    // Partial rows at the reveal edge are expected. Only test the
                    // real padding once the complete 128px surface is exposed.
                    if(panel.Height!=128)return;
                    DwmFlush();using var shot=new Bitmap(panel.Width,panel.Height);
                    using(var g=Graphics.FromImage(shot))g.CopyFromScreen(panel.Location,Point.Empty,panel.Size);
                    int colored=0;
                    for(int y=124;y<128;y++)for(int x=8;x<shot.Width-8;x++)
                    {var c=shot.GetPixel(x,y);if(Math.Max(c.R,Math.Max(c.G,c.B))-Math.Min(c.R,Math.Min(c.G,c.B))>50)colored++;}
                    clean&=colored==0;
                    string direction=panel.IsOpening?"opening":"closing";
                    int index=panel.IsOpening?opening++:closing++;
                    shot.Save(Path.Combine(directory,$"{mode}-{direction}-{index:D3}.png"));
                    frames.Add(new{Mode=mode.ToString(),Direction=direction,Frame=index,ColoredFooterPixels=colored,panel.AcrylicActive,Point=point.ToString()});
                    // Represent stale child pixels from this frame, to be removed
                    // by the next moving frame, not just the final settlement.
                    using var surface=Graphics.FromHwnd(panel.Handle);using var brush=new SolidBrush(Color.Blue);
                    surface.FillRectangle(brush,12,125,160,2);
                }
                panel.MotionFrame+=Sample;
                panel.Open();Pump(800);panel.Dismiss();Pump(420);
                panel.MotionFrame-=Sample;
                checks[mode+"MovingFramesClearPreviousPixels"]=clean;
                checks[mode+"OpeningFramesCovered"]=opening>=2;
                checks[mode+"ClosingFramesCovered"]=closing>=1;
            }
        }
        catch(Exception error){errors.Add(error.ToString());}
        finally{foreach(var record in records)record.Dispose();}
        bool passed=errors.Count==0&&checks.Values.All(v=>v);
        File.WriteAllText(Path.Combine(directory,"motion-footer-test.json"),JsonSerializer.Serialize(new{Passed=passed,Checks=checks,Frames=frames,Errors=errors,Scope="Actual composed moving fixture panel; fault injection between frames, excludes legitimate partial rows; no mouse, Explorer takeover or real settings changes. DwmFlush is test-only."},DiagnosticStore.JsonOptions));return passed?0:1;
    }
    private static void Pump(int ms){var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(2);}}
}
