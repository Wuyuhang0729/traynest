namespace NestedTray;
internal sealed class TrayPanel:Form
{
    private sealed class IconTileButton:Button
    {
        internal IconTileButton(){SetStyle(ControlStyles.Selectable,false);TabStop=false;}
        private bool doubleClick;
        private bool hovered,pressed;
        internal Action? DoubleAction;
        protected override void OnMouseEnter(EventArgs e){hovered=true;Invalidate();base.OnMouseEnter(e);}
        protected override void OnMouseLeave(EventArgs e){hovered=false;Invalidate();base.OnMouseLeave(e);}
        protected override void OnMouseDown(MouseEventArgs e){if(e.Button==MouseButtons.Left)pressed=true;Invalidate();base.OnMouseDown(e);}
        protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
        protected override void OnMouseCaptureChanged(EventArgs e){pressed=false;Invalidate();base.OnMouseCaptureChanged(e);}
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            if(hovered)
            {
                float diameter=Math.Max(2,Height/5f),right=Width-1,bottom=Height-1;
                using var shape=new System.Drawing.Drawing2D.GraphicsPath();
                shape.AddArc(0,0,diameter,diameter,180,90);shape.AddArc(right-diameter,0,diameter,diameter,270,90);
                shape.AddArc(right-diameter,bottom-diameter,diameter,diameter,0,90);shape.AddArc(0,bottom-diameter,diameter,diameter,90,90);shape.CloseFigure();
                e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using var fill=new SolidBrush(pressed?Color.FromArgb(46,49,56):Color.FromArgb(54,57,64));e.Graphics.FillPath(fill,shape);
            }
            if(Image!=null)e.Graphics.DrawImageUnscaled(Image,(Width-Image.Width)/2,(Height-Image.Height)/2);
        }
        protected override void WndProc(ref Message message)
        {
            if(message.Msg==0x203){doubleClick=true;DoubleAction?.Invoke();}
            base.WndProc(ref message);
            if(message.Msg==0x202)doubleClick=false;
        }
        protected override void OnClick(EventArgs e){if(!doubleClick)base.OnClick(e);}
    }
    private readonly IconGrid grid=new();
    private readonly OverflowFade fade;
    internal bool HasOverflowFade=>fade.Visible;
    internal void ScrollToLastIcon()=>grid.ScrollTo(int.MaxValue);
    private readonly Label empty=new(){Text="从系统托盘拖入图标\n拖到入口图标上也可收纳",AutoSize=false,TextAlign=ContentAlignment.MiddleCenter,ForeColor=Color.Silver};
    private readonly Dictionary<string,Button> tiles=new();
    private readonly ToolTip tips=new();
    internal Action<IconRecord,uint,Rectangle>? IconInput;
    internal Action<IconRecord>? RemoveIcon;
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
        Text="嵌套面板";ShowInTaskbar=false;TopMost=true;FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.Manual;AutoScaleMode=AutoScaleMode.None;
        BackColor=Color.FromArgb(32,34,40);ClientSize=new Size(208,168);KeyPreview=true;
        grid.Location=new Point(4,4);grid.Size=new Size(200,160);grid.BackColor=BackColor;
        empty.Bounds=grid.Bounds;Controls.AddRange([grid,empty]);
        fade=new OverflowFade(grid,()=>grid.ScrollTo(grid.ScrollOffset+CellPixels));Controls.Add(fade);fade.Visible=false;
        grid.Scrolled=UpdateOverflowFade;fade.MouseDown+=(_,_)=>PanelInteraction?.Invoke();
        empty.Paint+=(_,args)=>DrawIncomingIcon(args.Graphics);
        grid.Paint+=(_,args)=>DrawIncomingIcon(args.Graphics);
        MouseUp+=BackgroundMenu;grid.MouseUp+=BackgroundMenu;empty.MouseUp+=BackgroundMenu;
        foreach(Control surface in new Control[]{this,grid,empty})surface.MouseDown+=(_,_)=>PanelInteraction?.Invoke();
        KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Escape)Dismiss();};
        dismissFrames.Tick+=(_,_)=>DrawDismissFrame();
        CoreDrop=new CoreDragReceiver(this,()=>internalDrag!=null||CanAcceptNativeDrag?.Invoke()==true,point=>{if(internalDrag!=null)Reorder(internalDrag.Key,point);else NativeDropReceived?.Invoke(point);});
        if(!CoreDrop.Registered)foreach(Control target in new Control[]{this,grid,empty})RegisterDropTarget(target);
        int corners=2;Native.DwmSetWindowAttribute(Handle,33,ref corners,4);
    }
    internal void Render(IEnumerable<IconRecord> records)
    {
        grid.SuspendLayout();
        var selected=records.Where(value=>value.Selected).ToArray();
        foreach(string key in tiles.Keys.Except(selected.Select(value=>value.Key)).ToArray())
        {var old=tiles[key];grid.Controls.Remove(old);old.Image?.Dispose();old.Dispose();tiles.Remove(key);}
        foreach(var record in selected)
        {
            if(!tiles.TryGetValue(record.Key,out var tile))
            {
                var iconTile=new IconTileButton{Size=new Size(CellPixels,CellPixels),Margin=Padding.Empty,FlatStyle=FlatStyle.Flat,BackColor=BackColor,ForeColor=Color.White,Tag=record};tile=iconTile;tile.FlatAppearance.BorderSize=0;
                Point down=Point.Empty;bool dragging=false,pressedHere=false;
                tile.MouseDown+=(_,e)=>{PanelInteraction?.Invoke();if(e.Button==MouseButtons.Left){down=e.Location;dragging=false;pressedHere=true;LastDragState="down";}};
                tile.MouseMove+=(_,e)=>
                {
                    if(!pressedHere||e.Button!=MouseButtons.Left||dragging||Math.Abs(e.X-down.X)+Math.Abs(e.Y-down.Y)<SystemInformation.DragSize.Width)return;
                    dragging=true;bool cancelled=false;LastDragState="dragging";
                    QueryContinueDragEventHandler cancel=(_,args)=>{if(args.EscapePressed)cancelled=true;};tile.QueryContinueDrag+=cancel;
                    DragPreview=true;
                    internalDrag=record;
                    var data=new DataObject();data.SetData("NestedTray.IconKey",false,record.Key);
                    try {var result=tile.DoDragDrop(data,DragDropEffects.Move);LastDragState=$"result={result};cancelled={cancelled};inside={Bounds.Contains(Cursor.Position)}";if(!cancelled&&!Bounds.Contains(Cursor.Position)){RemoveIcon?.Invoke(record);LastDragState+=";restored";}}
                    finally {internalDrag=null;DragPreview=false;tile.QueryContinueDrag-=cancel;}
                };
                tile.Click+=(_,_)=>{if(!dragging)IconInput?.Invoke(record,0x400,CellBounds(record));};
                iconTile.DoubleAction=()=>IconInput?.Invoke(record,0x203,CellBounds(record));
                tile.MouseUp+=(_,e)=>{pressedHere=false;if(e.Button==MouseButtons.Right)IconInput?.Invoke(record,0x7B,CellBounds(record));};
                tiles.Add(record.Key,tile);grid.Controls.Add(tile);
                if(!CoreDrop.Registered)RegisterDropTarget(tile);
            }
            tile.AccessibleName=string.IsNullOrWhiteSpace(record.Tooltip)?Path.GetFileNameWithoutExtension(record.Executable):record.Tooltip;
            tips.SetToolTip(tile,tile.AccessibleName);
            tile.Size=new Size(CellPixels,CellPixels);
            var image=tile.Image;tile.Image=record.Image==null?null:new Bitmap(record.Image,new Size(IconPixels,IconPixels));image?.Dispose();
        }
        empty.Visible=selected.Length==0;grid.Visible=selected.Length>0;
        ApplyContentLayout();grid.ResumeLayout(true);
    }
    internal Rectangle CellBounds(IconRecord record)=>Visible&&!IsDismissing&&tiles.TryGetValue(record.Key,out var tile)?tile.RectangleToScreen(tile.ClientRectangle):EntryRectangle;
    internal void SetNativeDragFeedback(bool enabled)
    {
        if(NativeDragFeedback==enabled)return;NativeDragFeedback=enabled;
        BackColor=enabled?Color.FromArgb(32,58,78):Color.FromArgb(32,34,40);
        empty.Text=enabled?"松开鼠标以收纳图标":"从系统托盘拖入图标\n拖到入口图标上也可收纳";
        Invalidate();
    }
    internal void SetIncomingIcon(IconRecord? record)
    {
        bool changed=(incomingIcon==null)!=(record==null);
        incomingIcon=record;SetNativeDragFeedback(record!=null);
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
    {if(tiles.TryGetValue(key,out var tile)){var over=grid.GetChildAtPoint(grid.PointToClient(screen));if(over!=null)grid.Controls.SetChildIndex(tile,grid.Controls.GetChildIndex(over));}}
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
        var layout=ContentTrayLayout.Measure(count,CellPixels,surfacePadding,work.Size);
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
    {base.SetVisibleCore(value);if(!value&&(IsDismissing||IsOpening))CancelDismiss();}
    private void PositionPanel()
    {
        nint overflow=Native.FindWindowW("TopLevelWindowForOverflowXamlIsland",null);
        var work=Screen.FromRectangle(EntryRectangle).WorkingArea;
        var position=new Point(EntryRectangle.Right-Width,EntryRectangle.Top-Height-8);
        if(overflow!=0&&Native.GetWindowRect(overflow,out var nativeBounds))
        {
            var source=Native.Rect(nativeBounds);
            if(source.IntersectsWith(EntryRectangle)||source.IntersectsWith(new Rectangle(position,Size)))
            {
                if(source.Left-Width-8>=work.Left)position=new Point(source.Left-Width-8,source.Bottom-Height);
                else if(source.Right+Width+8<=work.Right)position=new Point(source.Right+8,source.Bottom-Height);
                else position.Y=source.Top-Height-8;
            }
        }
        Location=new Point(Math.Clamp(position.X,work.Left,Math.Max(work.Left,work.Right-Width)),Math.Clamp(position.Y,work.Top,Math.Max(work.Top,work.Bottom-Height)));
    }
    private void BackgroundMenu(object? sender,MouseEventArgs e){if(e.Button==MouseButtons.Right)OpenMenu?.Invoke();}
    protected override void Dispose(bool disposing)
    {if(disposing){CancelDismiss();dismissFrames.Dispose();CoreDrop.Dispose();tips.Dispose();foreach(var tile in tiles.Values)tile.Image?.Dispose();}base.Dispose(disposing);}
}
