using System.Drawing.Drawing2D;

namespace NestedTray;
internal static class IconFeedback
{
    // Stroke only the perimeter so the center retains its material and color.
    internal static void Draw(Graphics graphics,Rectangle bounds,bool darkEdge,bool active)
    {
        var state=graphics.Save();
        try
        {
            graphics.SmoothingMode=SmoothingMode.AntiAlias;
            var edge=darkEdge?Color.Black:Color.White;
            for(int inset=1;inset<=3;inset++)
            {
                var rect=RectangleF.FromLTRB(bounds.Left+inset,bounds.Top+inset,bounds.Right-1-inset,bounds.Bottom-1-inset);
                float diameter=Math.Min(bounds.Height/5f,Math.Min(rect.Width,rect.Height));
                if(diameter<=0)continue;
                using var path=new GraphicsPath();
                path.AddArc(rect.Left,rect.Top,diameter,diameter,180,90);
                path.AddArc(rect.Right-diameter,rect.Top,diameter,diameter,270,90);
                path.AddArc(rect.Right-diameter,rect.Bottom-diameter,diameter,diameter,0,90);
                path.AddArc(rect.Left,rect.Bottom-diameter,diameter,diameter,90,90);path.CloseFigure();
                int alpha=inset==2?(active?110:75):inset==1?18:10;
                using var pen=new Pen(Color.FromArgb(alpha,edge));graphics.DrawPath(pen,path);
            }
        }
        finally{graphics.Restore(state);}
    }
}
