using System.Runtime.InteropServices;

namespace NestedTray;
internal sealed class DismissOptions
{
    public bool Enabled{get;set;}=true;
    public int DurationMs{get;set;}=250;
    public int DistanceDip{get;set;}=24;
    public bool Fade{get;set;}
    public string Curve{get;set;}="Windows弹出";
    public double Strength{get;set;}=2.4;
    internal int OpeningFadeDelayMs=83;
    internal DismissOptions Copy()=>(DismissOptions)MemberwiseClone();
    internal void Validate(){DurationMs=Math.Clamp(DurationMs,0,600);DistanceDip=Math.Clamp(DistanceDip,0,160);Strength=Math.Clamp(Strength,.5,6);}
    internal bool WindowsPopup=>Curve=="Windows弹出";
    internal int ClosingDuration=>WindowsPopup&&DurationMs>0?215:DurationMs;
    internal double Progress(double t)=>Curve switch{"Windows弹出"=>CubicBezier(t,.1,.9,.2,1),"线性"=>t,"减速下沉"=>1-Math.Pow(1-t,Strength),"平滑"=>t*t*(3-2*t),_=>Math.Pow(t,Strength)};
    internal static double CubicBezier(double time,double x1,double y1,double x2,double y2)
    {
        if(time<=0)return 0;if(time>=1)return 1;
        double low=0,high=1,u=time;
        for(int i=0;i<20;i++){double x=3*(1-u)*(1-u)*u*x1+3*(1-u)*u*u*x2+u*u*u;if(x<time)low=u;else high=u;u=(low+high)*.5;}
        return 3*(1-u)*(1-u)*u*y1+3*(1-u)*u*u*y2+u*u*u;
    }
}
internal static class SystemAnimation
{
    [DllImport("user32.dll")]private static extern bool SystemParametersInfoW(uint action,uint parameter,out int value,uint flags);
    internal static bool? Read(uint action)=>SystemParametersInfoW(action,0,out int value,0)?value!=0:null;
    internal static bool Enabled=>Read(0x1042)!=false;
}
