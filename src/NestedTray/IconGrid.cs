using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace NestedTray;
internal sealed class IconGrid:FlowLayoutPanel
{
    internal bool GlassSurface;
    protected override void OnPaintBackground(PaintEventArgs e)
    {if(GlassSurface)PanelTheme.PaintGlass(e.Graphics,ClientRectangle);else base.OnPaintBackground(e);}
    private sealed class GridEngine: System.Windows.Forms.Layout.LayoutEngine
    {
        public override bool Layout(object container,LayoutEventArgs args){((IconGrid)container).ArrangeIcons();return false;}
    }
    private readonly GridEngine engine=new();
    public override System.Windows.Forms.Layout.LayoutEngine LayoutEngine=>engine;
    private readonly Dictionary<Control,Rectangle> targets=new(),origins=new();
    private readonly System.Windows.Forms.Timer motion=new(){Interval=15};
    private readonly System.Diagnostics.Stopwatch motionClock=new();
    private bool arranging,animateLayout;
    internal const int ReorderDurationMs=180;
    internal bool IsAnimatingOrder=>motion.Enabled;
    internal IconGrid(){motion.Tick+=(_,_)=>DrawMotion();}
    private int contentHeight,offset;
    internal int ScrollOffset=>offset;
    internal int MaximumOffset=>Math.Max(0,contentHeight-ClientSize.Height);
    internal Action? Scrolled;
    internal int OrderIndexAt(Point point)
    {
        if(Controls.Count==0)return -1;
        var cell=Controls[0].Size;int columns=Math.Max(1,ClientSize.Width/Math.Max(1,cell.Width));
        return Math.Clamp((point.Y+offset)/Math.Max(1,cell.Height)*columns+point.X/Math.Max(1,cell.Width),0,Controls.Count-1);
    }
    internal void AnimateOrder(Action change)
    {
        bool previous=animateLayout;animateLayout=SystemAnimation.Enabled;SuspendLayout();
        try{change();}finally{ResumeLayout(true);animateLayout=previous;}
    }
    internal void MoveIcon(Control tile,int index)=>AnimateOrder(()=>Controls.SetChildIndex(tile,index));
    private void ArrangeIcons()
    {
        if(arranging)return;
        arranging=true;
        try
        {
            var next=new Dictionary<Control,Rectangle>();
            if(Controls.Count>0)
            {
                var cell=Controls[0].Size;int columns=Math.Max(1,ClientSize.Width/Math.Max(1,cell.Width));
                for(int index=0;index<Controls.Count;index++)next[Controls[index]]=new Rectangle(index%columns*cell.Width,index/columns*cell.Height-offset,cell.Width,cell.Height);
            }
            bool changed=next.Count!=targets.Count||next.Any(pair=>!targets.TryGetValue(pair.Key,out var old)||old!=pair.Value);
            if(!changed&&motion.Enabled)return;
            if(changed&&animateLayout&&next.Keys.All(targets.ContainsKey))
            {
                origins.Clear();foreach(var tile in next.Keys)origins[tile]=tile.Bounds;
                targets.Clear();foreach(var pair in next)targets[pair.Key]=pair.Value;
                motionClock.Restart();motion.Start();return;
            }
            motion.Stop();origins.Clear();targets.Clear();
            foreach(var pair in next){targets[pair.Key]=pair.Value;if(pair.Key.Bounds!=pair.Value)pair.Key.Bounds=pair.Value;}
        }
        finally{arranging=false;}
    }
    private void DrawMotion()
    {
        double t=Math.Clamp(motionClock.Elapsed.TotalMilliseconds/ReorderDurationMs,0,1),ease=1-Math.Pow(1-t,3);
        arranging=true;SuspendLayout();
        try
        {
            foreach(var pair in targets.ToArray())
            {
                if(pair.Key.IsDisposed||!Controls.Contains(pair.Key))continue;
                var start=origins.GetValueOrDefault(pair.Key,pair.Value);var end=pair.Value;
                pair.Key.Bounds=new Rectangle((int)Math.Round(start.X+(end.X-start.X)*ease),(int)Math.Round(start.Y+(end.Y-start.Y)*ease),end.Width,end.Height);
            }
        }
        finally{ResumeLayout(false);arranging=false;}
        if(t>=1){motion.Stop();origins.Clear();}
    }
    public override Rectangle DisplayRectangle=>new(0,-offset,ClientSize.Width,Math.Max(ClientSize.Height,contentHeight));
    internal void ConfigureContent(int height){contentHeight=height;ScrollTo(offset);PerformLayout();}
    internal void ScrollTo(int value){int next=Math.Clamp(value,0,MaximumOffset);if(next==offset)return;offset=next;PerformLayout();Invalidate();Scrolled?.Invoke();}
    protected override void OnMouseWheel(MouseEventArgs e){if(e is HandledMouseEventArgs handled)handled.Handled=true;ScrollTo(offset-e.Delta/120*120);}
    protected override void Dispose(bool disposing){if(disposing)motion.Dispose();base.Dispose(disposing);}
}

internal sealed class OverflowFade(IconGrid source,Action advance):Control
{
    protected override void OnMouseWheel(MouseEventArgs e){if(e is HandledMouseEventArgs handled)handled.Handled=true;source.ScrollTo(source.ScrollOffset-e.Delta/120*120);}
    protected override void OnMouseClick(MouseEventArgs e){if(e.Button==MouseButtons.Left)advance();}
    protected override void OnPaint(PaintEventArgs e)
    {
        if(Width<=0||Height<=0)return;
        using var full=new Bitmap(source.Width,source.Height,PixelFormat.Format32bppArgb);
        source.DrawToBitmap(full,new Rectangle(Point.Empty,full.Size));
        using var region=full.Clone(new Rectangle(0,full.Height-Height,Width,Height),PixelFormat.Format32bppArgb);
        var data=region.LockBits(new Rectangle(Point.Empty,region.Size),ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);
        try
        {
            int width=region.Width,height=region.Height,stride=data.Stride;
            var original=new byte[stride*height];Marshal.Copy(data.Scan0,original,0,original.Length);
            var temp=new byte[original.Length];var blurred=new byte[original.Length];int radius=4;
            for(int y=0;y<height;y++)for(int channel=0;channel<3;channel++)
            {
                int sum=0;for(int k=-radius;k<=radius;k++)sum+=original[y*stride+Math.Clamp(k,0,width-1)*4+channel];
                for(int x=0;x<width;x++){temp[y*stride+x*4+channel]=(byte)(sum/(radius*2+1));sum-=original[y*stride+Math.Clamp(x-radius,0,width-1)*4+channel];sum+=original[y*stride+Math.Clamp(x+radius+1,0,width-1)*4+channel];}
            }
            for(int x=0;x<width;x++)for(int channel=0;channel<3;channel++)
            {
                int sum=0;for(int k=-radius;k<=radius;k++)sum+=temp[Math.Clamp(k,0,height-1)*stride+x*4+channel];
                for(int y=0;y<height;y++){blurred[y*stride+x*4+channel]=(byte)(sum/(radius*2+1));sum-=temp[Math.Clamp(y-radius,0,height-1)*stride+x*4+channel];sum+=temp[Math.Clamp(y+radius+1,0,height-1)*stride+x*4+channel];}
            }
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                double blend=y/(double)Math.Max(1,height-1),shade=blend*.4;int at=y*stride+x*4;
                for(int channel=0;channel<3;channel++)original[at+channel]=(byte)((original[at+channel]*(1-blend)+blurred[at+channel]*blend)*(1-shade)+(channel==0?BackColor.B:channel==1?BackColor.G:BackColor.R)*shade);
                original[at+3]=255;
            }
            Marshal.Copy(original,0,data.Scan0,original.Length);
        }
        finally{region.UnlockBits(data);}
        e.Graphics.DrawImageUnscaled(region,0,0);
        using var pen=new Pen(ForeColor,2);int cx=Width/2,cy=Height-12;
        e.Graphics.DrawLines(pen,[new Point(cx-5,cy-3),new Point(cx,cy+2),new Point(cx+5,cy-3)]);
    }
}
