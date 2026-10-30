using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace NestedTray;
internal sealed class IconGrid:FlowLayoutPanel
{
    private int contentHeight,offset;
    internal int ScrollOffset=>offset;
    internal int MaximumOffset=>Math.Max(0,contentHeight-ClientSize.Height);
    internal Action? Scrolled;
    public override Rectangle DisplayRectangle=>new(0,-offset,ClientSize.Width,Math.Max(ClientSize.Height,contentHeight));
    internal void ConfigureContent(int height){contentHeight=height;ScrollTo(offset);PerformLayout();}
    internal void ScrollTo(int value){int next=Math.Clamp(value,0,MaximumOffset);if(next==offset)return;offset=next;PerformLayout();Invalidate();Scrolled?.Invoke();}
    protected override void OnMouseWheel(MouseEventArgs e){if(e is HandledMouseEventArgs handled)handled.Handled=true;ScrollTo(offset-e.Delta/120*120);}
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
                for(int channel=0;channel<3;channel++)original[at+channel]=(byte)((original[at+channel]*(1-blend)+blurred[at+channel]*blend)*(1-shade)+(channel==0?40:channel==1?34:32)*shade);
                original[at+3]=255;
            }
            Marshal.Copy(original,0,data.Scan0,original.Length);
        }
        finally{region.UnlockBits(data);}
        e.Graphics.DrawImageUnscaled(region,0,0);
        using var pen=new Pen(Color.FromArgb(210,225,235),2);int cx=Width/2,cy=Height-12;
        e.Graphics.DrawLines(pen,[new Point(cx-5,cy-3),new Point(cx,cy+2),new Point(cx+5,cy-3)]);
    }
}
