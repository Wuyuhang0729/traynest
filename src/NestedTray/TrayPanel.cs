namespace NestedTray;
internal sealed partial class TrayPanel:Form
{
    private sealed class IconTileButton:Button
    {
        internal IconTileButton(){SetStyle(ControlStyles.Selectable,false);TabStop=false;}
        private bool doubleClick;
        private bool hovered,pressed;
        internal Action? DoubleAction;
        internal bool Dimmed;
        internal string StatusMark="";
        internal bool SuppressClick,Reordering,Placed;
        internal Action<MouseEventArgs>? PointerReleased;
        protected override void OnMouseEnter(EventArgs e){hovered=true;Invalidate();base.OnMouseEnter(e);}
        protected override void OnMouseLeave(EventArgs e){hovered=false;Invalidate();base.OnMouseLeave(e);}
        protected override void OnMouseDown(MouseEventArgs e){if(e.Button==MouseButtons.Left)pressed=true;Invalidate();base.OnMouseDown(e);}
        protected override void OnMouseUp(MouseEventArgs e){PointerReleased?.Invoke(e);pressed=false;Invalidate();base.OnMouseUp(e);}
        protected override void OnMouseCaptureChanged(EventArgs e){pressed=false;Invalidate();base.OnMouseCaptureChanged(e);}
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            if(hovered||Reordering||Placed)
            {
                float diameter=Math.Max(2,Height/5f),right=Width-1,bottom=Height-1;
                using var shape=new System.Drawing.Drawing2D.GraphicsPath();
                shape.AddArc(0,0,diameter,diameter,180,90);shape.AddArc(right-diameter,0,diameter,diameter,270,90);
                shape.AddArc(right-diameter,bottom-diameter,diameter,diameter,0,90);shape.AddArc(0,bottom-diameter,diameter,diameter,90,90);shape.CloseFigure();
                e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using var fill=new SolidBrush(Reordering?Color.FromArgb(43,66,86):Placed?Color.FromArgb(36,60,50):pressed?Color.FromArgb(46,49,56):Color.FromArgb(54,57,64));e.Graphics.FillPath(fill,shape);
                if(Reordering||Placed){using var border=new Pen(Reordering?Color.FromArgb(112,190,255):Color.FromArgb(105,200,150));e.Graphics.DrawPath(border,shape);}
            }
            if(Image!=null)
            {
                var target=new Rectangle((Width-Image.Width)/2,(Height-Image.Height)/2,Image.Width,Image.Height);
                if(Dimmed||Reordering){using var attributes=new System.Drawing.Imaging.ImageAttributes();var matrix=new System.Drawing.Imaging.ColorMatrix{Matrix33=.4f};attributes.SetColorMatrix(matrix);e.Graphics.DrawImage(Image,target,0,0,Image.Width,Image.Height,GraphicsUnit.Pixel,attributes);}
                else e.Graphics.DrawImageUnscaled(Image,target.Location);
            }
            if(StatusMark.Length>0)
            {
                var badge=new Rectangle(Width-15,Height-15,14,14);
                using var brush=new SolidBrush(Color.FromArgb(32,34,40));e.Graphics.FillEllipse(brush,badge);
                TextRenderer.DrawText(e.Graphics,StatusMark,Font,badge,Color.White,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding);
            }
        }
        protected override void WndProc(ref Message message)
        {
            if(message.Msg==0x203){doubleClick=true;DoubleAction?.Invoke();}
            base.WndProc(ref message);
            if(message.Msg==0x202)doubleClick=false;
        }
        protected override void OnClick(EventArgs e){if(!doubleClick&&!SuppressClick)base.OnClick(e);}
    }
    private readonly IconGrid grid=new();
    private readonly OverflowFade fade;
    internal bool HasOverflowFade=>fade.Visible;
    internal void ScrollToLastIcon()=>grid.ScrollTo(int.MaxValue);
    private const string EmptyInstructions="展开系统托盘的隐藏图标区\n将图标拖到这里收纳";
    private readonly Label empty=new(){Text=EmptyInstructions,AutoSize=false,TextAlign=ContentAlignment.MiddleCenter,ForeColor=Color.Silver};
    private readonly Dictionary<string,Button> tiles=new();
    private readonly ToolTip tips=new();
    internal Action<IconRecord,uint,Rectangle>? IconInput;
    internal Action<IconRecord>? RemoveIcon;
    internal Action<IconRecord,uint>? OfflineInput;
    internal Func<string,ToolLaunchStatus>? ToolStatus;
    internal Action<IconRecord,int>? Reordered;
    internal bool UseSavedOrder;
    internal int FixedColumns;
    internal PopupPlacement Placement=PopupPlacement.Left;
    internal void SetPlacement(PopupPlacement value)
    {
        if(Placement==value)return;Placement=value;
        if(Visible&&!IsDismissing){CancelDismiss();PositionPanel();}
    }
    private string TileKey(IconRecord record)=>UseSavedOrder&&!string.IsNullOrEmpty(record.Rule)?record.Rule:record.Key;
    internal Action? OpenMenu;
    internal Action? PanelInteraction;
    internal Func<bool>? CanAcceptNativeDrag;
    internal Action<Point>? NativeDropReceived;
    internal int ExternalDragEnters,ExternalDragOvers,ExternalDrops;
    internal string[] LastExternalFormats=[];
    internal DragDropEffects LastExternalAllowed,LastExternalEffect;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal Rectangle EntryRectangle{get;set;}
    internal bool DragPreview;
    internal string LastDragState="";
    internal bool NativeDragFeedback;
    private IconRecord? incomingIcon;
    private IconRecord? internalDrag;
    internal bool HasIncomingIconPreview=>incomingIcon?.Image!=null&&NativeDragFeedback;
    internal int IconPixels=16,CellPixels=40;
    private int surfacePadding=4;
    internal readonly CoreDragReceiver CoreDrop;
    internal DismissOptions DismissSettings=new();
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal bool IsDismissing{get;private set;}
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal bool IsOpening{get;private set;}
    internal Action? DismissStarted;
    private readonly System.Windows.Forms.Timer dismissFrames=new(){Interval=15};
    private readonly System.Diagnostics.Stopwatch dismissClock=new();
    private Point dismissHome;
    private DismissOptions? activeDismiss;
    private double animationStartOffset,animationEndOffset,animationStartOpacity=1;
    protected override bool ShowWithoutActivation=>true;
    protected override CreateParams CreateParams
    {get {var value=base.CreateParams;value.ExStyle|=0x08000000;return value;}}
    protected override void WndProc(ref Message message)
    {
        if(message.Msg==0x21){message.Result=3;return;} // MA_NOACTIVATE: preserve the native tray's focus.
        base.WndProc(ref message);
    }
    internal TrayPanel()
    {
        Icon=AppIcon.Load();Text="嵌套面板";ShowInTaskbar=false;TopMost=true;FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.Manual;AutoScaleMode=AutoScaleMode.None;
        BackColor=Color.FromArgb(32,34,40);ClientSize=new Size(208,168);KeyPreview=true;
        grid.Location=new Point(4,4);grid.Size=new Size(200,160);grid.BackColor=BackColor;
        empty.Bounds=grid.Bounds;Controls.AddRange([grid,empty]);
        fade=new OverflowFade(grid,()=>grid.ScrollTo(grid.ScrollOffset+CellPixels));Controls.Add(fade);fade.Visible=false;
        grid.Scrolled=UpdateOverflowFade;fade.MouseDown+=(_,_)=>PanelInteraction?.Invoke();
        empty.Paint+=(_,args)=>DrawIncomingIcon(args.Graphics);
        grid.Paint+=(_,args)=>DrawIncomingIcon(args.Graphics);
        MouseUp+=BackgroundMenu;grid.MouseUp+=BackgroundMenu;empty.MouseUp+=BackgroundMenu;
        foreach(Control surface in new Control[]{this,grid,empty})surface.MouseDown+=(_,_)=>PanelInteraction?.Invoke();
        KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Escape&&!HandleReorderEscape(true))Dismiss();};
        KeyUp+=(_,e)=>{if(e.KeyCode==Keys.Escape)HandleReorderEscape(false);};
        dismissFrames.Tick+=(_,_)=>DrawDismissFrame();
        reorderFrames.Tick+=(_,_)=>TrackReorder();
        reorderFeedback.Tick+=(_,_)=>ClearPlacementFeedback();
        CoreDrop=new CoreDragReceiver(this,()=>internalDrag!=null||CanAcceptNativeDrag?.Invoke()==true,point=>{if(internalDrag!=null)Reorder(TileKey(internalDrag),point);else NativeDropReceived?.Invoke(point);});
        if(!CoreDrop.Registered)foreach(Control target in new Control[]{this,grid,empty})RegisterDropTarget(target);
        int corners=2;Native.DwmSetWindowAttribute(Handle,33,ref corners,4);
    }
    internal void Render(IEnumerable<IconRecord> records)
    {
        grid.SuspendLayout();
        var selected=records.Where(value=>value.Selected).ToArray();
        if(pressedTile?.Tag is IconRecord pressed&&!selected.Any(record=>TileKey(record)==TileKey(pressed)))FinishTilePress(pressedTile,Point.Empty,true);
        foreach(string key in tiles.Keys.Except(selected.Select(TileKey)).ToArray())
        {var old=tiles[key];grid.Controls.Remove(old);old.Image?.Dispose();old.Dispose();tiles.Remove(key);}
        foreach(var record in selected)
        {
            string key=TileKey(record);
            if(!tiles.TryGetValue(key,out var tile))
            {
                var iconTile=new IconTileButton{Size=new Size(CellPixels,CellPixels),Margin=Padding.Empty,FlatStyle=FlatStyle.Flat,BackColor=BackColor,ForeColor=Color.White,Tag=record};tile=iconTile;tile.FlatAppearance.BorderSize=0;
                tile.MouseDown+=(_,e)=>{PanelInteraction?.Invoke();if(e.Button==MouseButtons.Left)BeginTilePress(iconTile,tile.PointToScreen(e.Location));};
                tile.MouseMove+=(_,e)=>{if(e.Button==MouseButtons.Left)MoveTilePress(iconTile,tile.PointToScreen(e.Location));};
                iconTile.PointerReleased=e=>{if(e.Button==MouseButtons.Left)FinishTilePress(iconTile,tile.PointToScreen(e.Location),false);};
                tile.MouseCaptureChanged+=(_,_)=>{if(pressedTile==iconTile&&!iconTile.Capture)FinishTilePress(iconTile,Point.Empty,true);};
                void Input(uint code){if(tile.Tag is IconRecord current){if(current.IsOffline)OfflineInput?.Invoke(current,code);else IconInput?.Invoke(current,code,CellBounds(current));}}
                tile.Click+=(_,_)=>Input(0x400);
                iconTile.DoubleAction=()=>Input(0x203);
                tile.MouseUp+=(_,e)=>{if(e.Button==MouseButtons.Right)Input(0x7B);};
                tiles.Add(key,tile);grid.Controls.Add(tile);
                if(!CoreDrop.Registered)RegisterDropTarget(tile);
            }
            tile.Tag=record;((IconTileButton)tile).Dimmed=record.IsOffline;
            tile.AccessibleName=string.IsNullOrWhiteSpace(record.Tooltip)?Path.GetFileNameWithoutExtension(record.Executable):record.Tooltip;
            UpdateToolState((IconTileButton)tile,record);
            tile.Size=new Size(CellPixels,CellPixels);
            var image=tile.Image;tile.Image=record.Image==null?null:new Bitmap(record.Image,new Size(IconPixels,IconPixels));image?.Dispose();
        }
        if(UseSavedOrder&&internalDrag==null)for(int index=0;index<selected.Length;index++){var tile=tiles[TileKey(selected[index])];if(grid.Controls.GetChildIndex(tile)!=index)grid.Controls.SetChildIndex(tile,index);}
        empty.Visible=selected.Length==0;grid.Visible=selected.Length>0;
        ApplyContentLayout();grid.ResumeLayout(true);
    }
    internal Rectangle CellBounds(IconRecord record)=>Visible&&!IsDismissing&&tiles.TryGetValue(TileKey(record),out var tile)?tile.RectangleToScreen(tile.ClientRectangle):EntryRectangle;
    internal void ShowToolStatus(IconRecord record,string message)
    {if(tiles.TryGetValue(TileKey(record),out var tile)&&Visible)tips.Show(message,tile,new Point(0,tile.Height),5000);}
    private void UpdateToolState(IconTileButton tile,IconRecord record)
    {
        var status=record.IsOffline?ToolStatus?.Invoke(record.Rule)??new(ToolLaunchState.Idle,"未运行或图标暂不可用；单击尝试启动，右键管理。"):new(ToolLaunchState.Idle,"");
        string mark=status.State is ToolLaunchState.Waiting or ToolLaunchState.RunningWithoutIcon?"…":status.State==ToolLaunchState.Idle?"":"!";
        if(tile.StatusMark!=mark){tile.StatusMark=mark;tile.Invalidate();}
        tile.AccessibleDescription=status.Message;
        string hint=record.IsOffline?$"{tile.AccessibleName}\n{status.Message}":tile.AccessibleName??"";
        if(tips.GetToolTip(tile)!=hint)tips.SetToolTip(tile,hint);
    }
    internal void RefreshToolStates()
    {foreach(var tile in tiles.Values.OfType<IconTileButton>())if(tile.Tag is IconRecord record)UpdateToolState(tile,record);}
    internal void SetNativeDragFeedback(bool enabled)
    {
        if(NativeDragFeedback==enabled)return;NativeDragFeedback=enabled;
        BackColor=enabled?Color.FromArgb(32,58,78):Color.FromArgb(32,34,40);
        empty.Text=enabled?"松开鼠标以收纳图标":EmptyInstructions;
        Invalidate();
    }
    internal void SetIncomingIcon(IconRecord? record)
    {
        bool changed=(incomingIcon==null)!=(record==null);
        incomingIcon=record is {CanCollect:true,Selected:false}?record:null;SetNativeDragFeedback(incomingIcon!=null);
        if(changed)ApplyContentLayout();
        empty.Invalidate();grid.Invalidate();
    }
    private void DrawIncomingIcon(Graphics graphics)
    {
        if(incomingIcon?.Image==null||!NativeDragFeedback)return;
        int columns=Math.Max(1,grid.ClientSize.Width/CellPixels),index=tiles.Count;
        var bounds=new Rectangle(index%columns*CellPixels,index/columns*CellPixels-grid.ScrollOffset,CellPixels-1,CellPixels-1);
        using var background=new SolidBrush(Color.FromArgb(32,58,78));graphics.FillRectangle(background,bounds);
        using var border=new Pen(Color.FromArgb(112,190,255)){DashStyle=System.Drawing.Drawing2D.DashStyle.Dash};graphics.DrawRectangle(border,bounds);
        graphics.DrawImage(incomingIcon.Image,bounds.Left+(CellPixels-IconPixels)/2,bounds.Top+(CellPixels-IconPixels)/2,IconPixels,IconPixels);
    }
    private void RegisterDropTarget(Control target)
    {
        target.AllowDrop=true;
        target.DragEnter+=(_,e)=>UpdateDropEffect(e,true);
        target.DragOver+=(_,e)=>UpdateDropEffect(e,false);
        target.DragDrop+=(_,e)=>
        {
            UpdateDropEffect(e,false);
            if(e.Effect==DragDropEffects.None)return;
            if(e.Data?.GetDataPresent("NestedTray.IconKey")==true)
            {
                if(e.Data.GetData("NestedTray.IconKey")is string key)Reorder(key,new Point(e.X,e.Y));
            }
            else {ExternalDrops++;NativeDropReceived?.Invoke(new Point(e.X,e.Y));}
        };
    }
    private void Reorder(string key,Point screen)
    {if(tiles.TryGetValue(key,out var tile)){var over=grid.GetChildAtPoint(grid.PointToClient(screen));if(over!=null){int index=grid.Controls.GetChildIndex(over);if(Reordered!=null&&tile.Tag is IconRecord record)Reordered(record,index);else grid.Controls.SetChildIndex(tile,index);}}}
    internal bool IsDropSurface(Point point)=>Visible&&!IsDismissing&&Bounds.Contains(point)&&Native.GetAncestor(Native.WindowFromPoint(new Native.Point{X=point.X,Y=point.Y}),2)==Handle;
    private void UpdateDropEffect(DragEventArgs e,bool entering)
    {
        bool internalDrag=e.Data?.GetDataPresent("NestedTray.IconKey")==true;
        if(internalDrag)e.Effect=(e.AllowedEffect&DragDropEffects.Move)!=0?DragDropEffects.Move:DragDropEffects.None;
        else
        {
            if(entering){ExternalDragEnters++;try{LastExternalFormats=e.Data?.GetFormats(false)??[];}catch{LastExternalFormats=[];}}
            else ExternalDragOvers++;
            bool ready=CanAcceptNativeDrag?.Invoke()==true;
            // Prefer Copy so Explorer retains the original icon model; collection hides it
            // only after the OLE transaction has finished.
            e.Effect=!ready?DragDropEffects.None:(e.AllowedEffect&DragDropEffects.Copy)!=0?DragDropEffects.Copy:(e.AllowedEffect&DragDropEffects.Move)!=0?DragDropEffects.Move:DragDropEffects.None;
            LastExternalAllowed=e.AllowedEffect;LastExternalEffect=e.Effect;
        }
    }
    internal void Open(bool preview=false)
    {
        RefreshToolStates();
        bool animate=!Visible||IsDismissing;
        Point? interrupted=IsDismissing?Location:null;
        CancelDismiss();
        DragPreview=preview;
        nint overflow=Native.FindWindowW("TopLevelWindowForOverflowXamlIsland",null);
        uint dpi=overflow!=0?Native.GetDpiForWindow(overflow):(uint)DeviceDpi;
        var layout=NativeTrayLayout.Measure(overflow,dpi,!preview);
        IconPixels=layout.Icon;CellPixels=layout.Cell;surfacePadding=layout.Padding;ApplyContentLayout();
        foreach(var tile in tiles.Values){tile.Size=new Size(CellPixels,CellPixels);if(tile.Tag is IconRecord record){var previous=tile.Image;tile.Image=record.Image==null?null:new Bitmap(record.Image,new Size(IconPixels,IconPixels));previous?.Dispose();}}
        PositionPanel();
        var options=DismissSettings.Copy();options.Validate();
        if(animate&&options.Enabled&&options.DurationMs>0&&SystemAnimation.Enabled)
        {
            dismissHome=Location;activeDismiss=options;IsOpening=true;
            animationStartOffset=interrupted.HasValue&&interrupted.Value.X==dismissHome.X?Math.Clamp(interrupted.Value.Y-dismissHome.Y,0,options.DistanceDip*DeviceDpi/96.0):options.DistanceDip*DeviceDpi/96.0;
            animationEndOffset=0;animationStartOpacity=options.Fade ? .01 : 1;
            Location=new Point(dismissHome.X,dismissHome.Y+(int)Math.Round(animationStartOffset));Opacity=animationStartOpacity;
            dismissClock.Restart();dismissFrames.Start();
        }
        Show();EnsureAboveApplications();
    }
    private void ApplyContentLayout()
    {
        var work=Screen.FromRectangle(EntryRectangle).WorkingArea;
        int count=tiles.Count+(incomingIcon!=null&&!tiles.ContainsKey(incomingIcon.Key)?1:0);
        var layout=ContentTrayLayout.Measure(count,CellPixels,surfacePadding,work.Size,FixedColumns);
        bool resized=ClientSize!=layout.Surface;
        grid.SuspendLayout();
        ClientSize=layout.Surface;
        grid.Bounds=new Rectangle(surfacePadding,surfacePadding,Width-surfacePadding*2,Height-surfacePadding*2);empty.Bounds=grid.Bounds;
        grid.ConfigureContent(layout.ContentRows*CellPixels);
        grid.ResumeLayout(true);
        if(layout.Scroll&&incomingIcon!=null)grid.ScrollTo(int.MaxValue);
        UpdateOverflowFade();
        if(resized&&Visible&&!IsDismissing)
        {
            int offset=IsOpening?Location.Y-dismissHome.Y:0;
            PositionPanel();
            if(IsOpening){dismissHome=Location;Location=new Point(dismissHome.X,dismissHome.Y+offset);}
        }
    }
    private void UpdateOverflowFade()
    {
        int height=Math.Max(1,grid.Height/4);
        fade.Bounds=new Rectangle(grid.Left,grid.Bottom-height,grid.Width,height);
        fade.Visible=tiles.Count>0&&grid.ScrollOffset<grid.MaximumOffset;
        if(fade.Visible){fade.BringToFront();fade.Invalidate();}
    }
    internal void EnsureAboveApplications()
    {if(Visible&&!IsDismissing)Native.SetWindowPos(Handle,new nint(-1),0,0,0,0,0x213);}
    internal void Dismiss()
    {
        if(!Visible||IsDismissing)return;
        DismissStarted?.Invoke();
        var options=DismissSettings.Copy();options.Validate();
        if(!options.Enabled||options.DurationMs==0||!SystemAnimation.Enabled){Hide();return;}
        Point current=Location,home=IsOpening?dismissHome:Location;double opacity=Opacity;
        CancelDismiss();dismissHome=home;Location=current;Opacity=opacity;
        activeDismiss=options;IsDismissing=true;Enabled=false;
        animationStartOffset=current.Y-home.Y;animationEndOffset=options.DistanceDip*DeviceDpi/96.0;animationStartOpacity=opacity;
        dismissClock.Restart();dismissFrames.Start();
    }
    private void DrawDismissFrame()
    {
        if((!IsDismissing&&!IsOpening)||activeDismiss==null){dismissFrames.Stop();return;}
        double t=Math.Clamp(dismissClock.Elapsed.TotalMilliseconds/activeDismiss.DurationMs,0,1);
        double p=IsOpening?1-activeDismiss.Progress(1-t):activeDismiss.Progress(t);
        if(t>=1){if(IsDismissing)Hide();else CancelDismiss();return;}
        Location=new Point(dismissHome.X,dismissHome.Y+(int)Math.Round(animationStartOffset+(animationEndOffset-animationStartOffset)*p));
        if(activeDismiss.Fade)Opacity=Math.Clamp(animationStartOpacity+((IsOpening?1:0)-animationStartOpacity)*p,.01,1);
    }
    private void CancelDismiss()
    {
        dismissFrames.Stop();if(IsDismissing||IsOpening)Location=dismissHome;
        IsDismissing=false;IsOpening=false;activeDismiss=null;Opacity=1;Enabled=true;
    }
    protected override void SetVisibleCore(bool value)
    {if(!value&&pressedTile!=null)FinishTilePress(pressedTile,Point.Empty,true);base.SetVisibleCore(value);if(!value&&(IsDismissing||IsOpening))CancelDismiss();}
    private void PositionPanel()
    {
        nint overflow=Native.FindWindowW("TopLevelWindowForOverflowXamlIsland",null);
        var work=Screen.FromRectangle(EntryRectangle).WorkingArea;
        var anchor=EntryRectangle;
        if(overflow!=0&&Native.IsWindowVisible(overflow)&&Native.GetWindowRect(overflow,out var nativeBounds))
        {
            var source=Native.Rect(nativeBounds);
            if(source.IntersectsWith(EntryRectangle))anchor=source;
        }
        Location=PopupPosition.Calculate(anchor,Size,work,Placement);
    }
    private void BackgroundMenu(object? sender,MouseEventArgs e){if(e.Button==MouseButtons.Right)OpenMenu?.Invoke();}
    protected override void Dispose(bool disposing)
    {if(disposing){if(pressedTile!=null)FinishTilePress(pressedTile,Point.Empty,true);reorderFrames.Dispose();reorderFeedback.Dispose();CancelDismiss();dismissFrames.Dispose();CoreDrop.Dispose();tips.Dispose();foreach(var tile in tiles.Values)tile.Image?.Dispose();}base.Dispose(disposing);}
}
