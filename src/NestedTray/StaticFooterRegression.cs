using System.Diagnostics;
using System.Text.Json;

namespace NestedTray;
internal static class StaticFooterRegression
{
    internal static int Run(string directory)
    {
        Directory.CreateDirectory(directory);var checks=new Dictionary<string,bool>();var errors=new List<string>();var measurements=new List<object>();
        var records=Enumerable.Range(0,14).Select(i=>new IconRecord{Key="footer:"+i,Rule="footer|"+i,Selected=true,Image=Glyph(i)}).ToArray();
        try
        {
            using var board=new Form{ShowInTaskbar=false,FormBorderStyle=FormBorderStyle.None,StartPosition=FormStartPosition.Manual,Bounds=new Rectangle(80,80,500,500),BackColor=Color.FromArgb(90,90,90),TopMost=true};board.Show();
            using var panel=new TrayPanel{UseSavedOrder=true,FixedColumns=5,Placement=PopupPlacement.Above,EntryRectangle=new Rectangle(300,460,16,16)};
            // Model dirty child pixels left in the uncovered margin during resizing.
            // The settled surface must repaint them, even when intermediate frames avoid erasing.
            panel.MotionFrame+=(_,_)=>
            {
                if(panel.IsOpening&&panel.Height==128)
                {using var g=Graphics.FromHwnd(panel.Handle);using var brush=new SolidBrush(Color.Blue);g.FillRectangle(brush,12,panel.Height-3,160,2);}
            };
            foreach(var mode in new[]{PanelAppearance.Acrylic,PanelAppearance.Dark,PanelAppearance.Light})
            {
                panel.SetAppearance(mode);
                for(int cycle=0;cycle<3;cycle++)
                {
                    panel.Hide();panel.Render(cycle==1?records.Take(5):records);panel.Open();Pump(60);
                    panel.Render(records);Pump(400);
                    for(int frame=0;frame<20;frame++){foreach(var record in records){var old=record.Image;record.Image=Glyph(frame);old?.Dispose();}panel.Render(records);Pump(25);}
                    using var shot=new Bitmap(panel.Width,panel.Height);using(var g=Graphics.FromImage(shot))g.CopyFromScreen(panel.Location,Point.Empty,panel.Size);
                    shot.Save(Path.Combine(directory,$"{mode}-{cycle}.png"));
                    var grid=panel.Controls.OfType<IconGrid>().Single();int colored=0;
                    for(int y=grid.Bottom;y<shot.Height;y++)for(int x=8;x<shot.Width-8;x++)
                    {var c=shot.GetPixel(x,y);if(Math.Max(c.R,Math.Max(c.G,c.B))-Math.Min(c.R,Math.Min(c.G,c.B))>50)colored++;}
                    checks[$"{mode}-{cycle}-FooterHasNoIconPixels"]=colored==0;
                    checks[$"{mode}-{cycle}-SettledSize"]=panel.Height==128&&!panel.IsAnimating&&!panel.HasOverflowFade;
                    measurements.Add(new{Mode=mode.ToString(),Cycle=cycle,ColoredFooterPixels=colored,panel.AcrylicActive,Panel=panel.Size.ToString(),Grid=grid.Bounds.ToString()});
                    panel.Dismiss();Pump(260);
                }
            }
        }
        catch(Exception error){errors.Add(error.ToString());}
        finally{foreach(var record in records)record.Dispose();}
        bool passed=errors.Count==0&&checks.Values.All(v=>v);
        File.WriteAllText(Path.Combine(directory,"static-footer-test.json"),JsonSerializer.Serialize(new{Passed=passed,Checks=checks,Measurements=measurements,Errors=errors,Scope="Own colored fixture icons on neutral board; actual composed screen after animation and layout growth with injected dirty margin pixels; no mouse, native tray takeover or real profile writes"},DiagnosticStore.JsonOptions));return passed?0:1;
    }
    private static Bitmap Glyph(int index){var b=new Bitmap(16,16);using var g=Graphics.FromImage(b);g.Clear(Color.Transparent);using var brush=new SolidBrush(index%2==0?Color.FromArgb(0,100,255):Color.FromArgb(255,160,0));g.FillRectangle(brush,2,2,12,12);return b;}
    private static void Pump(int ms){var watch=Stopwatch.StartNew();while(watch.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(3);}}
}
