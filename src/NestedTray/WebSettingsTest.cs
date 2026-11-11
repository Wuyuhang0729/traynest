using System.Diagnostics;
using System.Text.Json;
namespace NestedTray;
// Test driver for the production WebView DOM. Changes click the same controls as users.
internal static class WebSettingsTest
{
    internal static void Pump(int ms){var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(10);}}
    internal static void Wait(Func<bool> ready,int timeout=15000){var clock=Stopwatch.StartNew();while(!ready()&&clock.ElapsedMilliseconds<timeout)Pump(20);if(!ready())throw new TimeoutException("Web settings did not become ready");}
    internal static void Ready(SettingsWindow window){Wait(()=>window.IsReady||window.InitializationError!=null);if(!window.IsReady)throw new InvalidOperationException(window.InitializationError);}
    internal static T Script<T>(SettingsWindow window,string code){Ready(window);var task=window.Browser.ExecuteScriptAsync(code);Wait(()=>task.IsCompleted);return JsonSerializer.Deserialize<T>(task.GetAwaiter().GetResult())!;}
    internal static void Page(SettingsWindow window,int index){Ready(window);window.SelectPage(index);Wait(()=>Script<int>(window,"page")==index);}
    internal static int Value(SettingsWindow window,string id)=>Script<int>(window,$"Number(document.getElementById({JsonSerializer.Serialize(id)}).value)");
    internal static bool Checked(SettingsWindow window,string id)=>Script<bool>(window,$"document.getElementById({JsonSerializer.Serialize(id)}).checked");
    internal static bool Exists(SettingsWindow window,string id)=>Script<bool>(window,$"!!document.getElementById({JsonSerializer.Serialize(id)})");
    internal static bool Visible(SettingsWindow window,string id)=>Script<bool>(window,$"!!document.getElementById({JsonSerializer.Serialize(id)})?.getClientRects().length");
    internal static bool Enabled(SettingsWindow window,string id)=>Script<bool>(window,"!document.getElementById("+JsonSerializer.Serialize(id)+").closest('.picker').querySelector('.picker-trigger').disabled");
    internal static void Set(SettingsWindow window,string id,object value)
    {
        string field=JsonSerializer.Serialize(id),json=JsonSerializer.Serialize(value);
        Script<bool>(window,"(()=>{const c=document.getElementById("+field+");if(c.type==='checkbox'){if(c.checked!=="+json+")c.click()}else{const p=c.closest('.picker');p.querySelector('.picker-trigger').click();[...p.querySelectorAll('[data-value]')].find(b=>Number(b.dataset.value)==="+json+").click()}return true})()");
        Wait(()=>Script<bool>(window,"!busy"));Pump(40);
    }
    internal static void Capture(SettingsWindow window,string path){Ready(window);using var stream=File.Create(path);var task=window.Browser.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png,stream);Wait(()=>task.IsCompleted);task.GetAwaiter().GetResult();}
}
