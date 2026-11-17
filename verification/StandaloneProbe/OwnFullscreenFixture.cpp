#ifndef UNICODE
#define UNICODE
#endif
#ifndef _UNICODE
#define _UNICODE
#endif
#include <windows.h>
#include <shellapi.h>
#include <d3d11.h>
#include <dxgi.h>
#include <filesystem>
#include <fstream>
#include <vector>

static bool cancelled=false;
static std::ofstream timeline;
static IDXGISwapChain* activeChain=nullptr;
static int trayClicks=0,exclusiveClicks=0;
struct MonitorState {HMONITOR handle;MONITORINFOEXW info;DEVMODEW mode;};
static std::vector<MonitorState> readMonitors(){
    std::vector<MonitorState> result;
    bool complete=EnumDisplayMonitors(nullptr,nullptr,[](HMONITOR handle,HDC,LPRECT,LPARAM context)->BOOL{
        MonitorState state{};state.handle=handle;state.info.cbSize=sizeof(state.info);state.mode.dmSize=sizeof(state.mode);
        if(!GetMonitorInfoW(handle,reinterpret_cast<MONITORINFO*>(&state.info))||!EnumDisplaySettingsExW(state.info.szDevice,ENUM_CURRENT_SETTINGS,&state.mode,0))return FALSE;
        reinterpret_cast<std::vector<MonitorState>*>(context)->push_back(state);return TRUE;
    },reinterpret_cast<LPARAM>(&result));
    if(!complete)result.clear();
    return result;
}
static UINT windowDpi(HWND window){using Read=UINT(WINAPI*)(HWND);auto read=reinterpret_cast<Read>(GetProcAddress(GetModuleHandleW(L"user32.dll"),"GetDpiForWindow"));return read?read(window):0;}
static void record(const char* phase,HRESULT hr=S_OK,bool actual=false){
    SYSTEMTIME time{};GetSystemTime(&time);
    timeline<<"{\"Utc\":\""<<time.wYear<<"-";
    timeline.fill('0');timeline.width(2);timeline<<time.wMonth<<"-";timeline.width(2);timeline<<time.wDay<<"T";
    timeline.width(2);timeline<<time.wHour<<":";timeline.width(2);timeline<<time.wMinute<<":";timeline.width(2);timeline<<time.wSecond<<"Z\",";
    timeline<<"\"UptimeMs\":"<<GetTickCount64()<<",\"Phase\":\""<<phase<<"\",\"HResult\":"<<(long)hr<<",\"ActualExclusive\":"<<(actual?"true":"false")<<"}"<<std::endl;
}
static LRESULT CALLBACK windowProc(HWND w,UINT m,WPARAM wp,LPARAM lp){
    if(m==WM_APP+73&&LOWORD(lp)==NIN_SELECT){BOOL exclusive=FALSE;if(activeChain)activeChain->GetFullscreenState(&exclusive,nullptr);trayClicks++;if(exclusive)exclusiveClicks++;record("TrayClickReceived",S_OK,exclusive);return 0;}
    if(m==WM_CLOSE||(m==WM_KEYDOWN&&wp==VK_ESCAPE)){cancelled=true;return 0;}
    if(m==WM_DISPLAYCHANGE)record("DisplayChange");
    if(m==WM_DPICHANGED)record("DpiChange");
    return DefWindowProcW(w,m,wp,lp);
}
static void renderFor(unsigned milliseconds,IDXGISwapChain* chain,ID3D11DeviceContext* context,ID3D11RenderTargetView* target){
    auto start=GetTickCount64();MSG msg{};
    while(!cancelled&&GetTickCount64()-start<milliseconds){
        while(PeekMessageW(&msg,nullptr,0,0,PM_REMOVE)){if(msg.message==WM_QUIT){cancelled=true;break;}TranslateMessage(&msg);DispatchMessageW(&msg);}
        float color[]{0.08f,0.15f,0.23f,1.0f};context->ClearRenderTargetView(target,color);
        HRESULT hr=chain->Present(1,0);if(FAILED(hr)){record("PresentFailed",hr);cancelled=true;}
        if(hr==DXGI_STATUS_OCCLUDED)Sleep(20);
    }
}
int WINAPI wWinMain(HINSTANCE instance,HINSTANCE,LPWSTR,int){
    int argc=0;auto argv=CommandLineToArgvW(GetCommandLineW(),&argc);
    if(argc<2){if(argv)LocalFree(argv);return 2;}
    bool lifecycle=false;int requestedMonitor=-1;
    for(int i=2;i<argc;i++){
        if(wcscmp(argv[i],L"--lifecycle")==0)lifecycle=true;
        else if(wcscmp(argv[i],L"--monitor")==0&&i+1<argc){wchar_t* end=nullptr;long value=wcstol(argv[++i],&end,10);if(!end||end==argv[i]||*end||value<0||value>15){LocalFree(argv);return 2;}requestedMonitor=static_cast<int>(value);}
        else{LocalFree(argv);return 2;}
    }
    std::filesystem::path output(argv[1]);LocalFree(argv);std::filesystem::create_directories(output);
    timeline.open(output/L"fixture.private.jsonl",std::ios::out);record("FixtureStarted");
    using SetDpi=BOOL(WINAPI*)(DPI_AWARENESS_CONTEXT);
    if(auto setDpi=reinterpret_cast<SetDpi>(GetProcAddress(GetModuleHandleW(L"user32.dll"),"SetProcessDpiAwarenessContext")))setDpi(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
    auto monitors=readMonitors();int primary=-1;
    for(size_t i=0;i<monitors.size();i++)if(monitors[i].info.dwFlags&MONITORINFOF_PRIMARY)primary=static_cast<int>(i);
    int targetIndex=requestedMonitor>=0?requestedMonitor:primary;
    if(primary<0||targetIndex<0||static_cast<size_t>(targetIndex)>=monitors.size()){record("RequestedMonitorUnavailable",E_FAIL);return 3;}
    auto& selected=monitors[targetIndex];auto& monitor=selected.info;auto& before=selected.mode;
    WNDCLASSW cls{};cls.hInstance=instance;cls.lpfnWndProc=windowProc;cls.lpszClassName=L"TrayNestOwnFullscreenFixture";cls.hCursor=LoadCursorW(nullptr,IDC_ARROW);
    if(!RegisterClassW(&cls)){record("RegisterWindowFailed",HRESULT_FROM_WIN32(GetLastError()));return 3;}
    HWND window=CreateWindowExW(0,cls.lpszClassName,L"TrayNest own fullscreen test - Esc cancels",WS_OVERLAPPEDWINDOW,monitor.rcMonitor.left+100,monitor.rcMonitor.top+100,640,400,nullptr,nullptr,instance,nullptr);
    if(!window)return 3;
    bool windowOnTarget=MonitorFromWindow(window,MONITOR_DEFAULTTONEAREST)==selected.handle;UINT startingDpi=windowDpi(window);
    IDXGIFactory* factory=nullptr;IDXGIAdapter* adapter=nullptr;IDXGIOutput* targetOutput=nullptr;
    HRESULT enumeration=CreateDXGIFactory(__uuidof(IDXGIFactory),reinterpret_cast<void**>(&factory));
    if(SUCCEEDED(enumeration)){
        for(UINT a=0;!adapter;a++){
            IDXGIAdapter* candidate=nullptr;if(FAILED(factory->EnumAdapters(a,&candidate)))break;
            for(UINT o=0;;o++){
                IDXGIOutput* outputCandidate=nullptr;if(FAILED(candidate->EnumOutputs(o,&outputCandidate)))break;
                DXGI_OUTPUT_DESC outputDesc{};if(SUCCEEDED(outputCandidate->GetDesc(&outputDesc))&&outputDesc.Monitor==selected.handle){adapter=candidate;targetOutput=outputCandidate;break;}
                outputCandidate->Release();
            }
            if(!adapter)candidate->Release();
        }
        factory->Release();
    }
    if(!adapter||!targetOutput){record("TargetDxgiOutputUnavailable",E_FAIL);DestroyWindow(window);UnregisterClassW(cls.lpszClassName,instance);return 4;}
    DXGI_SWAP_CHAIN_DESC desc{};desc.BufferDesc.Width=before.dmPelsWidth;desc.BufferDesc.Height=before.dmPelsHeight;
    desc.BufferDesc.Format=DXGI_FORMAT_R8G8B8A8_UNORM;desc.SampleDesc.Count=1;desc.BufferUsage=DXGI_USAGE_RENDER_TARGET_OUTPUT;
    desc.BufferCount=1;desc.OutputWindow=window;desc.Windowed=TRUE;desc.SwapEffect=DXGI_SWAP_EFFECT_DISCARD;
    ID3D11Device* device=nullptr;ID3D11DeviceContext* context=nullptr;IDXGISwapChain* chain=nullptr;
    HRESULT create=D3D11CreateDeviceAndSwapChain(adapter,D3D_DRIVER_TYPE_UNKNOWN,nullptr,0,nullptr,0,D3D11_SDK_VERSION,&desc,&chain,&device,nullptr,&context);
    record("DeviceCreated",create);
    if(FAILED(create)){targetOutput->Release();adapter->Release();DestroyWindow(window);UnregisterClassW(cls.lpszClassName,instance);return 4;}
    ID3D11Texture2D* buffer=nullptr;ID3D11RenderTargetView* target=nullptr;
    HRESULT hr=chain->GetBuffer(0,__uuidof(ID3D11Texture2D),reinterpret_cast<void**>(&buffer));
    if(SUCCEEDED(hr))hr=device->CreateRenderTargetView(buffer,nullptr,&target);
    if(buffer)buffer->Release();
    if(FAILED(hr)){record("RenderTargetFailed",hr);chain->Release();context->Release();device->Release();targetOutput->Release();adapter->Release();DestroyWindow(window);return 4;}
    activeChain=chain;
    NOTIFYICONDATAW tray{};tray.cbSize=sizeof(tray);tray.hWnd=window;tray.uID=701;tray.uFlags=NIF_MESSAGE|NIF_ICON|NIF_TIP|NIF_GUID;
    tray.uCallbackMessage=WM_APP+73;tray.hIcon=LoadIconW(nullptr,IDI_APPLICATION);CoCreateGuid(&tray.guidItem);wcscpy_s(tray.szTip,L"TrayNest own fullscreen source");
    auto addTray=[&](const char* phase){BOOL added=Shell_NotifyIconW(NIM_ADD,&tray);tray.uVersion=NOTIFYICON_VERSION_4;BOOL version=added&&Shell_NotifyIconW(NIM_SETVERSION,&tray);record(phase,added&&version?S_OK:E_FAIL);return added&&version;};
    if(lifecycle){
        if(!addTray("SourceInitiallyRegistered"))cancelled=true;
        auto gate=GetTickCount64();
        while(!cancelled&&!std::filesystem::exists(output/L"collected.ready")&&GetTickCount64()-gate<7000)renderFor(20,chain,context,target);
        if(!std::filesystem::exists(output/L"collected.ready")){record("CollectionGateTimedOut",E_FAIL);cancelled=true;}
    }
    ShowWindow(window,SW_SHOW);SetForegroundWindow(window);record("Windowed");renderFor(2000,chain,context,target);
    if(!cancelled){
        SetWindowLongPtrW(window,GWL_STYLE,WS_POPUP|WS_VISIBLE);
        SetWindowPos(window,HWND_TOPMOST,monitor.rcMonitor.left,monitor.rcMonitor.top,monitor.rcMonitor.right-monitor.rcMonitor.left,monitor.rcMonitor.bottom-monitor.rcMonitor.top,SWP_FRAMECHANGED|SWP_SHOWWINDOW);
        SetForegroundWindow(window);record("BorderlessFullscreen");renderFor(lifecycle?900:3000,chain,context,target);
        if(lifecycle&&!cancelled){record("BorderlessSourceDeleted",Shell_NotifyIconW(NIM_DELETE,&tray)?S_OK:E_FAIL);renderFor(700,chain,context,target);addTray("BorderlessSourceReadded");renderFor(1400,chain,context,target);}
    }
    HRESULT enter=E_ABORT,leave=S_OK;BOOL exclusive=FALSE;bool exclusiveOnTarget=false;
    if(!cancelled){
        enter=chain->SetFullscreenState(TRUE,targetOutput);IDXGIOutput* activeOutput=nullptr;chain->GetFullscreenState(&exclusive,&activeOutput);
        if(activeOutput){DXGI_OUTPUT_DESC actual{};exclusiveOnTarget=exclusive&&SUCCEEDED(activeOutput->GetDesc(&actual))&&actual.Monitor==selected.handle;activeOutput->Release();}
        record("ExclusiveRequested",enter,exclusive);
        renderFor(lifecycle?1000:3000,chain,context,target);
        if(lifecycle&&!cancelled){record("ExclusiveSourceDeleted",Shell_NotifyIconW(NIM_DELETE,&tray)?S_OK:E_FAIL,exclusive);renderFor(700,chain,context,target);addTray("ExclusiveSourceReadded");renderFor(1300,chain,context,target);}
        leave=chain->SetFullscreenState(FALSE,nullptr);BOOL still=FALSE;chain->GetFullscreenState(&still,nullptr);record("ExclusiveExited",leave,still);
    }
    chain->SetFullscreenState(FALSE,nullptr);
    auto& home=monitors[primary].info;
    SetWindowLongPtrW(window,GWL_STYLE,WS_OVERLAPPEDWINDOW|WS_VISIBLE);SetWindowPos(window,HWND_NOTOPMOST,home.rcMonitor.left+100,home.rcMonitor.top+100,640,400,SWP_FRAMECHANGED|SWP_SHOWWINDOW);
    record("ReturnedToDesktop");if(!cancelled)renderFor(2000,chain,context,target);
    if(lifecycle)record("SourceFinallyDeleted",Shell_NotifyIconW(NIM_DELETE,&tray)?S_OK:E_FAIL);
    bool same=true;
    for(auto& original:monitors){
        DEVMODEW after{};after.dmSize=sizeof(after);
        same=same&&EnumDisplaySettingsExW(original.info.szDevice,ENUM_CURRENT_SETTINGS,&after,0)
            &&original.mode.dmPelsWidth==after.dmPelsWidth&&original.mode.dmPelsHeight==after.dmPelsHeight
            &&original.mode.dmDisplayFrequency==after.dmDisplayFrequency&&original.mode.dmBitsPerPel==after.dmBitsPerPel
            &&original.mode.dmDisplayOrientation==after.dmDisplayOrientation
            &&original.mode.dmPosition.x==after.dmPosition.x&&original.mode.dmPosition.y==after.dmPosition.y;
    }
    bool countUnchanged=readMonitors().size()==monitors.size();bool returnedPrimary=MonitorFromWindow(window,MONITOR_DEFAULTTONEAREST)==monitors[primary].handle;
    std::ofstream result(output/L"fixture-result.json");
    result<<"{\"DeviceCreated\":true,\"Cancelled\":"<<(cancelled?"true":"false")<<",\"ExclusiveEnterHResult\":"<<(long)enter<<",\"ExclusiveActuallyEntered\":"<<(exclusive?"true":"false")<<",\"ExclusiveLeaveHResult\":"<<(long)leave<<",\"DisplayModeRestored\":"<<(same?"true":"false")<<",\"TrayClicks\":"<<trayClicks<<",\"ExclusiveClicks\":"<<exclusiveClicks
        <<",\"TargetIndex\":"<<targetIndex<<",\"TargetPrimary\":"<<((monitor.dwFlags&MONITORINFOF_PRIMARY)?"true":"false")<<",\"WindowOnTarget\":"<<(windowOnTarget?"true":"false")<<",\"ExclusiveOnTarget\":"<<(exclusiveOnTarget?"true":"false")
        <<",\"MonitorCountUnchanged\":"<<(countUnchanged?"true":"false")<<",\"ReturnedToPrimary\":"<<(returnedPrimary?"true":"false")<<",\"StartingDpi\":"<<startingDpi<<",\"ReturnedDpi\":"<<windowDpi(window)<<",\"Monitors\":[";
    for(size_t i=0;i<monitors.size();i++){auto& value=monitors[i];if(i)result<<",";result<<"{\"Index\":"<<i<<",\"Primary\":"<<((value.info.dwFlags&MONITORINFOF_PRIMARY)?"true":"false")<<",\"Width\":"<<value.mode.dmPelsWidth<<",\"Height\":"<<value.mode.dmPelsHeight<<",\"Hz\":"<<value.mode.dmDisplayFrequency<<",\"Left\":"<<value.mode.dmPosition.x<<",\"Top\":"<<value.mode.dmPosition.y<<"}";}
    result<<"]}"<<std::endl;
    activeChain=nullptr;target->Release();chain->Release();context->Release();device->Release();targetOutput->Release();adapter->Release();DestroyWindow(window);UnregisterClassW(cls.lpszClassName,instance);record("FixtureExited");
    return same&&countUnchanged&&returnedPrimary&&SUCCEEDED(leave)?0:5;
}
