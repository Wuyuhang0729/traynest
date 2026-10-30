using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace NestedTray;
internal static class ExternalOleRegression
{
    internal static int Run(string fixtureExe)
    {
        Directory.CreateDirectory(Settings.DirectoryPath);
        string report=Path.Combine(Settings.DirectoryPath,"ole-live-test.json");
        var result=new Dictionary<string,object?> { ["Scope"]="Cross-process fixture OLE with production UI Application.Run; fixture-only mapping, no discovery broadcast or startup writes.",["Errors"]=new List<string>() };
        var errors=(List<string>)result["Errors"]!;
        using var timeout=new System.Threading.Timer(_=>Process.GetCurrentProcess().Kill(),null,25000,Timeout.Infinite);
        using var app=new TrayApplication(true);
        string directory=Path.Combine(Settings.DirectoryPath,"fixture");
        using var fixture=Process.Start(new ProcessStartInfo(Path.GetFullPath(fixtureExe)){UseShellExecute=false,CreateNoWindow=true,ArgumentList={directory,"NestedTray OLE fixture"}})!;
        app.Bridge.AllowedOwners!.Add(fixture.Id);
        using var ready=new System.Windows.Forms.Timer{Interval=80};
        int phase=0;IconRecord? record=null;
        Thread? driver=null;
        ready.Tick+=(_,_)=>
        {
            try
            {
                if(phase==0)
                {
                    record=app.Bridge.Icons.Values.FirstOrDefault(value=>value.ProcessId==fixture.Id&&value.Callback!=0);
                    if(record==null)return;
                    var anchor=app.Entry.Bounds;
                    if(anchor.IsEmpty){var work=Screen.PrimaryScreen!.WorkingArea;anchor=new Rectangle(work.Right-80,work.Bottom-16,16,16);}
                    app.Panel.EntryRectangle=anchor;app.Panel.Open();
                    uint origin=(40u<<16)|40u;NativeTray.PostMessage(record.Window,0x8000+903,origin,0);phase=1;
                }
                else if(phase==1&&!app.Panel.IsOpening&&File.Exists(Path.Combine(directory,"external-ole-window.json")))
                {
                    ready.Stop();
                    nint source=(nint)JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(directory,"external-ole-window.json"))).GetProperty("Window").GetInt64();
                    app.RegisterTestDragSource(source,record!);Native.SetWindowPos(source,new nint(-1),0,0,0,0,0x13);
                    app.Panel.Open();
                    var work=Screen.PrimaryScreen!.WorkingArea;app.Panel.Location=new Point(work.Left+40,work.Top+200);
                    result["PanelVisibleBeforeSourcePress"]=app.Panel.Visible;
                    Native.GetWindowRect(app.Panel.Handle,out var panelBefore);result["PanelNativeBoundsBefore"]=Native.Rect(panelBefore).ToString();
                    Native.GetWindowRect(source,out var fromRect);
                    var empty=app.Panel.Controls.OfType<Label>().Single();var target=empty.RectangleToScreen(empty.ClientRectangle);
                    var from=new Point((fromRect.Left+fromRect.Right)/2,(fromRect.Top+fromRect.Bottom)/2);var to=new Point(target.Left+target.Width/2,target.Top+target.Height/2);
                    driver=new Thread(()=>
                    {
                        Native.GetCursorPos(out var previous);
                        try
                        {
                            Native.SetCursorPos(from.X,from.Y);Thread.Sleep(100);Button(2);Thread.Sleep(100);
                            for(int step=1;step<=12;step++){Native.SetCursorPos(from.X+(to.X-from.X)*step/12,from.Y+(to.Y-from.Y)*step/12);Thread.Sleep(45);}
                            Thread.Sleep(250);
                            app.Panel.Invoke((Action)(()=>
                            {
                                bool core=app.Panel.CoreDrop.Enters>0;
                                var effect=core?(DragDropEffects)app.Panel.CoreDrop.LastEffect:app.Panel.LastExternalEffect;
                                var allowed=core?(DragDropEffects)app.Panel.CoreDrop.LastAllowed:app.Panel.LastExternalAllowed;
                                result["PanelVisibleAtHover"]=app.Panel.Visible;
                                Native.GetWindowRect(app.Panel.Handle,out var panelNow);result["PanelNativeBoundsAtHover"]=Native.Rect(panelNow).ToString();
                                result["PanelNativeVisible"]=Native.IsWindowVisible(app.Panel.Handle);result["PanelNativeEnabled"]=Native.IsWindowEnabled(app.Panel.Handle);result["PanelStyles"]=Native.GetWindowLongPtrW(app.Panel.Handle,-16).ToInt64().ToString("X");result["PanelExStyles"]=Native.GetWindowLongPtrW(app.Panel.Handle,-20).ToInt64().ToString("X");
                                result["CoreRegistration"]=app.Panel.CoreDrop.Status;result["CoreTargetRequests"]=app.Panel.CoreDrop.Requests;result["CoreEnters"]=app.Panel.CoreDrop.Enters;result["CoreEffect"]=app.Panel.CoreDrop.LastEffect;
                                result["ExternalEnterObserved"]=app.Panel.ExternalDragEnters+app.Panel.CoreDrop.Enters>0;
                                result["ReceiverTransport"]=core?"Windows CoreDragDropManager":"Classic OLE";
                                result["SourceGestureRecognized"]=app.HasDragCandidate;
                                result["PanelBounds"]=app.Panel.Bounds.ToString();result["SourceBounds"]=Native.Rect(fromRect).ToString();
                                nint hit=Native.GetAncestor(Native.WindowFromPoint(new Native.Point{X=to.X,Y=to.Y}),2);Native.GetWindowThreadProcessId(hit,out uint hitPid);
                                var cls=new System.Text.StringBuilder(256);Native.GetClassNameW(hit,cls,256);Native.GetWindowRect(hit,out var hitRect);Native.GetCursorPos(out var actual);
                                result["HoverReallyHitsPanel"]=hit==app.Panel.Handle;result["HoverProcess"]=hitPid;result["ProductProcess"]=Environment.ProcessId;result["FixtureProcess"]=fixture.Id;
                                result["HoverClass"]=cls.ToString();result["HoverBounds"]=Native.Rect(hitRect).ToString();result["ActualPointer"]=new Point(actual.X,actual.Y).ToString();result["RequestedPointer"]=to.ToString();
                                result["ExternalHoverAccepted"]=effect!=DragDropEffects.None;
                                result["AcceptedEffect"]=effect.ToString();result["AllowedEffects"]=allowed.ToString();result["Formats"]=app.Panel.LastExternalFormats;
                                result["IconPixels"]=app.Panel.IconPixels;result["CellPixels"]=app.Panel.CellPixels;
                                result["SurfaceSize"]=app.Panel.ClientSize.ToString();
                            }));
                            Button(4);Thread.Sleep(600);
                            var wait=Stopwatch.StartNew();while(!File.Exists(Path.Combine(directory,"external-ole-result.txt"))&&wait.ElapsedMilliseconds<3000)Thread.Sleep(50);
                            result["SourceReportedSuccess"]=File.Exists(Path.Combine(directory,"external-ole-result.txt"))&&File.ReadAllText(Path.Combine(directory,"external-ole-result.txt")) is "Copy" or "Move";
                            app.Panel.Invoke((Action)(()=>
                            {
                                result["ExternalDropDelivered"]=app.Panel.ExternalDrops+app.Panel.CoreDrop.Drops>0;result["CollectedAfterOleCompletes"]=record!.Selected;
                                result["CoreDrops"]=app.Panel.CoreDrop.Drops;result["CoreOvers"]=app.Panel.CoreDrop.Overs;
                                var tile=app.Panel.Controls.OfType<FlowLayoutPanel>().Single().Controls.OfType<Button>().SingleOrDefault();
                                result["RenderedIconIsNativeSize"]=tile?.Image?.Width==app.Panel.IconPixels&&tile?.Image?.Height==app.Panel.IconPixels;
                            }));
                        }
                        catch(Exception error){errors.Add(error.ToString());}
                        finally
                        {
                            Button(4);Native.SetCursorPos(previous.X,previous.Y);
                            File.WriteAllText(report,JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
                            app.Panel.BeginInvoke((Action)(()=>{app.Entry.DiagnosticsRequested?.Invoke();app.Dispose();}));
                        }
                    }){IsBackground=true,Name="External OLE automation"};
                    driver.SetApartmentState(ApartmentState.STA);driver.Start();
                }
            }
            catch(Exception error){errors.Add(error.ToString());File.WriteAllText(report,JsonSerializer.Serialize(result));app.Dispose();}
        };
        ready.Start();Application.Run(app);driver?.Join(1000);
        if(record!=null)NativeTray.PostMessage(record.Window,0x10,0,0);
        if(!fixture.WaitForExit(2000)){fixture.Kill();fixture.WaitForExit(1000);}
        result["FixtureExited"]=fixture.HasExited;
        File.WriteAllText(report,JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
        return errors.Count==0&&!result.Values.Any(value=>value is false)?0:1;
    }
    private static void Button(uint flags)
    {var input=new Native.Input{Type=0,Mouse=new Native.MouseInput{Flags=flags}};if(Native.SendInput(1,[input],Marshal.SizeOf<Native.Input>())!=1)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());}
}
