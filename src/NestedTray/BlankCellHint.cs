namespace NestedTray;
internal sealed class BlankCellHint:Form
{
    internal BlankCellHint()
    {
        Name="BlankCellHint";Text="双击空白处使用说明";Icon=AppIcon.Load();StartPosition=FormStartPosition.CenterParent;
        AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new SizeF(96,96);ClientSize=new Size(560,300);
        FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;ShowInTaskbar=false;
        Font=new Font("Microsoft YaHei UI",10);BackColor=Color.FromArgb(246,248,251);
        var heading=new Label{Dock=DockStyle.Top,Height=66,Padding=new Padding(24,16,20,0),Text="请确认面板有空白处，双击空白处即可快速打开/关闭收纳面板"};
        var picture=new HintPicture{Dock=DockStyle.Fill,AccessibleName="用户提供的系统隐藏区图片，红框标出了可双击的空白处"};
        var footer=new Panel{Dock=DockStyle.Bottom,Height=66};
        var note=new Label{Text="此功能仅在联动模式下可用。",Location=new Point(24,18),Size=new Size(400,36)};
        var button=new Button{Text="知道了",Name="HintConfirm",DialogResult=DialogResult.OK,Size=new Size(92,32),Location=new Point(442,16),Anchor=AnchorStyles.Top|AnchorStyles.Right};
        footer.Controls.AddRange([note,button]);Controls.Add(picture);Controls.Add(heading);Controls.Add(footer);AcceptButton=button;CancelButton=button;
    }
    internal static Bitmap RenderIllustration()
    {
        using var stream=typeof(BlankCellHint).Assembly.GetManifestResourceStream("NestedTray.BlankCellHelp.png")??throw new InvalidOperationException("功能说明图片缺失。");
        using var source=Image.FromStream(stream);return new Bitmap(source);
    }
    internal static void ShowFor(IWin32Window? owner){using var hint=new BlankCellHint();hint.ShowDialog(owner);}
    private sealed class HintPicture:Control
    {
        private readonly Bitmap picture=RenderIllustration();
        internal HintPicture(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);double scale=Math.Min(DeviceDpi/96.0,Math.Min((Width-32)/(double)picture.Width,(Height-12)/(double)picture.Height));
            int width=Math.Max(1,(int)Math.Round(picture.Width*scale)),height=Math.Max(1,(int)Math.Round(picture.Height*scale));
            e.Graphics.DrawImage(picture,new Rectangle((Width-width)/2,(Height-height)/2,width,height));
        }
        protected override void Dispose(bool disposing){if(disposing)picture.Dispose();base.Dispose(disposing);}
    }
    protected override void Dispose(bool disposing){var owned=Font;base.Dispose(disposing);if(disposing)owned.Dispose();}
}
