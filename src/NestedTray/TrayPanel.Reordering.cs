namespace NestedTray;

internal sealed partial class TrayPanel
{
    private readonly System.Windows.Forms.Timer reorderFeedback=new(){Interval=450};
    private IconTileButton? placementTile;
    internal bool HasReorderPreview=>pressedTile?.Reordering==true;
    private IconTileButton? pressedTile;
    private Point pressedScreen;
    private string[] reorderOriginal=[];
    private readonly System.Windows.Forms.Timer reorderFrames=new(){Interval=60};
    internal bool IsReordering=>internalDrag!=null;
    private bool reorderEscapeHeld;
    internal bool HasOutsideReorderHint{get;private set;}
    private void UpdateOutsideReorderHint(Point screen)
    {
        bool outside=internalDrag!=null&&!Bounds.Contains(screen);
        if(outside==HasOutsideReorderHint)return;
        HasOutsideReorderHint=outside;
        if(pressedTile==null)return;
        if(outside)tips.Show("松开移出收纳，Esc取消",pressedTile,new Point(0,pressedTile.Height),30000);
        else tips.Hide(pressedTile);
    }
    internal bool HandleReorderEscape(bool down)
    {
        if(!down){reorderEscapeHeld=false;return false;}
        if(IsReordering&&pressedTile!=null){reorderEscapeHeld=true;FinishTilePress(pressedTile,Point.Empty,true);}
        return reorderEscapeHeld;
    }

    private void BeginTilePress(IconTileButton tile,Point screen)
    {
        if(pressedTile!=null)FinishTilePress(pressedTile,Point.Empty,true);
        ClearPlacementFeedback();
        reorderEscapeHeld=false;tile.SuppressClick=false;pressedTile=tile;pressedScreen=screen;LastDragState="down";
        // Capture belongs to the original tile even as the preview changes its cell.
        tile.Capture=true;reorderFrames.Start();
    }
    private void MoveTilePress(IconTileButton tile,Point screen)
    {
        if(pressedTile!=tile)return;
        if(internalDrag==null)
        {
            var size=SystemInformation.DragSize;
            if(new Rectangle(pressedScreen.X-size.Width/2,pressedScreen.Y-size.Height/2,size.Width,size.Height).Contains(screen))return;
            if(tile.Tag is not IconRecord record)return;
            internalDrag=record;tile.SuppressClick=true;tile.Reordering=true;
            reorderOriginal=grid.Controls.Cast<Control>().Select(control=>TileKey((IconRecord)control.Tag!)).ToArray();
            DragPreview=true;tips.Hide(tile);LastDragState="reordering";tile.Invalidate();
        }
        PreviewReorder(screen);
    }
    private void PreviewReorder(Point screen)
    {
        UpdateOutsideReorderHint(screen);
        if(pressedTile==null||!grid.RectangleToScreen(grid.ClientRectangle).Contains(screen))return;
        // Hit-test the destination cells, so moving icons never change which
        // position the pointer is requesting halfway through an animation.
        int index=grid.OrderIndexAt(grid.PointToClient(screen));
        if(index>=0&&grid.Controls.GetChildIndex(pressedTile)!=index)grid.MoveIcon(pressedTile,index);
    }
    private void TrackReorder()
    {
        if(pressedTile==null)return;
        if(HandleReorderEscape((Native.GetAsyncKeyState((int)Keys.Escape)&0x8000)!=0))return;
        if(internalDrag==null)return;
        var point=Cursor.Position;var rect=grid.RectangleToScreen(grid.ClientRectangle);
        // Continue through the existing bottom fade without showing scrollbars.
        if(rect.Contains(point)&&grid.MaximumOffset>0)
        {
            int edge=Math.Max(12,CellPixels/2);
            if(point.Y>=rect.Bottom-edge)grid.ScrollTo(grid.ScrollOffset+Math.Max(4,CellPixels/4));
            else if(point.Y<rect.Top+edge)grid.ScrollTo(grid.ScrollOffset-Math.Max(4,CellPixels/4));
        }
        PreviewReorder(point);
    }
    private void RestoreReorderPreview()
    {
        grid.SuspendLayout();int index=0;
        foreach(string key in reorderOriginal)if(tiles.TryGetValue(key,out var tile))grid.Controls.SetChildIndex(tile,index++);
        grid.ResumeLayout(true);
    }
    private void FinishTilePress(IconTileButton tile,Point screen,bool cancelled)
    {
        if(pressedTile!=tile)return;
        var record=internalDrag==null?null:tile.Tag as IconRecord;bool inside=Bounds.Contains(screen);
        if(record!=null&&!cancelled&&inside)PreviewReorder(screen);
        int target=grid.Controls.Contains(tile)?grid.Controls.GetChildIndex(tile):-1;
        int original=record==null?-1:Array.IndexOf(reorderOriginal,TileKey(record));
        tips.Hide(tile);HasOutsideReorderHint=false;
        pressedTile=null;internalDrag=null;reorderFrames.Stop();DragPreview=false;
        tile.Reordering=false;tile.Cursor=Cursors.Default;tile.Capture=false;tile.Invalidate();
        if(record==null)return;
        // Preview is temporary. Commit through the same transactional save used by
        // the fixed tool library, or restore the preview before a cancellation.
        LastDragState=$"reorder;cancelled={cancelled};inside={inside};target={target}";
        grid.AnimateOrder(()=>
        {
            RestoreReorderPreview();
            if(cancelled)return;
            if(!inside){RemoveIcon?.Invoke(record);LastDragState+=";restored";}
            else if(target>=0&&target!=original)
            {
                if(Reordered!=null)Reordered(record,target);
                else grid.Controls.SetChildIndex(tile,target);
                if(grid.Controls.Contains(tile)&&grid.Controls.GetChildIndex(tile)==target){placementTile=tile;tile.Placed=true;tile.Invalidate();reorderFeedback.Start();}
            }
        });
    }
    private void ClearPlacementFeedback()
    {
        reorderFeedback.Stop();if(placementTile is {IsDisposed:false}){placementTile.Placed=false;placementTile.Invalidate();}placementTile=null;
    }
}
