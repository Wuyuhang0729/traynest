using System.Diagnostics;
string marker=Environment.GetEnvironmentVariable("TRAYNEST_LAUNCH_FIXTURE_MARKER")??"";
if(!string.IsNullOrEmpty(marker)){File.AppendAllText(marker,Process.GetCurrentProcess().Id+Environment.NewLine);Thread.Sleep(1800);}
