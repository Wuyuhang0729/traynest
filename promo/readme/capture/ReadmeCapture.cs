using NestedTray;
using System.Reflection;
using System.Drawing.Imaging;
internal static class ReadmeCapture
{
    [STAThread] static int Main(string[] args)
    {
        string root=Path.GetFullPath(args[0]),source=Path.GetFullPath(args[1]);Directory.CreateDirectory(root);
        Environment.SetEnvironmentVariable("NESTEDTRAY_TEST_PROFILE",Path.Combine(root,"profile"));
        Application.SetHighDpiMode(HighDpiMode.DpiUnaware);Application.EnableVisualStyles();
        var ids=args.Length>2?args[2].Split(','):new[]{"qbittorrent","sharex","localsend","keepassxc","obs","bitwarden","syncthing","autohotkey","rustdesk","tailscale"};
        var records=ids.Select(id=>new IconRecord{Key=id,Rule="demo|"+id,Tooltip=id,Selected=true,RegistrationKnown=true,Image=new Bitmap(Path.Combine(source,id+"-normal.png"))}).ToArray();
        try
        {
            using var panel=new TrayPanel{UseSavedOrder=true,FixedColumns=5,IconPixels=64,CellPixels=160};
            typeof(TrayPanel).GetField("surfacePadding",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(panel,16);
            void Save(string name){panel.Render(records);using var bitmap=new Bitmap(panel.Width,panel.Height);panel.DrawToBitmap(bitmap,new Rectangle(Point.Empty,panel.Size));bitmap.Save(Path.Combine(root,name+".png"),ImageFormat.Png);}
            panel.Render(records);panel.Location=new Point(-20000,-20000);panel.Show();Application.DoEvents();Save("panel");
            var grid=panel.Controls.OfType<FlowLayoutPanel>().Single();var tile=grid.Controls[2];
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            tile.GetType().GetMethod("OnMouseEnter",flags)!.Invoke(tile,[EventArgs.Empty]);Save("hover");
            tile.GetType().GetField("Reordering",flags)!.SetValue(tile,true);Save("reorder");tile.GetType().GetField("Reordering",flags)!.SetValue(tile,false);
            foreach(var record in records.Skip(2).Take(2))record.IsOffline=true;Save("offline");
            panel.ToolStatus=_=>new ToolLaunchStatus(ToolLaunchState.Waiting,"正在启动，等待图标回到原位。");Save("waiting");
            File.WriteAllText(Path.Combine(root,"provenance.txt"),"Current production TrayPanel compiled from src/NestedTray; 4x logical size (16px icon / 40px cell), 5 columns, public icon fixtures from existing video source manifest. No user desktop, settings, application titles or real tray takeover. Reorder highlight and offline/waiting statuses are demonstration states.");
        }
        finally{foreach(var record in records)record.Dispose();}
        return 0;
    }
}
