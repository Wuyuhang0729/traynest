using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace NestedTray;
internal static class FramePaintRegression
{
    [DllImport("dwmapi.dll")]private static extern int DwmFlush();
    [DllImport("user32.dll")]private static extern bool RedrawWindow(nint window,nint rect,nint region,uint flags);
    private sealed class PaintProbe:NativeWindow,IDisposable
    {
        private readonly Action sample;
        internal PaintProbe(nint window,Action sample){this.sample=sample;AssignHandle(window);}
        protected override void WndProc(ref Message m){base.WndProc(ref m);if(m.Msg==0xF)sample();}
        public void Dispose()=>ReleaseHandle();
    }
    internal static int Run(string directory)
    {
        Directory.CreateDirectory(directory);var checks=new Dictionary<string,bool>();var errors=new List<string>();var samples=new List<object>();
        var records=Enumerable.Range(0,14).Select(i=>new IconRecord{Key="frame-paint:"+i,Selected=true,Image=Marker(Color.Red)}).ToArray();
        try
        {
            using var board=new Form{ShowInTaskbar=false,FormBorderStyle=FormBorderStyle.None,StartPosition=FormStartPosition.Manual,Bounds=new Rectangle(100,250,600,400)};
            board.Paint+=(_,e)=>{using var red=new SolidBrush(Color.Red);using var blue=new SolidBrush(Color.Blue);e.Graphics.FillRectangle(red,0,0,300,400);e.Graphics.FillRectangle(blue,300,0,300,400);};
            board.Show();
            using var panel=new TrayPanel{FixedColumns=5,Placement=PopupPlacement.Above,EntryRectangle=new Rectangle(350,500,16,16),DismissSettings=new(){Enabled=false}};
            panel.Render(records);
            foreach(var mode in new[]{PanelAppearance.Dark,PanelAppearance.Light,PanelAppearance.Acrylic})
            {
                panel.SetAppearance(mode);panel.Open();
                // Keep the reference board immediately behind our own panel;
                // foreground restrictions must not substitute the user's app
                // for the intended red/blue material reference.
                Native.SetWindowPos(board.Handle,new nint(-1),0,0,0,0,0x13);
                Native.SetWindowPos(panel.Handle,new nint(-1),0,0,0,0,0x13);
                board.Refresh();Pump(180);
                using(var reference=new Bitmap(board.Width,board.Height))
                {
                    using(var g=Graphics.FromImage(reference))g.CopyFromScreen(board.Location,Point.Empty,board.Size);
                    var red=reference.GetPixel(40,40);var blue=reference.GetPixel(board.Width-40,40);
                    if(red.R<200||red.B>60||blue.B<200||blue.R>60)throw new InvalidOperationException("Own material reference board is occluded");
                }
                if(mode==PanelAppearance.Acrylic&&panel.AcrylicActive)
                {
                    DwmFlush();using var backdrop=new Bitmap(panel.Width,panel.Height);
                    using(var g=Graphics.FromImage(backdrop))g.CopyFromScreen(panel.Location,Point.Empty,panel.Size);
                    var left=backdrop.GetPixel(24,3);var right=backdrop.GetPixel(panel.Width-24,3);
                    checks["AcrylicStillRespondsToBackdrop"]=Math.Abs(left.R-right.R)+Math.Abs(left.G-right.G)+Math.Abs(left.B-right.B)>15;
                    samples.Add(new{Phase="backdrop",Left=left.ToArgb(),Right=right.ToArgb()});
                }
                var buttons=panel.Controls.OfType<IconGrid>().Single().Controls.OfType<Button>().ToArray();
                checks[mode+"-NativeInputTargetsRetained"]=buttons.All(button=>
                {
                    var point=button.PointToScreen(new Point(button.Width/2,button.Height/2));
                    return Native.WindowFromPoint(new Native.Point{X=point.X,Y=point.Y})==button.Handle;
                });
                int clicks=0;panel.IconInput=(_,_,_)=>clicks++;
                // Tiles intentionally cannot take keyboard focus, so
                // Button.PerformClick's CanSelect gate is not their input path.
                buttons[0].GetType().GetMethod("OnClick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(buttons[0],[EventArgs.Empty]);
                checks[mode+"-SimulatedButtonCallbackRetained"]=clicks==1;
                bool armed=false,mixed=false;int observed=0;
                (int Red,int Blue) Colors()
                {
                    using var image=new Bitmap(panel.Width,panel.Height);
                    using(var g=Graphics.FromImage(image))g.CopyFromScreen(panel.Location,Point.Empty,panel.Size);
                    int red=0,blue=0;
                    foreach(var button in buttons)
                    {
                        var p=panel.PointToClient(button.PointToScreen(new Point(button.Width/2,button.Height/2)));
                        var color=image.GetPixel(p.X,p.Y);
                        if(color.R>180&&color.B<70)red++;
                        if(color.B>180&&color.R<70)blue++;
                    }
                    return(red,blue);
                }
                void Sample()
                {
                    if(!armed)return;
                    // Test only: allow composition between individual child
                    // paints, rather than sampling solely after the full pass.
                    DwmFlush();var colors=Colors();observed++;
                    mixed|=colors.Red>0&&colors.Blue>0;
                    samples.Add(new{Mode=mode.ToString(),colors.Red,colors.Blue});
                }
                var probes=buttons.Select(b=>new PaintProbe(b.Handle,Sample)).ToArray();
                try
                {
                    foreach(var color in new[]{Color.Blue,Color.Red})
                    {
                        armed=true;
                        foreach(var record in records){var old=record.Image;record.Image=Marker(color);old?.Dispose();}
                        panel.Render(records);RedrawWindow(panel.Handle,0,0,0x185);armed=false;Pump(100);
                        DwmFlush();var settled=Colors();checks[mode+"-"+color.Name+"FullyPresented"]=color==Color.Red?settled.Red==14:settled.Blue==14;
                        samples.Add(new{Phase="settled",Mode=mode.ToString(),Target=color.Name,settled.Red,settled.Blue});
                    }
                }
                finally{armed=false;foreach(var probe in probes)probe.Dispose();panel.Hide();}
                checks[mode+"-ChildPaintsDoNotExposeMixedGenerations"]=!mixed&&observed>=14;
            }
        }
        catch(Exception error){errors.Add(error.ToString());}
        finally{foreach(var record in records)record.Dispose();}
        bool passed=errors.Count==0&&checks.Values.All(x=>x);
        File.WriteAllText(Path.Combine(directory,"frame-paint-test.json"),JsonSerializer.Serialize(new{Passed=passed,Checks=checks,Samples=samples,Errors=errors,Scope="14 own color icons; force test-only composition between child paint callbacks and inspect complete old/new generations. Static repaint atomicity probe, not natural moving tear reproduction. No mouse, Explorer or user settings."},DiagnosticStore.JsonOptions));return passed?0:1;
    }
    private static Bitmap Marker(Color color){var image=new Bitmap(16,16);using var g=Graphics.FromImage(image);g.Clear(color);return image;}
    private static void Pump(int ms){var c=Stopwatch.StartNew();while(c.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(2);}}
}
