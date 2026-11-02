using System.Diagnostics;
using System.Text.Json;

namespace NestedTray;
internal static class ReorderMotionRegression
{
    internal static int Run(string directory)
    {
        Directory.CreateDirectory(directory);var checks=new Dictionary<string,bool>();var errors=new List<string>();
        var records=Enumerable.Range(0,10).Select(i=>new IconRecord{Key="motion:"+i,Rule="motion|"+i,Selected=true,Image=SystemIcons.Information.ToBitmap()}).ToArray();
        try
        {
            using var panel=new TrayPanel{UseSavedOrder=true,FixedColumns=5};panel.Render(records);panel.Location=new Point(-20000,-20000);panel.Show();Pump(30);
            var grid=panel.Controls.OfType<IconGrid>().Single();var source=grid.Controls[9];var neighbor=grid.Controls[0];
            var start=source.Bounds;var end=neighbor.Bounds;var neighborStart=neighbor.Bounds;
            bool enabled=SystemAnimation.Enabled;
            grid.MoveIcon(source,0);
            checks["AnimationFollowsSystemPreference"]=grid.IsAnimatingOrder==enabled;
            checks["MoveStartsWithoutPositionJump"]=!enabled||source.Bounds==start&&neighbor.Bounds==neighborStart;
            Pump(45);var intermediate=source.Bounds;
            checks["SourceHasIntermediatePosition"]=!enabled||intermediate!=start&&intermediate!=end;
            checks["NeighborAlsoMovesGradually"]=!enabled||neighbor.Bounds!=neighborStart&&neighbor.Left!=neighborStart.Left+neighbor.Width;
            Capture("motion-midway");
            var order=grid.Controls.Cast<Control>().Select(tile=>(IconRecord)tile.Tag!).ToArray();panel.Render(order);
            checks["IconRefreshDoesNotSnapActiveMotion"]=!enabled||source.Bounds==intermediate&&grid.IsAnimatingOrder;
            var retargetStart=source.Bounds;grid.MoveIcon(source,7);
            checks["RapidRetargetContinuesFromCurrentPosition"]=!enabled||source.Bounds==retargetStart;
            Pump(230);
            var target=new Rectangle(2*source.Width,source.Height,source.Width,source.Height);
            checks["MotionFinishesAtExactCell"]=source.Bounds==target&&!grid.IsAnimatingOrder;
            checks["PointerRemainsDefault"]=source.Cursor==Cursors.Default&&grid.Cursor==Cursors.Default;
            var prior=source.Bounds;
            grid.AnimateOrder(()=>{for(int i=0;i<records.Length;i++)grid.Controls.SetChildIndex(grid.Controls.Cast<Control>().Single(tile=>ReferenceEquals(tile.Tag,records[i])),i);});
            checks["RollbackStartsWithoutPositionJump"]=!enabled||source.Bounds==prior;
            Pump(230);checks["RollbackReturnsAllIconsToOriginalCells"]=grid.Controls.Cast<Control>().Select((tile,index)=>tile.Bounds==new Rectangle(index%5*40,index/5*40,40,40)).All(value=>value);
            checks["NoFloatingDuplicateOrScrollbar"]=panel.Controls.Count==3&&!grid.HorizontalScroll.Visible&&!grid.VerticalScroll.Visible;
            void Capture(string name){using var bitmap=new Bitmap(panel.Width,panel.Height);panel.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(directory,name+".png"));}
        }
        catch(Exception error){errors.Add(error.ToString());}
        finally{foreach(var record in records)record.Dispose();}
        bool passed=checks.Values.All(value=>value)&&errors.Count==0;
        File.WriteAllText(Path.Combine(directory,"reorder-motion-test.json"),JsonSerializer.Serialize(new{Passed=passed,Checks=checks,Errors=errors,Scope="Offscreen production grid animation, intermediate positions and retargeting; no physical input, startup changes or Explorer takeover"},DiagnosticStore.JsonOptions));return passed?0:1;
    }
    private static void Pump(int ms){var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(5);}}
}
