using NestedTray;
using System.Reflection;
using System.Drawing.Imaging;
using System.Text.Json;

internal static class ReadmeCaptureV2
{
    [STAThread]static int Main(string[] args)
    {
        string root=Path.GetFullPath(args[0]),icons=Path.GetFullPath(args[1]);Directory.CreateDirectory(root);
        Environment.SetEnvironmentVariable("NESTEDTRAY_TEST_PROFILE",Path.Combine(root,"profile"));
        Application.SetHighDpiMode(HighDpiMode.DpiUnaware);Application.EnableVisualStyles();
        string[] ids=["baidupan","xunlei","snipaste","everything","tencent-meeting","netease-music","qqmusic","obs","sunlogin","wecom"];
        var records=ids.Select(id=>new IconRecord{Key=id,Rule="demo|"+id,Tooltip=id,Selected=true,RegistrationKnown=true,Image=new Bitmap(Path.Combine(icons,id+"-normal.png"))}).ToArray();
        try
        {
            using var panel=new TrayPanel{UseSavedOrder=true,FixedColumns=5,IconPixels=64,CellPixels=160};
            typeof(TrayPanel).GetField("surfacePadding",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(panel,16);
            panel.SetAppearance(PanelAppearance.Light);panel.Location=new Point(-20000,-20000);panel.Show();Application.DoEvents();
            void Save(string name){panel.Render(records);using var bitmap=new Bitmap(panel.Width,panel.Height);panel.DrawToBitmap(bitmap,new Rectangle(Point.Empty,panel.Size));bitmap.Save(Path.Combine(root,name+".png"),ImageFormat.Png);}
            Save("panel-light");panel.SetAppearance(PanelAppearance.Dark);Save("panel-dark");panel.SetAppearance(PanelAppearance.Light);
            var grid=panel.Controls.OfType<FlowLayoutPanel>().Single();var tile=grid.Controls[2];var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            tile.GetType().GetField("Reordering",flags)!.SetValue(tile,true);Save("panel-reorder");tile.GetType().GetField("Reordering",flags)!.SetValue(tile,false);
            records[0].IsOffline=true;records[6].IsOffline=true;Save("panel-offline");
            var settings=new Settings{Appearance=PanelAppearance.Acrylic,MatchPanelMaterials=true,HoverToOpen=true};
            using var window=new SettingsWindow(settings,()=>{},()=>false,_=>{});window.StartPosition=FormStartPosition.Manual;window.Location=new Point(-20000,-20000);window.Show();
            foreach(int page in new[]{0,1}){window.SelectPage(page);Application.DoEvents();using var bitmap=new Bitmap(window.Width,window.Height);window.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(root,page==0?"settings-general.png":"settings-panel.png"),ImageFormat.Png);}
            File.WriteAllText(Path.Combine(root,"provenance.json"),JsonSerializer.Serialize(new{Version="0.2.0-preview.12",Renderer="Current production TrayPanel and SettingsWindow compiled from src/NestedTray",PanelLogicalScale=4,Ids=ids,NoUserDesktop=true,NoRealTrayConnection=true,DemoStates=new[]{"reorder highlight","two offline slots","material sync shown enabled without connecting native Explorer"}},new JsonSerializerOptions{WriteIndented=true}));
        }
        finally{foreach(var record in records)record.Dispose();}
        return 0;
    }
}

