using System.Text.Json;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length is < 1 or > 4) return 2;
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using var window = new FixtureWindow(args[0], args.Length >= 2 ? args[1] : "NestedTray controlled test",args.Length>=3?Guid.Parse(args[2]):null,args.Length==4&&args[3]=="legacy-unused-guid");
        Application.Run();
        return 0;
    }
}

internal sealed class FixtureWindow : NativeWindow, IDisposable
{
    private readonly string directory;
    private readonly Guid guid = Guid.NewGuid();
    private readonly bool legacyIdentity;
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 400 };
    private readonly System.Windows.Forms.Timer timeout = new() { Interval = 30000 };
    private int tick;
    private bool registered;
    private readonly string label;
    private readonly ContextMenuStrip menu = new();
    private Form? outside;
    private Form? oleSource;
    private sealed class OleSourceWindow:Form {protected override bool ShowWithoutActivation=>true;}
    private const uint Id = 73;
    private const uint Callback = 0x8000 + 327;

    internal FixtureWindow(string directory, string label,Guid? fixedGuid=null,bool legacyIdentity=false)
    {
        this.directory = directory;
        if(fixedGuid.HasValue)guid=fixedGuid.Value;
        this.legacyIdentity=legacyIdentity;
        if(legacyIdentity)guid=Guid.Empty;
        this.label = label;
        var command = menu.Items.Add("NestedTray fixture command");
        command.Click += (_, _) => File.AppendAllText(Path.Combine(directory, "commands.log"), "CommandSelected\n");
        Directory.CreateDirectory(directory);
        CreateHandle(new CreateParams { Caption = "NestedTray verification fixture", Parent = new nint(-3) });
        var icon = MakeData();
        icon.Flags = 1 | 2 | 4 | (legacyIdentity?0u:0x20u);
        icon.CallbackMessage = Callback;
        icon.Icon = SystemIcons.Information.Handle;
        icon.Tip = label;
        if (!NativeTray.Notify(0, ref icon)) throw new InvalidOperationException("Fixture NIM_ADD failed.");
        registered = true;
        icon.Version = legacyIdentity?0u:4u;
        if (!legacyIdentity&&!NativeTray.Notify(4, ref icon)) { Dispose(); throw new InvalidOperationException("Fixture NIM_SETVERSION failed."); }
        File.WriteAllText(Path.Combine(directory, "fixture.json"), JsonSerializer.Serialize(new FixtureManifest(Handle.ToInt64(), Id, guid, Callback, icon.Version)));
        timer.Tick += (_, _) =>
        {
            var update = MakeData();
            update.Flags = 2 | 4 | (legacyIdentity?0u:0x20u);
            update.Icon = (++tick % 2 == 0 ? SystemIcons.Information : SystemIcons.Warning).Handle;
            update.Tip = $"{label} {tick}";
            bool ok = NativeTray.Notify(1, ref update);
            if (!ok) File.AppendAllText(Path.Combine(directory, "fixture-errors.log"), "NIM_MODIFY failed\n");
        };
        timeout.Tick += (_, _) => Application.ExitThread();
        timer.Start(); timeout.Start();
    }

    // Vary unused storage to catch both incorrect queries and split records.
    private NativeTray.Data MakeData() => NativeTray.New(Handle, Id, legacyIdentity?Guid.NewGuid():guid);

    protected override void WndProc(ref Message message)
    {
        if(message.Msg==0x8000+906)
        {
            var update=MakeData();update.Flags=1|2|4|(legacyIdentity?0u:0x20u);update.CallbackMessage=Callback;update.Icon=SystemIcons.Information.Handle;update.Tip=label;
            NativeTray.Notify(1,ref update);update.Version=legacyIdentity?0u:4u;if(!legacyIdentity)NativeTray.Notify(4,ref update);return;
        }
        if(message.Msg==0x8000+905){if(message.WParam!=0)timer.Stop();else timer.Start();return;}
        if(message.Msg==0x8000+903)
        {
            var identity=new NativeTray.Identity {Size=(uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeTray.Identity>(),Window=Handle,Id=Id,Guid=guid};
            NativeTray.GetRect(ref identity,out var rect);
            uint requested=unchecked((uint)message.WParam.ToInt64());
            if(requested!=0){rect.Left=unchecked((short)(requested&0xFFFF));rect.Top=unchecked((short)(requested>>16));}
            oleSource?.Dispose();oleSource=new OleSourceWindow{Text="NestedTray fixture OLE source",ShowInTaskbar=false,TopMost=true,FormBorderStyle=FormBorderStyle.None,StartPosition=FormStartPosition.Manual,Location=new Point(rect.Left,rect.Top),ClientSize=new Size(40,40),BackColor=Color.FromArgb(40,42,48)};
            var source=new PictureBox {Dock=DockStyle.Fill,Image=SystemIcons.Information.ToBitmap(),SizeMode=PictureBoxSizeMode.CenterImage};
            bool pressed=false,started=false;Point down=Point.Empty;
            source.MouseDown+=(_,e)=>{if(e.Button==MouseButtons.Left){pressed=true;down=e.Location;File.WriteAllText(Path.Combine(directory,"external-ole-pressed.txt"),"pressed");}};
            source.MouseMove+=(_,e)=>
            {
                if(!pressed||started||e.Button!=MouseButtons.Left||Math.Abs(e.X-down.X)+Math.Abs(e.Y-down.Y)<4)return;
                started=true;
                File.WriteAllText(Path.Combine(directory,"external-ole-started.txt"),"started");
                var payload=new DataObject();payload.SetData("NestedTray.NativeFixturePayload",false,guid.ToString("D"));
                var effect=source.DoDragDrop(payload,DragDropEffects.Copy|DragDropEffects.Move);
                File.WriteAllText(Path.Combine(directory,"external-ole-result.txt"),effect.ToString());oleSource.Hide();
            };
            oleSource.Controls.Add(source);oleSource.Show();
            File.WriteAllText(Path.Combine(directory,"external-ole-window.json"),JsonSerializer.Serialize(new {Window=oleSource.Handle.ToInt64()}));
        }
        if(message.Msg==0x8000+902)
        {
            outside?.Dispose();
            var work=Screen.PrimaryScreen!.WorkingArea;
            outside=new Form {Text="NestedTray fixture outside",ShowInTaskbar=false,TopMost=true,StartPosition=FormStartPosition.Manual,Location=new Point(work.Left+40,work.Top+40),ClientSize=new Size(220,80)};
            var button=new Button {Text="Test outside click",Dock=DockStyle.Fill};
            button.Click+=(_,_)=>File.WriteAllText(Path.Combine(directory,"outside-click.log"),"clicked");
            outside.Controls.Add(button);outside.Show();
            File.WriteAllText(Path.Combine(directory,"outside.json"),JsonSerializer.Serialize(new {Window=outside.Handle.ToInt64(),Button=button.Handle.ToInt64()}));
        }
        if ((uint)message.Msg == Callback)
        {
            uint packed = unchecked((uint)message.LParam.ToInt64());
            uint eventId = packed & 0xFFFF;
            uint iconId = legacyIdentity?unchecked((uint)message.WParam.ToInt64()):packed >> 16;
            File.AppendAllText(Path.Combine(directory, "events.jsonl"), JsonSerializer.Serialize(new { Event = eventId, IconId = iconId, Anchor = message.WParam.ToInt64() }) + "\n");
            if (eventId == (legacyIdentity?0x205u:0x7Bu))
            {
                uint point = unchecked((uint)message.WParam.ToInt64());
                menu.Show(legacyIdentity?Cursor.Position:new Point(unchecked((short)(point & 0xFFFF)), unchecked((short)(point >> 16))));
                File.WriteAllText(Path.Combine(directory, "menu.json"), JsonSerializer.Serialize(new { Handle = menu.Handle.ToInt64(), menu.Bounds.X, menu.Bounds.Y }));
            }
        }
        else if (message.Msg == 0x0010) { Application.ExitThread(); return; }
        base.WndProc(ref message);
    }

    public void Dispose()
    {
        timer.Dispose(); timeout.Dispose();
        menu.Dispose();
        outside?.Dispose();
        oleSource?.Dispose();
        if (registered)
        {
            var data = MakeData(); data.Flags = legacyIdentity?0u:0x20u;
            NativeTray.Notify(2, ref data);
            registered = false;
        }
        DestroyHandle();
    }
}
