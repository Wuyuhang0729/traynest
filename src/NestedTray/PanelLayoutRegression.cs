using System.Text.Json;

namespace NestedTray;
internal static class PanelLayoutRegression
{
    internal static int Run(string directory)
    {
        Directory.CreateDirectory(directory);
        var results=new List<object>();bool success=true;
        using var panel=new TrayPanel();var work=Screen.PrimaryScreen!.WorkingArea;
        panel.EntryRectangle=new Rectangle(work.Right-40,work.Bottom-8,16,16);
        var records=new List<IconRecord>();
        foreach(int count in new[]{0,1,9,17,36,64,65,96,9})
        {
            foreach(var old in records)old.Dispose();records.Clear();
            for(int index=0;index<count;index++)records.Add(new IconRecord{Key=$"{count}-{index}",Selected=true,Image=SystemIcons.Information.ToBitmap(),Tooltip="Layout fixture"});
            panel.Render(records);panel.Open();Application.DoEvents();
            var grid=panel.Controls.OfType<FlowLayoutPanel>().Single();var buttons=grid.Controls.OfType<Button>().ToArray();
            bool fits=buttons.All(button=>grid.ClientRectangle.Contains(button.Bounds));
            bool correct=count<=64?fits&&!grid.HorizontalScroll.Visible&&!grid.VerticalScroll.Visible:!grid.HorizontalScroll.Visible&&!grid.VerticalScroll.Visible&&panel.HasOverflowFade;
            bool nativeSizes=buttons.All(button=>button.Size==new Size(panel.CellPixels,panel.CellPixels)&&button.Image?.Size==new Size(panel.IconPixels,panel.IconPixels));
            success&=correct&&nativeSizes;
            results.Add(new {Count=count,Surface=panel.ClientSize.ToString(),AllTilesInside=fits,HorizontalScroll=grid.HorizontalScroll.Visible,VerticalScroll=grid.VerticalScroll.Visible,NativeSizes=nativeSizes,Passed=correct&&nativeSizes});
            if(count==96)
            {
                using var image=new Bitmap(panel.Width,panel.Height);panel.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));
                // DrawToBitmap paints overlapping siblings in reverse order.
                // Composite the foreground cue last, matching the live HWND order.
                var cue=panel.Controls.OfType<OverflowFade>().Single();
                using var layer=new Bitmap(cue.Width,cue.Height);cue.DrawToBitmap(layer,new Rectangle(Point.Empty,layer.Size));
                using(var graphics=Graphics.FromImage(image))graphics.DrawImageUnscaled(layer,cue.Location);
                layer.Save(Path.Combine(directory,"overflow-cue.png"));image.Save(Path.Combine(directory,"panel-overflow.png"));
            }
            if(count>64){panel.ScrollToLastIcon();Application.DoEvents();bool endVisible=grid.ClientRectangle.Contains(buttons.Last().Bounds)&&!panel.HasOverflowFade;success&=endVisible;results.Add(new{Count=count,LastIconVisibleAtEnd=endVisible});}
            if(count==17)
            {
                var tile=buttons[0];var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                tile.GetType().GetMethod("OnMouseEnter",flags)!.Invoke(tile,[EventArgs.Empty]);
                using var hover=new Bitmap(tile.Width,tile.Height);tile.DrawToBitmap(hover,new Rectangle(Point.Empty,hover.Size));
                bool rounded=hover.GetPixel(0,0).ToArgb()==tile.BackColor.ToArgb()&&hover.GetPixel(8,8).ToArgb()!=tile.BackColor.ToArgb();
                tile.GetType().GetMethod("OnMouseLeave",flags)!.Invoke(tile,[EventArgs.Empty]);
                using var normal=new Bitmap(tile.Width,tile.Height);tile.DrawToBitmap(normal,new Rectangle(Point.Empty,normal.Size));
                bool cleared=normal.GetPixel(8,8).ToArgb()==tile.BackColor.ToArgb();success&=rounded&&cleared;
                results.Add(new{RoundedHoverCorners=rounded,HoverClearsOnLeave=cleared});hover.Save(Path.Combine(directory,"rounded-hover-tile.png"));
                tile.GetType().GetMethod("OnMouseEnter",flags)!.Invoke(tile,[EventArgs.Empty]);
                using var image=new Bitmap(panel.Width,panel.Height);panel.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(directory,"panel-17-icons.png"));
            }
        }
        using var cover=new Form{TopMost=true,ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Bounds=panel.Bounds};
        cover.Show();Application.DoEvents();nint foreground=Native.GetForegroundWindow();
        panel.EnsureAboveApplications();Application.DoEvents();
        var point=new Native.Point{X=panel.Left+2,Y=panel.Top+2};
        bool above=Native.GetAncestor(Native.WindowFromPoint(point),2)==panel.Handle;
        bool noActivation=Native.GetForegroundWindow()==foreground;success&=above&&noActivation;
        foreach(var record in records)record.Dispose();
        var output=new {Passed=success,Cases=results,AboveAnotherTopmostWindow=above,DoesNotStealForeground=noActivation};
        File.WriteAllText(Path.Combine(directory,"panel-layout-test.json"),JsonSerializer.Serialize(output,new JsonSerializerOptions{WriteIndented=true}));
        return success?0:1;
    }
}
