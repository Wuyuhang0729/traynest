using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace NestedTray;

internal sealed class SettingsWindow:Form
{
    private readonly Settings settings;
    private readonly Action changed;
    private readonly Func<bool> readStartup;
    private readonly Action<bool> writeStartup;
    private readonly Panel content=new(){Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(32,24,32,20)};
    private readonly Label status=new(){Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(32,0,20,0),AutoEllipsis=true};
    private readonly List<Button> navigation=[];
    private readonly Color accent=Color.FromArgb(24,98,211),muted=Color.FromArgb(99,110,128);
    private readonly List<Font> ownedFonts=[];
    private int page;
    internal Action? OpenDiagnostics;
    internal Action? OpenPanelRequested,RefreshIconsRequested,RestoreAllRequested,ExitRequested;
    internal Func<bool,Task>? ChangeMaterialSync;
    internal void ShowOperationStatus(string text,bool error=false){status.Text=text;status.ForeColor=error?Color.Firebrick:muted;}
    internal void RefreshValues()=>SelectPage(page);
    internal SettingsWindow(Settings settings,Action changed,Func<bool> readStartup,Action<bool> writeStartup)
    {
        this.settings=settings;this.changed=changed;this.readStartup=readStartup;this.writeStartup=writeStartup;
        Text="TrayNest 设置";Name="TrayNestSettings";Icon=AppIcon.Load();StartPosition=FormStartPosition.CenterScreen;
        AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new SizeF(96,96);ClientSize=new Size(920,680);MinimumSize=new Size(760,570);
        Font=OwnFont(10);BackColor=Color.FromArgb(246,248,251);ForeColor=Color.FromArgb(26,35,50);
        var shell=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,Margin=Padding.Empty};
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,196));shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent,100));shell.RowStyles.Add(new RowStyle(SizeType.Absolute,46));
        var side=new Panel{Dock=DockStyle.Fill,BackColor=Color.FromArgb(235,240,247),Padding=new Padding(20),Margin=Padding.Empty};
        var brand=new Label{Text="TrayNest",Font=OwnFont(21,FontStyle.Bold),Location=new Point(16,30),AutoSize=true};side.Controls.Add(brand);
        var subtitle=new Label{Text="按你的习惯，安排图标",ForeColor=muted,Location=new Point(20,76),AutoSize=true};side.Controls.Add(subtitle);
        string[] names=["常规","面板","关于"];
        for(int i=0;i<names.Length;i++)
        {
            int target=i;var button=new Button{Text=names[i],Name="Nav"+i,AccessibleName=names[i],FlatStyle=FlatStyle.Flat,TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(14,0,0,0),Bounds=new Rectangle(12,136+i*54,172,44),Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right,Cursor=Cursors.Hand};
            button.FlatAppearance.BorderSize=0;button.Click+=(_,_)=>SelectPage(target);side.Controls.Add(button);navigation.Add(button);
        }
        var exit=new Button{Text="退出 TrayNest",Name="ExitApplication",AccessibleName="退出 TrayNest",Dock=DockStyle.Bottom,Height=38,FlatStyle=FlatStyle.Flat,Cursor=Cursors.Hand};
        exit.FlatAppearance.BorderColor=Color.FromArgb(201,211,225);exit.Click+=(_,_)=>ExitRequested?.Invoke();side.Controls.Add(exit);
        shell.Controls.Add(side,0,0);shell.SetRowSpan(side,2);shell.Controls.Add(content,1,0);shell.Controls.Add(status,1,1);Controls.Add(shell);
        Shown+=(_,_)=>{var work=Screen.FromControl(this).WorkingArea;if(Width>work.Width||Height>work.Height){MinimumSize=Size.Empty;Size=new Size(Math.Min(Width,work.Width-24),Math.Min(Height,work.Height-24));CenterToScreen();}};
        SelectPage(0);
    }
    private Font OwnFont(float size,FontStyle style=FontStyle.Regular){var existing=ownedFonts.FirstOrDefault(font=>font.Size==size&&font.Style==style);if(existing!=null)return existing;var font=new Font("Microsoft YaHei UI",size,style);ownedFonts.Add(font);return font;}
    internal void SelectPage(int index)
    {
        page=index;
        foreach(var button in navigation){bool active=navigation.IndexOf(button)==page;button.BackColor=active?Color.FromArgb(215,230,252):Color.FromArgb(235,240,247);button.ForeColor=active?accent:ForeColor;}
        content.SuspendLayout();foreach(Control control in content.Controls.Cast<Control>().ToArray())control.Dispose();content.Controls.Clear();content.AutoScrollPosition=Point.Empty;
        var stack=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=1,Margin=Padding.Empty};stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        void Add(Control control){control.Margin=new Padding(0,0,0,14);control.Dock=DockStyle.Top;stack.Controls.Add(control);}
        Add(new Label{Text=new[]{"常规","面板","关于 TrayNest"}[page],AutoSize=true,Font=OwnFont(25,FontStyle.Bold)});
        Add(new Label{Text=new[]{"决定什么时候展开，以及如何启动。","位置、布局和材质，放在一起调整。","给系统隐藏区，再添一个自选空间。"}[page],AutoSize=true,ForeColor=muted,Padding=new Padding(0,0,0,12)});
        if(page==0)
        {
            Add(Heading("自动展开"));
            Add(ChoiceRow("ExpansionMode","展开方式","选择一种：手动打开、随系统隐藏区打开，或悬停入口约半秒。",["手动展开","跟随系统隐藏区","悬停入口"],settings.FollowNativeOverflow?1:settings.HoverToOpen?2:0,index=>settings.ChangeBehavior(index==1,index==2,settings.MatchPanelMaterials)));
            Add(new Label{Text="拖动图标靠近入口时，仍会自动展开。",ForeColor=muted,AutoSize=true,MaximumSize=new Size(570,0)});
            Add(Heading("启动"));
            bool enabled=false;string detail="登录 Windows 后自动运行 TrayNest。";
            try{enabled=readStartup();}catch{detail="暂时无法读取启动状态，请稍后重试。";}
            Add(ToggleRow("StartupEnabled","开机启动",detail,enabled,value=>writeStartup(value)));
            Add(Heading("应用操作"));
            Add(ActionRow("打开收纳面板","查看已收纳的图标",()=>OpenPanelRequested?.Invoke()));
            Add(ActionRow("刷新图标","重新获取正在运行的软件图标",()=>RefreshIconsRequested?.Invoke()));
        }
        else if(page==1)
        {
            Add(Heading("排列"));
            Add(ChoiceRow("Placement","展开位置","相对于系统隐藏区的位置。",["左侧","上方"],settings.Placement==PopupPlacement.Left?0:1,index=>settings.ChangePlacement(index==0?PopupPlacement.Left:PopupPlacement.Above)));
            Add(ChoiceRow("Columns","每行图标数","按收纳数量和屏幕空间调整。",["5 个","6 个","7 个","8 个"],settings.Columns-5,index=>{int previous=settings.Columns;settings.Columns=index+5;try{settings.Save();}catch{settings.Columns=previous;throw;}}));
            Add(Heading("材质与颜色"));
            Add(ChoiceRow("Appearance","收纳面板外观","亚克力效果会保留背景的模糊与透光。",["跟随系统托盘","亚克力","浅色","深色"],(int)settings.Appearance,index=>settings.ChangeAppearance((PanelAppearance)index)));
            var material=new SettingsToggle{Name="MatchPanelMaterials",AccessibleName="统一两个面板的材质",Checked=settings.MatchPanelMaterials};bool resetting=false;
            material.CheckedChanged+=async(_,_)=>
            {
                if(resetting)return;bool requested=material.Checked;material.Enabled=false;ShowOperationStatus("正在应用主面板材质…");
                try{if(ChangeMaterialSync==null)throw new NativeMaterialException("材质组件尚未准备好。");await ChangeMaterialSync(requested);if(!IsDisposed)ShowOperationStatus(requested?"已保存，两个面板的外观已同步":"已关闭，系统背景已恢复");}
                catch(Exception error){if(!material.IsDisposed){resetting=true;material.Checked=settings.MatchPanelMaterials;resetting=false;}if(!IsDisposed)ShowOperationStatus(error is NativeMaterialException?error.Message:"未能保存，已保留原设置。",true);}
                finally{if(!material.IsDisposed)material.Enabled=true;}
            };
            Add(Row("统一两个面板的材质","让系统隐藏区主面板采用相同外观；关闭后恢复系统背景。",material));
            Add(Heading("收纳管理"));
            Add(ActionRow("移回全部并清空收纳","恢复系统图标位置，并移除保存的收纳项目。",()=>RestoreAllRequested?.Invoke()));
        }
        else
        {
            Add(Heading("TrayNest · 托盘收纳盒"));
            Add(new Label{Text="版本 "+Application.ProductVersion.Split('+')[0]+"\n\n手动挑选图标，保留你习惯的顺序。\n布局和外观设置保存在当前用户目录。",AutoSize=true,ForeColor=muted});
            Add(ActionRow("项目主页","使用说明、更新与源代码",()=>Process.Start(new ProcessStartInfo("https://github.com/Wuyuhang0729/traynest"){UseShellExecute=true})));
            Add(ActionRow("诊断与反馈","查看或导出诊断报告",()=>OpenDiagnostics?.Invoke()));
        }
        content.Controls.Add(stack);content.ResumeLayout(true);
    }
    private Label Heading(string text)=>new(){Text=text,Font=OwnFont(11,FontStyle.Bold),AutoSize=true,Padding=new Padding(0,8,0,2)};
    private TableLayoutPanel Row(string title,string description,Control input)
    {
        var row=new TableLayoutPanel{BackColor=Color.White,AutoSize=true,MinimumSize=new Size(0,90),ColumnCount=2,RowCount=1,Padding=new Padding(18,15,18,15)};
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,input is ComboBox?168:66));
        var text=new TableLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,ColumnCount=1,Margin=Padding.Empty};
        text.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        text.Controls.Add(new Label{Text=title,Font=OwnFont(10,FontStyle.Bold),AutoSize=true,Margin=new Padding(0,0,10,7)});
        var desc=new Label{Text=description,ForeColor=muted,AutoSize=true,Dock=DockStyle.Top,Margin=new Padding(0,0,12,0)};
        text.SizeChanged+=(_,_)=>desc.MaximumSize=new Size(Math.Max(100,text.ClientSize.Width-12),0);text.Controls.Add(desc);
        input.Anchor=AnchorStyles.Right;input.Margin=Padding.Empty;row.Controls.Add(text,0,0);row.Controls.Add(input,1,0);return row;
    }
    private Control ToggleRow(string name,string title,string description,bool current,Action<bool> apply)
    {
        var toggle=new SettingsToggle{Name=name,AccessibleName=title,Checked=current};bool syncing=false;
        toggle.CheckedChanged+=(_,_)=>{if(syncing)return;if(!Save(()=>apply(toggle.Checked))){syncing=true;toggle.Checked=!toggle.Checked;syncing=false;}};
        return Row(title,description,toggle);
    }
    private Control ChoiceRow(string name,string title,string description,string[] choices,int current,Action<int> apply)
    {
        var combo=new ComboBox{Name=name,AccessibleName=title,DropDownStyle=ComboBoxStyle.DropDownList,Width=160,FlatStyle=FlatStyle.Standard};combo.Items.AddRange(choices);combo.SelectedIndex=current;int previous=current;bool syncing=false;
        combo.SelectedIndexChanged+=(_,_)=>{if(syncing)return;if(Save(()=>apply(combo.SelectedIndex)))previous=combo.SelectedIndex;else{syncing=true;combo.SelectedIndex=previous;syncing=false;}};
        return Row(title,description,combo);
    }
    private Control ActionRow(string title,string description,Action action)
    {var button=new Button{Text="打开",Width=64,Height=32,AccessibleName=title};button.Click+=(_,_)=>{try{action();}catch(Exception){status.ForeColor=Color.Firebrick;status.Text="未能打开，请稍后重试。";}};return Row(title,description,button);}
    private bool Save(Action action)
    {
        try{action();changed();status.ForeColor=muted;status.Text="已保存，立即生效";return true;}
        catch(Exception error){AppDiagnostics.Error(error is TrayNestException known?known.Code:"TN4001",error);status.ForeColor=Color.Firebrick;status.Text="未能保存，已保留原设置。";return false;}
    }
    protected override void Dispose(bool disposing){base.Dispose(disposing);if(disposing)foreach(var font in ownedFonts)font.Dispose();}
}

internal sealed class SettingsToggle:CheckBox
{
    internal SettingsToggle(){Size=new Size(52,28);FlatStyle=FlatStyle.Flat;Cursor=Cursors.Hand;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}
    protected override void OnCheckedChanged(EventArgs e){base.OnCheckedChanged(e);Invalidate();}
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        float scale=DeviceDpi/96f;var bounds=new RectangleF(1,1,Width-2,Height-2);float radius=bounds.Height;
        using var path=new GraphicsPath();path.AddArc(bounds.X,bounds.Y,radius,radius,90,180);path.AddArc(bounds.Right-radius,bounds.Y,radius,radius,270,180);path.CloseFigure();
        using var track=new SolidBrush(Enabled?(Checked?Color.FromArgb(24,98,211):Color.FromArgb(150,160,175)):SystemColors.ControlDark);e.Graphics.FillPath(track,path);
        float diameter=Height-8*scale;using var knob=new SolidBrush(Color.White);e.Graphics.FillEllipse(knob,Checked?Width-diameter-4*scale:4*scale,4*scale,diameter,diameter);
        if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,ClientRectangle);
    }
}
