using System.Text.Json;
using System.Xml.Linq;

namespace NestedTray;
internal static class StartupRegression
{
    internal static int Run(string directory)
    {
        Directory.CreateDirectory(directory);var checks=new Dictionary<string,bool>();var errors=new List<string>();
        try
        {
            const string sid="S-1-5-21-123-456-789-1001";
            string executable=Path.Combine(directory,"中文 & spaces","NestedTray.exe"),xml=StartupRegistration.Xml(executable,sid);
            XNamespace ns="http://schemas.microsoft.com/windows/2004/02/mit/task";var doc=XDocument.Parse(xml);
            checks["QuotedUnicodeAndXmlCharactersRoundTrip"]=StartupRegistration.Matches(xml,executable,sid);
            checks["DifferentExecutableRejected"]=!StartupRegistration.Matches(xml,executable+".other",sid);
            checks["DifferentUserRejected"]=!StartupRegistration.Matches(xml,executable,sid+"1");
            checks["InvalidXmlRejected"]=!StartupRegistration.Matches("<invalid",executable,sid);
            checks["LogonDelayTenSeconds"]=doc.Descendants(ns+"Delay").Single().Value=="PT10S";
            checks["WorksOnBattery"]=doc.Descendants(ns+"DisallowStartIfOnBatteries").Single().Value=="false"&&doc.Descendants(ns+"StopIfGoingOnBatteries").Single().Value=="false";
            checks["NoNetworkRequirement"]=doc.Descendants(ns+"RunOnlyIfNetworkAvailable").Single().Value=="false";
            checks["NoThreeDayTermination"]=doc.Descendants(ns+"ExecutionTimeLimit").Single().Value=="PT0S";
            checks["NoDuplicateScheduledInstances"]=doc.Descendants(ns+"MultipleInstancesPolicy").Single().Value=="IgnoreNew";
            checks["DisabledTaskIsNotEnabled"]=!StartupRegistration.Matches(Change("Enabled","false",true),executable,sid);
            checks["DisabledTriggerIsNotEnabled"]=!StartupRegistration.Matches(Change("Enabled","false",false),executable,sid);
            checks["NoninteractiveTaskRejected"]=!StartupRegistration.Matches(Change("LogonType","S4U"),executable,sid);
            checks["ElevatedTaskRejected"]=!StartupRegistration.Matches(Change("RunLevel","HighestAvailable"),executable,sid);
            checks["WrongArgumentsRejected"]=!StartupRegistration.Matches(Change("Arguments","--recover-after-exit 123"),executable,sid);
            var normalized=XDocument.Parse(xml);
            normalized.Descendants(ns+"Enabled").Remove();normalized.Descendants(ns+"RunLevel").Remove();
            checks["SchedulerOmittedDefaultValuesAccepted"]=StartupRegistration.Matches(normalized.ToString(),executable,sid);
            var identity=System.Security.Principal.WindowsIdentity.GetCurrent();string actualSid=identity.User!.Value;
            var accountXml=XDocument.Parse(StartupRegistration.Xml(executable,actualSid));
            accountXml.Descendants(ns+"LogonTrigger").Single().Element(ns+"UserId")!.Value=identity.Name;
            checks["SchedulerAccountNameResolvesToSameUser"]=StartupRegistration.Matches(accountXml.ToString(),executable,actualSid);
            checks["ActuallyMissingScheduledTaskReturnsAbsent"]=StartupRegistration.ReadTask("TrayNest-absence-probe-"+Guid.NewGuid().ToString("N"))==null;
            long now=0;int sleeps=0;
            checks["SlowShellAfterTwentySecondsStillStarts"]=CompatibilityProbe.WaitForTray(()=>now>=60000,()=>now,ms=>{now+=ms;sleeps++;},120000)&&now==60000&&sleeps==300;
            now=0;
            checks["MissingShellTimesOutWithoutBusyLoop"]=!CompatibilityProbe.WaitForTray(()=>false,()=>now,ms=>now+=ms,120000)&&now==120000;
            now=0;sleeps=0;
            checks["ReadyShellDoesNotDelay"]=CompatibilityProbe.WaitForTray(()=>true,()=>now,ms=>sleeps++,120000)&&sleeps==0;
            string Change(string name,string value,bool last=false){var copy=XDocument.Parse(xml);(last?copy.Descendants(ns+name).Last():copy.Descendants(ns+name).First()).Value=value;return copy.ToString();}
        }
        catch(Exception error){errors.Add(error.ToString());}
        bool passed=checks.Values.All(v=>v)&&errors.Count==0;
        File.WriteAllText(Path.Combine(directory,"startup-test.json"),JsonSerializer.Serialize(new{Passed=passed,Checks=checks,Errors=errors,Scope="Production task XML and shell wait policy; no actual login, real startup writes, Explorer restart or mouse input"},DiagnosticStore.JsonOptions));
        return passed?0:1;
    }
}
