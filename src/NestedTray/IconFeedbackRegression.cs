using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace NestedTray;
internal static class IconFeedbackRegression
{
    [DllImport("user32.dll")]private static extern nint GetTopWindow(nint window);
    internal static int Run(string directory)
    {
        Directory.CreateDirectory(directory);var checks=new Dictionary<string,bool>();var errors=new List<string>();
        var records=Enumerable.Range(0,10).Select(i=>new IconRecord{Key="feedback:"+i,Rule="feedback|"+i,Selected=true,Image=(i%2==0?SystemIcons.Information:SystemIcons.Warning).ToBitmap()}).ToArray();
        using var incoming=new IconRecord{Key="incoming",Callback=1,Image=SystemIcons.Warning.ToBitmap()};
        try
        {
            using var panel=new TrayPanel{UseSavedOrder=true,FixedColumns=5};panel.Render(records);
            panel.Location=new Point(-20000,-20000);panel.Show();Pump(30);
            var grid=panel.Controls.OfType<IconGrid>().Single();var tile=(Button)grid.Controls[9];
            void Invoke(string method,params object[] args)=>typeof(TrayPanel).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(panel,args);
            void Hover(bool on)=>tile.GetType().GetMethod(on?"OnMouseEnter":"OnMouseLeave",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(tile,[EventArgs.Empty]);
            Bitmap Shot(Control control){var image=new Bitmap(control.Width,control.Height);control.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));return image;}
            using var board=new Form{FormBorderStyle=FormBorderStyle.None,ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,TopMost=true,BackColor=Color.FromArgb(167,201,230),Bounds=new Rectangle(80,80,480,260)};
            board.Show();
            void ScreenShot(string name)
            {
                panel.Location=new Point(board.Left+100,board.Top+80);panel.Show();panel.EnsureAboveApplications();Pump(100);
                using var bitmap=new Bitmap(panel.Width,panel.Height);using(var g=Graphics.FromImage(bitmap))g.CopyFromScreen(panel.Location,Point.Empty,panel.Size);
                bitmap.Save(Path.Combine(directory,name+"-screen.png"));
            }
            foreach(var mode in new[]{PanelAppearance.Light,PanelAppearance.Dark,PanelAppearance.Acrylic})
            {
                panel.SetAppearance(mode);Hover(false);ScreenShot(mode+"-idle");using var idle=Shot(tile);idle.Save(Path.Combine(directory,mode+"-idle.png"));
                Hover(true);ScreenShot(mode+"-hover");using var hover=Shot(tile);hover.Save(Path.Combine(directory,mode+"-hover.png"));
                checks[mode+"CenterUnchanged"]=idle.GetPixel(10,20)==hover.GetPixel(10,20);
                checks[mode+"EdgeVisibleOnOpaqueSurface"]=panel.AcrylicActive||idle.GetPixel(2,20)!=hover.GetPixel(2,20);
                Hover(false);ScreenShot(mode+"-leave");using var leave=Shot(tile);checks[mode+"LeaveRestores"]=Equal(idle,leave);
                panel.Hide();panel.SetIncomingIcon(incoming);ScreenShot(mode+"-incoming");Pump(20);int invalidations=0;grid.Invalidated+=Count;
                for(int i=0;i<100;i++)panel.SetIncomingIcon(incoming);
                checks[mode+"RepeatedObservationDoesNotInvalidate"]=invalidations==0&&panel.HasIncomingIconPreview;
                using(var preview=Shot(panel))preview.Save(Path.Combine(directory,mode+"-incoming.png"));
                incoming.UpdateImage(SystemIcons.Information.Handle);panel.SetIncomingIcon(incoming);
                checks[mode+"DynamicSourceStillUpdates"]=invalidations>0;
                grid.Invalidated-=Count;panel.SetIncomingIcon(null);
                checks[mode+"LeaveOrCancelClearsPreview"]=!panel.HasIncomingIconPreview&&!panel.NativeDragFeedback;
                panel.SetIncomingIcon(incoming);incoming.Selected=true;panel.SetIncomingIcon(incoming);
                checks[mode+"CollectedSourceClearsPreview"]=!panel.HasIncomingIconPreview;incoming.Selected=false;
                void Count(object? sender,InvalidateEventArgs e)=>invalidations++;
            }
            board.Hide();panel.Location=new Point(-20000,-20000);
            var order=grid.Controls.Cast<Control>().ToArray();
            var from=tile.PointToScreen(new Point(20,20));Invoke("BeginTilePress",tile,from);
            foreach(int index in new[]{0,4,7,2,9,1})
            {
                var target=grid.PointToScreen(new Point(index%5*40+20,index/5*40+20));
                Invoke("MoveTilePress",tile,target);
                for(int frame=0;frame<8;frame++)
                {
                    Pump(15);checks["ActiveTileAboveSiblings"+index+"-"+frame]=GetTopWindow(grid.Handle)==tile.Handle;
                    checks["LogicalOrderPreserved"+index+"-"+frame]=grid.Controls.GetChildIndex(tile)==index;
                }
            }
            Invoke("FinishTilePress",tile,Point.Empty,true);Pump(230);
            checks["CancelRestoresOriginalOrder"]=grid.Controls.Cast<Control>().SequenceEqual(order);
            checks["CancelRestoresNativeOrder"]=GetTopWindow(grid.Handle)==grid.Controls[0].Handle;

            checks["CellAndIconSizesUnchanged"]=tile.Size==new Size(40,40)&&tile.Image!.Size==new Size(16,16);
        }
        catch(Exception error){errors.Add(error.ToString());}
        finally{foreach(var record in records)record.Dispose();}
        bool passed=errors.Count==0&&checks.Values.All(v=>v);
        File.WriteAllText(Path.Combine(directory,"icon-feedback-test.json"),JsonSerializer.Serialize(new{Passed=passed,Checks=checks,Errors=errors,Scope="Production panels with own color board screen captures; synthetic hover and reorder paths, real native child z-order; no physical input or Explorer takeover. Only *-screen.png captures include composed acrylic."},DiagnosticStore.JsonOptions));return passed?0:1;
    }
    private static bool Equal(Bitmap a,Bitmap b){for(int y=0;y<a.Height;y++)for(int x=0;x<a.Width;x++)if(a.GetPixel(x,y)!=b.GetPixel(x,y))return false;return true;}
    private static void Pump(int ms){var watch=Stopwatch.StartNew();while(watch.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(1);}}
}
