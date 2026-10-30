using System.Runtime.InteropServices;

namespace NestedTray;
internal sealed class DismissOptions
{
    public bool Enabled{get;set;}=true;
    public int DurationMs{get;set;}=160;
    public int DistanceDip{get;set;}=24;
    public bool Fade{get;set;}=false;
    public string Curve{get;set;}="加速下沉";
    public double Strength{get;set;}=2.4;
    internal DismissOptions Copy()=>(DismissOptions)MemberwiseClone();
    internal void Validate(){DurationMs=Math.Clamp(DurationMs,0,600);DistanceDip=Math.Clamp(DistanceDip,0,160);Strength=Math.Clamp(Strength,.5,6);}
    internal double Progress(double t)=>Curve switch{"线性"=>t,"减速下沉"=>1-Math.Pow(1-t,Strength),"平滑"=>t*t*(3-2*t),_=>Math.Pow(t,Strength)};
}
internal static class SystemAnimation
{
    [DllImport("user32.dll")]private static extern bool SystemParametersInfoW(uint action,uint parameter,out int value,uint flags);
    internal static bool? Read(uint action)=>SystemParametersInfoW(action,0,out int value,0)?value!=0:null;
    internal static bool Enabled=>Read(0x1042)!=false;
}
