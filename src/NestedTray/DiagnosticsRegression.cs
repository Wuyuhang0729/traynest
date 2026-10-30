using System.Runtime.InteropServices;
using System.Text.Json;

namespace NestedTray;
internal static class DiagnosticsRegression
{
    internal static int Run(string root)
    {
        Environment.SetEnvironmentVariable("NESTEDTRAY_TEST_PROFILE",root);Directory.CreateDirectory(root);
        var checks=new Dictionary<string,object?>();var failures=new List<string>();checks["Errors"]=failures;
        try
        {
            var store=new DiagnosticStore(root);
            Exception error=PrivateException();
            var entry=store.Record("TN3001","Error",error,new(){["PrivatePath"]="C:\\Users\\PrivatePerson\\private-document.txt",["Credential"]="Bearer deliberately-invalid-test-value",["Count"]="2"});
            checks["ErrorCodeAndUniqueEventId"]=entry.Code=="TN3001"&&entry.EventId.Length==12&&store.Record("TN3001","Error").EventId!=entry.EventId;
            checks["PersistedLogAvailable"]=store.LogAvailable&&store.ReadRecent().Any(value=>value.EventId==entry.EventId);
            string report=store.Report(new(){["Count"]=2,["FullPath"]="D:\\PrivateFolder\\private-data.json"},entry);
            checks["PrivateExceptionMessageExcluded"]=!report.Contains("PrivatePerson")&&!report.Contains("private-document")&&!report.Contains("deliberately-invalid");
            checks["ContextPathsAndCredentialsRedacted"]=!report.Contains("PrivateFolder")&&!report.Contains("Bearer");
            checks["ExceptionTypeAndMethodsAvailable"]=report.Contains("System.IO.IOException")&&report.Contains(nameof(PrivateException));
            using(var parsed=JsonDocument.Parse(report))checks["ReportSchemaAndVersion"]=parsed.RootElement.GetProperty("SchemaVersion").GetInt32()==1&&parsed.RootElement.GetProperty("Application").GetProperty("Version").GetString()!.StartsWith("0.2.0");
            string export=Path.Combine(root,"export.json");store.Export(export,new(){["Count"]=2},entry);
            checks["AtomicExportAndNoTemporaryFile"]=File.Exists(export)&&!Directory.GetFiles(root,"*.tmp").Any();
            File.AppendAllText(Path.Combine(store.LogDirectory,"events.jsonl"),"not-json\n{}\n{\"Code\":null}\n");
            checks["CorruptLogLinesIgnored"]=store.ReadRecent().Any(value=>value.EventId==entry.EventId);
            var reopened=new DiagnosticStore(root);checks["PreviousProblemSurvivesRestart"]=reopened.LastProblem?.Code=="TN3001";
            var bounded=new DiagnosticStore(Path.Combine(root,"bounded"),4096,2,12);
            for(int index=0;index<120;index++)bounded.Record("TN1101","Warning",context:new(){["Counter"]=index.ToString(),["Details"]=new string('x',200)});
            var logs=Directory.GetFiles(bounded.LogDirectory,"*.jsonl");
            checks["LogRotationAndMemoryBounded"]=logs.Length<=3&&logs.All(path=>new FileInfo(path).Length<=4096)&&bounded.ReadRecent().Length<=12;
            var parallel=new DiagnosticStore(Path.Combine(root,"parallel"),128*1024,2,100);
            Parallel.For(0,40,index=>parallel.Record("TN1101","Warning",context:new(){["Counter"]=index.ToString()}));
            checks["ConcurrentLogRecordsRemainValid"]=parallel.ReadRecent().Length==40;
            var blockedRoot=Path.Combine(root,"blocked");File.WriteAllText(blockedRoot,"file-not-directory");
            var blocked=new DiagnosticStore(blockedRoot);var blockedEntry=blocked.Record("TN5001","Error",error);
            checks["LogFailureKeepsInMemoryReport"]=!blocked.LogAvailable&&blocked.Report(problem:blockedEntry).Contains(blockedEntry.EventId);
            checks["ExportFailureIsObservable"]=Throws<IOException>(()=>store.Export(Path.Combine(root,"missing-directory","report.json")));
            CheckSettings();
            var now=DateTimeOffset.UtcNow;string restart=Path.Combine(root,"restart");
            checks["RestartLoopLimited"]=RestartThrottle.TryConsume(restart,now)&&RestartThrottle.TryConsume(restart,now)&&RestartThrottle.TryConsume(restart,now)&&!RestartThrottle.TryConsume(restart,now);
            checks["RestartBudgetRecoversAfterWindow"]=RestartThrottle.TryConsume(restart,now.AddMinutes(6));
            checks["CompatibilityRejectsUnsupportedEnvironment"]=CompatibilityProbe.Supported(true,26200,Architecture.X64,Architecture.X64)
                &&!CompatibilityProbe.Supported(true,19045,Architecture.X64,Architecture.X64)&&!CompatibilityProbe.Supported(false,26200,Architecture.X64,Architecture.X64)
                &&!CompatibilityProbe.Supported(true,26200,Architecture.Arm64,Architecture.Arm64);
            checks["IssueLinkTargetsProjectTemplate"]=DiagnosticUi.IssueUrl=="https://github.com/Wuyuhang0729/traynest/issues/new?template=bug_report.yml";
            using var close=new System.Windows.Forms.Timer{Interval=100};
            close.Tick+=(_,_)=>
            {
                var window=Application.OpenForms.Cast<Form>().FirstOrDefault(value=>value.Text.StartsWith("TrayNest · TN3001"));
                if(window==null)return;
                checks["DiagnosticWindowShowsErrorAndReport"]=window.Controls.OfType<TextBox>().Any(text=>text.Text.Contains("TN3001"))&&window.Controls.OfType<FlowLayoutPanel>().SelectMany(flow=>flow.Controls.Cast<Control>()).OfType<Button>().Count()==4;
                close.Stop();window.Close();
            };
            close.Start();DiagnosticUi.Show(entry);checks["DiagnosticWindowClosesCleanly"]=DiagnosticUi.OpenWindows==0;

            void CheckSettings()
            {
                bool corruptPreserved=true;
                foreach(string invalid in new[]{"{bad-json","null","{\"Collected\":null}","{\"Collected\":[\"\"]}"})
                {
                    File.WriteAllText(Settings.FilePath,invalid);
                    try{Settings.Load();corruptPreserved=false;}catch(TrayNestException known){corruptPreserved&=known.Code=="TN1002"&&File.ReadAllText(Settings.FilePath)==invalid;}
                }
                checks["CorruptSettingsPreservedWithStableCode"]=corruptPreserved;
                File.WriteAllText(Settings.FilePath,"{\"Collected\":[\"a\",\"a\"]}");var settings=Settings.Load();
                checks["LegacySettingsLoadAndDeduplicate"]=settings.Collected.SequenceEqual(new[]{"a"});
                settings.Collected.Add("b");settings.Save();
                checks["PreviousSettingsBackedUp"]=File.ReadAllText(Settings.FilePath+".bak").Contains("a")&&!File.ReadAllText(Settings.FilePath+".bak").Contains("b");
                string before=File.ReadAllText(Settings.FilePath);Directory.CreateDirectory(Settings.FilePath+".tmp");
                bool stableCode=false;try{settings.Save();}catch(TrayNestException known){stableCode=known.Code=="TN4001";}
                Directory.Delete(Settings.FilePath+".tmp");
                checks["SettingsWriteFailurePreservesOriginal"]=stableCode&&File.ReadAllText(Settings.FilePath)==before;
            }
        }
        catch(Exception error){failures.Add(error.ToString());}
        bool passed=failures.Count==0&&!checks.Values.Any(value=>value is false);checks["Passed"]=passed;
        File.WriteAllText(Path.Combine(root,"diagnostics-test.json"),JsonSerializer.Serialize(checks,DiagnosticStore.JsonOptions));return passed?0:1;
    }
    private static Exception PrivateException(){try{throw new IOException("C:\\Users\\PrivatePerson\\private-document.txt",new InvalidOperationException("private exception argument"));}catch(Exception error){return error;}}
    private static bool Throws<T>(Action action)where T:Exception{try{action();return false;}catch(T){return true;}}
}
