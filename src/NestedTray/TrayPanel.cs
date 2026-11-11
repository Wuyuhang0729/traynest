namespace NestedTray;
internal sealed partial class TrayPanel:Form
{
    private sealed class IconTileButton:Button
    {
        internal IconTileButton(){SetStyle(ControlStyles.Selectable,false);DoubleBuffered=true;TabStop=false;}
        private bool doubleClick;
        private bool hovered,pressed;
        internal Action? DoubleAction;
        internal bool Dimmed;
        internal bool LightSurface,GlassSurface;
        internal string StatusMark="";
        internal bool SuppressClick,Reordering,Placed;
        internal Action<MouseEventArgs>? PointerReleased;
        protected override void OnMouseEnter(EventArgs e){hovered=true;Invalidate();base.OnMouseEnter(e);}
        protected override void OnMouseLeave(EventArgs e){hovered=false;Invalidate();base.OnMouseLeave(e);}
        protected override void OnMouseDown(MouseEventArgs e){if(e.Button==MouseButtons.Left)pressed=true;Invalidate();base.OnMouseDown(e);}
        protected override void OnMouseUp(MouseEventArgs e){PointerReleased?.Invoke(e);pressed=false;Invalidate();base.OnMouseUp(e);}
        protected override void OnMouseCaptureChanged(EventArgs e){pressed=false;Invalidate();base.OnMouseCaptureChanged(e);}
        protected override void OnPaint(PaintEventArgs e)
        {PaintTile(e.Graphics,Point.Empty,true);}
        internal void PaintIntoGrid(Graphics graphics)=>PaintTile(graphics,Location,false);
        private void PaintTile(Graphics graphics,Point offset,bool clearGlass)
        {
            var bounds=new Rectangle(offset,Size);
            if(GlassSurface){if(clearGlass)PanelTheme.PaintGlass(graphics,bounds);}
            else {using var background=new SolidBrush(BackColor);graphics.FillRectangle(background,bounds);}
            if(hovered||Reordering||Placed)
                IconFeedback.Draw(graphics,bounds,LightSurface||GlassSurface,pressed||Reordering);
            if(Image!=null)
            {
                var target=new Rectangle(offset.X+(Width-Image.Width)/2,offset.Y+(Height-Image.Height)/2,Image.Width,Image.Height);
                if(Dimmed){using var attributes=new System.Drawing.Imaging.ImageAttributes();var matrix=new System.Drawing.Imaging.ColorMatrix{Matrix33=.4f};attributes.SetColorMatrix(matrix);graphics.DrawImage(Image,target,0,0,Image.Width,Image.Height,GraphicsUnit.Pixel,attributes);}
                else graphics.DrawImageUnscaled(Image,target.Location);
            }
            if(StatusMark.Length>0)
            {
                var badge=new Rectangle(bounds.Right-15,bounds.Bottom-15,14,14);
                using var brush=new SolidBrush(LightSurface?Color.White:Color.FromArgb(32,34,40));graphics.FillEllipse(brush,badge);
                TextRenderer.DrawText(graphics,StatusMark,Font,badge,ForeColor,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding);
            }
        }
        [System.Runtime.InteropServices.DllImport("user32.dll")]private static extern bool ValidateRect(nint window,nint rect);
        protected override void OnInvalidated(InvalidateEventArgs e)
        {
            base.OnInvalidated(e);
            if(Parent is IconGrid surface&&!surface.PaintingFrame)surface.Invalidate(Bounds);
        }
        protected override void WndProc(ref Message message)
        {
            // Keep native controls for input/accessibility, but paint every
            // tile in the grid's single buffered pass, preserving glass alpha.
            if(Parent is IconGrid surface)
            {
                if(surface.PrintingTree&&message.Msg is 0x317 or 0x318){message.Result=0;return;}
                if(message.Msg==0xF){ValidateRect(Handle,0);message.Result=0;return;}
                if(message.Msg==0x14){message.Result=1;return;}
            }
            // Native drag layering must not rewrite WinForms' logical Controls order.
            // Bounds are unchanged in this guarded SetWindowPos call.
            if(message.Msg==0x47&&Parent is IconGrid {ChangingDragZOrder:true}){DefWndProc(ref message);return;}
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
    private readonly SurfaceLabel empty=new(){Text=EmptyInstructions,AutoSize=false,TextAlign=ContentAlignment.MiddleCenter,ForeColor=Color.Silver,UseCompatibleTextRendering=true};
    private readonly Dictionary<string,Button> tiles=new();
    private readonly ToolTip tips=new();
    internal Action<IconRecord,uint,Rectangle>? IconInput;
    internal Action<IconRecord>? RemoveIcon;
    internal Action<IconRecord,uint>? OfflineInput;
    internal Func<string,ToolLaunchStatus>? ToolStatus;
    internal Action<IconRecord,int>? Reordered;
    internal bool UseSavedOrder;
    internal int FixedColumns;
    internal bool IndependentEntry;
    internal PopupPlacement Placement=PopupPlacement.Left;
    internal nint TaskbarWindow;
    private nint layerTaskbar;
    private long realignUntil,nextRealign;
    private Rectangle lastNativeHost;
    internal NativePanelGeometry? NativeGeometry;
    private NativePanelSnapshot? openingGeometry;
    private bool geometryDirty;
    internal void RequestRealignment(){realignUntil=Environment.TickCount64+1000;geometryDirty=true;NativeGeometry?.Refresh();}
    internal void FollowNativeBounds()
    {
        if(IndependentEntry||!Visible||IsAnimating||IsPreparing||!CanQueryExplorerAutomation||IsReordering||HasReorderPreview)return;
        nint overflow=NativeOverflow.Window;if(!NativeOverflow.Visible||!Native.GetWindowRect(overflow,out var raw))return;
        var host=Native.Rect(raw);long now=Environment.TickCount64;
        if(host!=lastNativeHost){lastNativeHost=host;RequestRealignment();}
        if(NativeGeometry!=null)
        {
            if(NativeGeometry.Latest is not {Measured:true} snapshot||snapshot.Host!=host||
                (!geometryDirty&&openingGeometry==snapshot))return;
            openingGeometry=snapshot;geometryDirty=false;
            var destination=CalculatePosition();
            if(destination!=Location)StartRelocation(destination);
            return;
        }
        if(now>realignUntil||now<nextRealign)return;
        nextRealign=now+250;PositionPanel();
    }
    internal void SetPlacement(PopupPlacement value)
    {
        if(Placement==value)return;Placement=value;
        if(Visible&&!IsDismissing){CancelDismiss(false);StartRelocation(CalculatePosition());}
    }
    private string TileKey(IconRecord record)=>UseSavedOrder&&!string.IsNullOrEmpty(record.Rule)?record.Rule:record.Key;
    internal Action? OpenMenu;
    internal Action? PanelInteraction;
    internal Func<bool>? CanAcceptNativeDrag;
    internal Func<bool>? NativeDragActive;
    private bool CanQueryExplorerAutomation=>!DragPreview&&!(NativeDragActive?.Invoke()??false)&&(Native.GetAsyncKeyState(1)&0x8000)==0;
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
    private Bitmap? incomingImage;
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
    private CompositionFrameClock? compositionFrames;
    private readonly System.Diagnostics.Stopwatch dismissClock=new();
    private Point dismissHome;
    private DismissOptions? activeDismiss;
    private double animationStartOffset,animationEndOffset,animationStartOpacity=1;
    private int activeDuration;
    private Point relocationStart;
    private bool isRelocating;
    private int openRequest;
    internal bool IsPreparing{get;private set;}
    internal bool IsAnimating=>IsOpening||IsDismissing||isRelocating;
    internal bool WantsVisible=>IsPreparing||IsOpening||Visible&&!IsDismissing;
    internal bool HasMeasuredAnchor=>openingGeometry?.Measured==true;
    private bool computingOpenLayout;
    private bool forceNativeAnchor;
    private bool simultaneousAbove;
    internal Rectangle NativeEntryRectangle;
    internal int MotionOriginEdge=>revealEdge;
    internal object PresentationGeometry=>new{Host=openingGeometry?.Host.ToString(),Surface=openingGeometry?.Surface.ToString(),Measured=openingGeometry?.Measured,ForcedNativeAnchor=forceNativeAnchor};
    internal event Action? MotionCompleted;
    internal event Action<Point,long>? MotionFrame;
    protected override bool ShowWithoutActivation=>true;
    protected override CreateParams CreateParams
    {get {var value=base.CreateParams;value.ExStyle|=0x08000000;return value;}}
    protected override void WndProc(ref Message message)
    {
        if(message.Msg==0x21){message.Result=3;return;} // MA_NOACTIVATE: preserve the native tray's focus.
        if(message.Msg==CompositionFrameClock.Message){compositionFrames?.Consume();DrawDismissFrame();message.Result=0;return;}
        base.WndProc(ref message);
        if(message.Msg is 0x1A or 0x31A or 0x320)ApplyAppearance(true);
    }
    internal TrayPanel()
    {
        Icon=AppIcon.Load();Text="嵌套面板";ShowInTaskbar=false;TopMost=true;FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.Manual;AutoScaleMode=AutoScaleMode.None;
        BackColor=Color.FromArgb(32,34,40);ClientSize=new Size(208,168);KeyPreview=true;
        grid.Location=new Point(4,4);grid.Size=new Size(200,160);grid.BackColor=BackColor;
        empty.Bounds=grid.Bounds;Controls.AddRange([grid,empty]);
        fade=new OverflowFade(grid,()=>grid.ScrollTo(grid.ScrollOffset+CellPixels));Controls.Add(fade);fade.Visible=false;
        grid.Scrolled=UpdateOverflowFade;fade.MouseDown+=(_,_)=>PanelInteraction?.Invoke();
        empty.Overlay=DrawIncomingIcon;
        grid.Paint+=(_,args)=>
        {
            foreach(var tile in grid.BackToFront().OfType<IconTileButton>())tile.PaintIntoGrid(args.Graphics);
            DrawIncomingIcon(args.Graphics);
        };
        MouseUp+=BackgroundMenu;grid.MouseUp+=BackgroundMenu;empty.MouseUp+=BackgroundMenu;
        foreach(Control surface in new Control[]{this,grid,empty})surface.MouseDown+=(_,_)=>PanelInteraction?.Invoke();
        KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Escape&&!HandleReorderEscape(true))Dismiss();};
        KeyUp+=(_,e)=>{if(e.KeyCode==Keys.Escape)HandleReorderEscape(false);};
        reorderFrames.Tick+=(_,_)=>TrackReorder();
        reorderFeedback.Tick+=(_,_)=>ClearPlacementFeedback();
        CoreDrop=new CoreDragReceiver(this,()=>internalDrag!=null||CanAcceptNativeDrag?.Invoke()==true,point=>{if(internalDrag!=null)Reorder(TileKey(internalDrag),point);else NativeDropReceived?.Invoke(point);});
        if(!CoreDrop.Registered)foreach(Control target in new Control[]{this,grid,empty})RegisterDropTarget(target);
        int corners=2;Native.DwmSetWindowAttribute(Handle,33,ref corners,4);
        ApplyAppearance();
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
                var iconTile=new IconTileButton{Size=new Size(CellPixels,CellPixels),Margin=Padding.Empty,FlatStyle=FlatStyle.Flat,BackColor=BackColor,ForeColor=ForeColor,LightSurface=LightSurface,GlassSurface=AcrylicActive,Tag=record};tile=iconTile;tile.FlatAppearance.BorderSize=0;
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
        UpdateAppearanceColors();
        empty.Text=enabled?(incomingIcon?.Image!=null?"":"松开鼠标以收纳图标"):EmptyInstructions;
        Invalidate();
    }
    internal void SetIncomingIcon(IconRecord? record)
    {
        var next=record is {CanCollect:true,Selected:false}?record:null;
        bool changed=(incomingIcon==null)!=(next==null);
        // Repeated timer observations must not erase and repaint the same preview.
        // Replaced source images still invalidate it immediately.
        if(ReferenceEquals(incomingIcon,next)&&ReferenceEquals(incomingImage,next?.Image))return;
        incomingIcon=next;incomingImage=next?.Image;SetNativeDragFeedback(next!=null);
        empty.Text=next==null?EmptyInstructions:next.Image!=null?"":"松开鼠标以收纳图标";
        if(changed)ApplyContentLayout();
        empty.Invalidate();grid.Invalidate();
    }
    private void DrawIncomingIcon(Graphics graphics)
    {
        if(incomingIcon?.Image==null||!NativeDragFeedback)return;
        int columns=Math.Max(1,grid.ClientSize.Width/CellPixels),index=tiles.Count;
        var bounds=new Rectangle(index%columns*CellPixels,index/columns*CellPixels-grid.ScrollOffset,CellPixels,CellPixels);
        IconFeedback.Draw(graphics,bounds,LightSurface||AcrylicActive,true);
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
    internal async void Open(bool preview=false,bool waitForNative=false,bool simultaneous=false)
    {
        // Repeated requests must not cancel an entrance and snap to its destination.
        if(IsPreparing||IsOpening||Visible&&!IsDismissing)return;
        Point? previousTarget=IsDismissing?dismissHome:null;
        if(!previousTarget.HasValue)simultaneousAbove=simultaneous&&!preview&&Placement==PopupPlacement.Above;
        forceNativeAnchor=waitForNative||simultaneous||(previousTarget.HasValue&&forceNativeAnchor);
        if(previousTarget.HasValue)NativeGeometry?.InterruptMeasurement();
        int request=++openRequest;
        Point? interrupted=IsDismissing?Location:null;double interruptedOpacity=Opacity;
        bool wasVisible=Visible;
        if(IsAnimating)CancelDismiss(false,preserveClip:interrupted.HasValue);
        if(interrupted.HasValue)Opacity=interruptedOpacity;
        if(waitForNative&&NativeGeometry!=null&&!preview&&!wasVisible&&NativeOverflow.Visible)
        {
            IsPreparing=true;
            try{openingGeometry=await NativeGeometry.PrepareAsync(()=>request!=openRequest||IsDisposed);}
            catch(Exception error){AppDiagnostics.Warn("TN1101",error:error);openingGeometry=NativeGeometry.CachedOrEstimate();}
            if(request!=openRequest||IsDisposed)return;
            IsPreparing=false;
            if(!NativeOverflow.Visible)return;
        }
        else openingGeometry=NativeGeometry?.CachedOrEstimate();
        RequestRealignment();
        RefreshToolStates();
        bool animate=!wasVisible||interrupted.HasValue;
        DragPreview=preview;
        nint overflow=Native.FindWindowW("TopLevelWindowForOverflowXamlIsland",null);
        uint dpi=overflow!=0?Native.GetDpiForWindow(overflow):(uint)DeviceDpi;
        var layout=openingGeometry?.Layout??NativeTrayLayout.Measure(overflow,dpi,NativeGeometry==null&&!preview&&CanQueryExplorerAutomation);
        IconPixels=layout.Icon;CellPixels=layout.Cell;surfacePadding=layout.Padding;
        computingOpenLayout=true;try{ApplyContentLayout();}finally{computingOpenLayout=false;}
        foreach(var tile in tiles.Values)
        {
            tile.Size=new Size(CellPixels,CellPixels);
            if(tile.Tag is IconRecord record&&record.Image!=null&&tile.Image?.Size!=new Size(IconPixels,IconPixels))
            {
                var previous=tile.Image;tile.Image=new Bitmap(record.Image,new Size(IconPixels,IconPixels));previous?.Dispose();
            }
        }
        // A reversed exit continues toward its resting position, even when Explorer
        // still exposes moving icon bounds. Later real layout changes follow normally.
        Point target=previousTarget??CalculatePosition();
        geometryDirty=false;
        if(Native.GetWindowRect(overflow,out var settledHost))lastNativeHost=Native.Rect(settledHost);
        var options=DismissSettings.Copy();options.Validate();
        if(preview){options.Fade=false;options.DurationMs=Math.Min(options.DurationMs,167);}
        if(simultaneous)options.OpeningFadeDelayMs=0;
        if(animate&&options.Enabled&&options.DurationMs>0&&SystemAnimation.Enabled)
        {
            dismissHome=target;activeDismiss=options;IsOpening=true;
            double travel=Travel(options);
            animationStartOffset=interrupted.HasValue&&interrupted.Value.X==dismissHome.X?interrupted.Value.Y-dismissHome.Y:travel;
            animationEndOffset=0;animationStartOpacity=interrupted.HasValue?interruptedOpacity:options.Fade ? .01 : 1;
            SetMotionBounds(new Point(dismissHome.X,dismissHome.Y+(int)Math.Round(animationStartOffset)));Opacity=animationStartOpacity;
            activeDuration=options.DurationMs;dismissClock.Restart();StartFrames();
        }
        else {ClearRevealClip();Location=target;}
        // Do not hand an empty 1px acrylic window to the compositor. Start the
        // native presentation only when the first actual content is exposed.
        if(!IsOpening||motionVisibleHeight!=0)
        {
            bool alreadyVisible=Visible;Show();
            if(alreadyVisible){EnsureAboveApplications();PaintVisibleContent(true);}
        }
    }
    private void ApplyContentLayout()
    {
        var work=Screen.FromRectangle(EntryRectangle).WorkingArea;
        int count=tiles.Count+(incomingIcon!=null&&!tiles.ContainsKey(incomingIcon.Key)?1:0);
        var layout=ContentTrayLayout.Measure(count,CellPixels,surfacePadding,work.Size,FixedColumns);
        bool resized=fullSurface!=layout.Surface;
        if(resized)RequestRealignment();
        grid.SuspendLayout();
        fullSurface=layout.Surface;
        if(!viewportActive)ClientSize=fullSurface;
        grid.Bounds=new Rectangle(surfacePadding,surfacePadding,fullSurface.Width-surfacePadding*2,fullSurface.Height-surfacePadding*2);empty.Bounds=grid.Bounds;
        grid.ConfigureContent(layout.ContentRows*CellPixels);
        grid.ResumeLayout(true);
        if(layout.Scroll&&incomingIcon!=null)grid.ScrollTo(int.MaxValue);
        UpdateOverflowFade();
        if(resized&&Visible&&!IsDismissing&&!IsAnimating&&!computingOpenLayout)
        {
            if(NativeGeometry==null)StartRelocation(CalculatePosition());
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
    {
        if(!Visible)return;
        // Use Explorer's real handle, not FindWindow("Shell_TrayWnd"), which
        // points at our notification receiver while the bridge is active.
        nint after=Native.IsWindow(layerTaskbar)?layerTaskbar:Native.IsWindow(TaskbarWindow)?TaskbarWindow:new nint(-1);
        if(simultaneousAbove&&NativeOverflow.Visible)after=NativeOverflow.Window;
        // Inserting behind an ordinary HWND also removes WS_EX_TOPMOST.
        // Keep the native panel in front only within the topmost band; a
        // taskbar whose topmost style was changed must not demote our popup.
        if(after!=new nint(-1)&&(Native.GetWindowLongPtrW(after,-20).ToInt64()&8)==0)after=new nint(-1);
        Native.SetWindowPos(Handle,after,0,0,0,0,0x213);
    }
    internal void Dismiss()
    {
        ++openRequest;
        if(IsPreparing){IsPreparing=false;DismissStarted?.Invoke();}
        if(IsOpening&&!Visible){CancelDismiss(false);DismissStarted?.Invoke();return;}
        if(!Visible||IsDismissing)return;
        DismissStarted?.Invoke();
        var options=DismissSettings.Copy();options.Validate();
        if(!options.Enabled||options.DurationMs==0||!SystemAnimation.Enabled){Hide();return;}
        NativeGeometry?.InterruptMeasurement();
        Point current=Location,home=IsAnimating?dismissHome:Location;double opacity=Opacity;
        CancelDismiss(false,preserveClip:true);dismissHome=home;Location=current;Opacity=opacity;
        activeDismiss=options;IsDismissing=true;Enabled=false;
        EnsureAboveApplications();
        animationStartOffset=current.Y-home.Y;animationEndOffset=Travel(options);animationStartOpacity=opacity;
        activeDuration=options.ClosingDuration;dismissClock.Restart();StartFrames();
    }
    private void StartFrames(){compositionFrames??=new CompositionFrameClock(Handle);compositionFrames.Start();}
    private int revealEdge;
    private double Travel(DismissOptions options)
    {
        if(!options.WindowsPopup||DragPreview)return options.DistanceDip*DeviceDpi/96.0;
        var work=Screen.FromPoint(dismissHome).WorkingArea;
        int anchorTop=openingGeometry?.Surface.Top??EntryRectangle.Top;
        // Only the simultaneous upper panel travels through the native panel.
        // Cache this edge for the paired exit, including rapid reversals.
        if(!simultaneousAbove)revealEdge=dismissHome.Y+fullSurface.Height<=anchorTop?anchorTop:work.Bottom;
        else if(IsOpening)revealEdge=NativeEntryRectangle.IsEmpty?work.Bottom:NativeEntryRectangle.Top;
        return Math.Max(fullSurface.Height,revealEdge-dismissHome.Y);
    }
    private Size fullSurface;
    private bool viewportActive;
    private int motionVisibleHeight=-1;
    private void ClearRevealClip()
    {
        motionVisibleHeight=-1;
        if(!viewportActive)return;
        viewportActive=false;
        Size=fullSurface;
    }
    private void SetMotionBounds(Point position)
    {
        if(activeDismiss?.WindowsPopup!=true||DragPreview||isRelocating){Location=position;return;}
        // The acrylic backdrop and child HWNDs must share the same physical edge.
        // Keep the grid laid out at full size; expose it by sizing the actual host.
        int edge=Math.Min(revealEdge,Screen.FromPoint(dismissHome).WorkingArea.Bottom);
        int visible=Math.Clamp(edge-position.Y,0,fullSurface.Height);
        viewportActive=visible!=fullSurface.Height;
        motionVisibleHeight=visible;
        var physical=position;
        // Windows can enforce a 2px HWND even when 1px was requested. Bound the
        // complete physical host, not only the logical exposure or its region.
        int physicalHeight=Math.Max(2,visible);
        physical.Y=Math.Min(physical.Y,edge-physicalHeight);
        Bounds=new Rectangle(physical,new Size(fullSurface.Width,physicalHeight));
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool RedrawWindow(nint hwnd,nint rect,nint region,uint flags);
    private void PaintVisibleContent(bool eraseBackground=false)
    {
        // Resizing child HWNDs can expose stale pixels on every moving frame.
        // Repaint the background and children together; refresh nonclient edges
        // as well when presenting the complete, settled surface.
        if(!Visible)return;
        grid.PaintingFrame=true;
        try{RedrawWindow(Handle,0,0,eraseBackground?0x585u:0x185u);}
        finally{grid.PaintingFrame=false;}
    }
    private void StartRelocation(Point destination)
    {
        if(destination==Location)return;
        if(!Visible||!SystemAnimation.Enabled){Location=destination;return;}
        CancelDismiss(false);relocationStart=Location;dismissHome=destination;isRelocating=true;
        activeDismiss=new DismissOptions{Fade=false};activeDuration=167;dismissClock.Restart();StartFrames();
    }
    private void DrawDismissFrame()
    {
        if(!IsAnimating||activeDismiss==null){compositionFrames?.Stop();return;}
        double elapsed=dismissClock.Elapsed.TotalMilliseconds,t=Math.Clamp(elapsed/Math.Max(1,activeDuration),0,1);
        double p=activeDismiss.WindowsPopup?(IsDismissing?Math.Pow(t,3.8):1-Math.Pow(1-t,3.2)):IsOpening?1-activeDismiss.Progress(1-t):activeDismiss.Progress(t);
        var next=isRelocating?new Point((int)Math.Round(relocationStart.X+(dismissHome.X-relocationStart.X)*p),(int)Math.Round(relocationStart.Y+(dismissHome.Y-relocationStart.Y)*p)):new Point(dismissHome.X,dismissHome.Y+(int)Math.Round(animationStartOffset+(animationEndOffset-animationStartOffset)*p));
        SetMotionBounds(next);
        if(IsOpening&&!Visible&&motionVisibleHeight>0)Show();
        PaintVisibleContent();
        if(activeDismiss.Fade)
        {
            double fade=activeDismiss.WindowsPopup&&IsOpening?Math.Clamp((elapsed-(animationStartOpacity>.02?0:activeDismiss.OpeningFadeDelayMs))/83,0,1):t;
            Opacity=Math.Clamp(animationStartOpacity+((IsOpening?1:0)-animationStartOpacity)*fade,.01,1);
        }
        MotionFrame?.Invoke(Location,System.Diagnostics.Stopwatch.GetTimestamp());
        if(t>=1)
        {
            if(IsDismissing)Hide();else {CancelDismiss(false);PaintVisibleContent(true);}
            MotionCompleted?.Invoke();
        }
    }
    private void CancelDismiss(bool restoreHome=true,bool preserveClip=false)
    {
        if(!preserveClip)ClearRevealClip();
        compositionFrames?.Stop();if(restoreHome&&IsAnimating)Location=dismissHome;
        IsDismissing=false;IsOpening=false;isRelocating=false;activeDismiss=null;Opacity=1;Enabled=true;
    }
    private void HoldComposition(bool hold)
    {
        int cloak=hold?1:0;
        int result=Native.DwmSetWindowAttribute(Handle,13,ref cloak,sizeof(int)); // DWMWA_CLOAK
        if(result<0)System.Runtime.InteropServices.Marshal.ThrowExceptionForHR(result);
    }
    protected override void SetVisibleCore(bool value)
    {
        if(value&&!Visible)
        {
            // Show raises a topmost HWND before EnsureAboveApplications can
            // put it behind the taskbar/native popup. Do not compose that
            // intermediate state, particularly when reversing a recent exit.
            HoldComposition(true);
            try{base.SetVisibleCore(true);EnsureAboveApplications();PaintVisibleContent(true);}
            finally{if(Visible)HoldComposition(false);}
            return;
        }
        if(!value)
        {
            ++openRequest;IsPreparing=false;
            if(pressedTile!=null)FinishTilePress(pressedTile,Point.Empty,true);
            // Retire the last narrow acrylic surface before hiding/restoring
            // the full layout for the next entrance.
            if(IsHandleCreated)HoldComposition(true);
        }
        base.SetVisibleCore(value);
        if(!value&&IsAnimating)CancelDismiss();
    }
    private void PositionPanel()
    {Location=CalculatePosition();}
    private Point CalculatePosition()
    {
        nint overflow=Native.FindWindowW("TopLevelWindowForOverflowXamlIsland",null);
        var work=Screen.FromRectangle(EntryRectangle).WorkingArea;
        var anchor=EntryRectangle;
        if(!IndependentEntry&&overflow!=0&&Native.IsWindowVisible(overflow))
        {
            // Explorer may be waiting on our drag target; querying its UIA
            // provider synchronously from here can create a circular wait.
            var source=openingGeometry?.Surface??NativeOverflow.SurfaceBounds(overflow,surfacePadding,NativeGeometry==null&&CanQueryExplorerAutomation);
            if(forceNativeAnchor||source.IntersectsWith(EntryRectangle))anchor=source;
        }
        layerTaskbar=NativeOverflow.TaskbarForBounds(EntryRectangle,TaskbarWindow);
        var panelSize=viewportActive?fullSurface:Size;
        if(IndependentEntry)return PopupPosition.CenterAbove(EntryRectangle,panelSize,work);
        return PopupPosition.Calculate(anchor,panelSize,work,Placement);
    }
    private void BackgroundMenu(object? sender,MouseEventArgs e){if(e.Button==MouseButtons.Right)OpenMenu?.Invoke();}
    protected override void Dispose(bool disposing)
    {if(disposing){++openRequest;IsPreparing=false;if(pressedTile!=null)FinishTilePress(pressedTile,Point.Empty,true);reorderFrames.Dispose();reorderFeedback.Dispose();CancelDismiss();compositionFrames?.Dispose();CoreDrop.Dispose();tips.Dispose();foreach(var tile in tiles.Values)tile.Image?.Dispose();}base.Dispose(disposing);}
}
