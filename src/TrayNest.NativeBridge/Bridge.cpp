#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <ocidl.h>
#ifndef _Out_retval_
#define _Out_retval_
#endif
#undef GetCurrentTime
#include "include/xamlOM.h"
__CRT_UUID_DECL(IXamlDiagnostics,0x18c9e2b6,0x3f43,0x4116,0x9f,0x2b,0xff,0x93,0x5d,0x77,0x70,0xd2)
__CRT_UUID_DECL(IVisualTreeService,0xa593b11a,0xd17f,0x48bb,0x8f,0x66,0x83,0x91,0x07,0x31,0xc8,0xa5)
__CRT_UUID_DECL(IVisualTreeServiceCallback,0xaa7a8931,0x80e4,0x4fec,0x8f,0x3b,0x55,0x3f,0x87,0xb4,0x96,0x6e)
__CRT_UUID_DECL(IVisualTreeService3,0x0e79c6e0,0x85a0,0x4be8,0xb4,0x1a,0x65,0x5c,0xf1,0xfd,0x19,0xbd)
__CRT_UUID_DECL(IVisualTreeServiceCallback2,0xbad9eb88,0xae77,0x4397,0xb9,0x48,0x5f,0xa2,0xdb,0x0a,0x19,0xea)
#include "BridgeProtocol.h"
#include <exception>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.UI.Xaml.Controls.h>
#include <winrt/Windows.UI.Xaml.Media.h>
#include <atomic>
#include <string>
#include <vector>
#include <thread>
#include <cstdio>

namespace xaml=winrt::Windows::UI::Xaml;
namespace media=winrt::Windows::UI::Xaml::Media;
using Border=winrt::Windows::UI::Xaml::Controls::Border;
static HMODULE module;
static bool FixtureProcess() {static bool value=[](){wchar_t path[32768]{};GetModuleFileNameW(nullptr,path,32768);return std::wstring_view(path).ends_with(L"TrayNest.NativeFixture.exe");}();return value;}

struct Target {
    InstanceHandle handle;
    winrt::weak_ref<Border> border;
    winrt::Windows::Foundation::IInspectable original{nullptr};
    media::Brush installed{nullptr};
    bool changed=false,clearOnRestore=false;
};
class Agent {
    HANDLE mapping=nullptr,owner=nullptr;
    SharedPolicy* policy=nullptr;
    HWND pump=nullptr;
    DWORD threadId=0;
    LONG lastSequence=-1;
    std::vector<Target> targets;
    bool validTarget(Border const& border) {
        auto parent=media::VisualTreeHelper::GetParent(border);
        for(int depth=0;parent&&depth<12;depth++,parent=media::VisualTreeHelper::GetParent(parent))
        {
            if(winrt::get_class_name(parent)==L"SystemTray.NotificationAreaOverflow")return true;
            if(FixtureProcess())
                if(auto element=parent.try_as<xaml::FrameworkElement>();element&&element.Name()==L"TrayNestMaterialTestRoot")return true;
        }
        return false;
    }
    void restore() noexcept {
        for(auto& target:targets)try {
            if(auto border=target.border.get();border&&target.changed) {
                // Do not overwrite a brush installed by another styling tool.
                if(border.Background()==target.installed) {
                    if(target.clearOnRestore)border.ClearValue(Border::BackgroundProperty());
                    else border.SetValue(Border::BackgroundProperty(),target.original);
                }
            }
            target.changed=false;target.original=nullptr;target.installed=nullptr;
        }catch(...){if(policy)InterlockedExchange(&policy->error,winrt::to_hresult());}
        if(policy){InterlockedExchange(&policy->applied,0);InterlockedExchange(&policy->restored,1);}
    }
    void paint(Target& target,LONG mode,bool light,bool transparency) {
        auto border=target.border.get();if(!border||!validTarget(border))return;
        if(target.changed&&border.Background()!=target.installed)throw winrt::hresult_error(HRESULT_FROM_WIN32(ERROR_BUSY));
        if(!target.changed) {
            target.original=border.ReadLocalValue(Border::BackgroundProperty());
            target.clearOnRestore=target.original==xaml::DependencyProperty::UnsetValue();
            if(!target.clearOnRestore&&target.original&&!target.original.try_as<media::Brush>())
                throw winrt::hresult_not_implemented();
            if(auto acrylic=border.Background().try_as<media::AcrylicBrush>()) {
                policy->originalAcrylic=1;policy->originalSource=static_cast<LONG>(acrylic.BackgroundSource());policy->originalFallback=acrylic.AlwaysUseFallback()?1:0;
            }
        }
        media::Brush brush{nullptr};
        auto color=light?winrt::Windows::UI::Color{255,250,250,250}:winrt::Windows::UI::Color{255,32,34,40};
        if(mode==4)brush=media::SolidColorBrush(winrt::Windows::UI::Color{255,238,44,86});
        else if(mode==3&&transparency) {
            media::AcrylicBrush acrylic;acrylic.BackgroundSource(media::AcrylicBackgroundSource::Backdrop);
            acrylic.TintColor(color);acrylic.TintOpacity(light?.08:.12);
            acrylic.TintLuminosityOpacity(winrt::box_value(.08).as<winrt::Windows::Foundation::IReference<double>>());
            acrylic.AlwaysUseFallback(false);policy->installedFallback=acrylic.AlwaysUseFallback()?1:0;
            acrylic.FallbackColor(color);brush=acrylic;
        }else brush=media::SolidColorBrush(color);
        border.Background(brush);target.installed=brush;target.changed=true;
    }
public:
    void TraceCallback(bool named) noexcept {if(policy){InterlockedIncrement(&policy->callbacks);if(named){InterlockedIncrement(&policy->namedCallbacks);InterlockedExchange(&policy->callbackThread,GetCurrentThreadId());}}}
    void AdviseResult(HRESULT result) noexcept {if(policy)InterlockedExchange(&policy->adviseResult,result);}
    bool Bind(std::wstring const& name) {
        if(name.rfind(L"Local\\TrayNest.Material.",0)!=0||name.size()>180)return false;
        HANDLE nextMap=OpenFileMappingW(FILE_MAP_READ|FILE_MAP_WRITE,FALSE,name.c_str());if(!nextMap)return false;
        auto next=static_cast<SharedPolicy*>(MapViewOfFile(nextMap,FILE_MAP_READ|FILE_MAP_WRITE,0,0,sizeof(SharedPolicy)));
        if(!next){CloseHandle(nextMap);return false;}
        DWORD targetOwner=0;auto root=reinterpret_cast<HWND>(next->rootWindow);
        DWORD targetThread=GetWindowThreadProcessId(root,&targetOwner);
        wchar_t cls[100]{};GetClassNameW(root,cls,100);
        HANDLE nextOwner=nullptr;
        bool valid=next->magic==BridgeMagic&&next->version==BridgeVersion&&next->explorerPid==GetCurrentProcessId()&&
            targetOwner==GetCurrentProcessId()&&wcscmp(cls,L"TopLevelWindowForOverflowXamlIsland")==0;
        if(valid)nextOwner=OpenProcess(SYNCHRONIZE|PROCESS_QUERY_LIMITED_INFORMATION,FALSE,next->ownerPid);
        if(!valid||!nextOwner||(owner&&policy&&policy->ownerPid!=next->ownerPid&&WaitForSingleObject(owner,0)!=WAIT_OBJECT_0)) {
            if(nextOwner)CloseHandle(nextOwner);UnmapViewOfFile(next);CloseHandle(nextMap);return false;
        }
        restore();if(owner)CloseHandle(owner);if(policy)UnmapViewOfFile(policy);if(mapping)CloseHandle(mapping);
        mapping=nextMap;policy=next;owner=nextOwner;threadId=targetThread;lastSequence=-1;
        InterlockedExchange(&policy->expectedThread,targetThread);
        InterlockedExchange(&policy->nativeVersion,BridgeVersion);InterlockedExchange(&policy->connected,1);
        if(pump)SetTimer(pump,1,100,nullptr);
        return true;
    }
    void Observe(InstanceHandle handle,Border const& border) {
        if(!policy||GetCurrentThreadId()!=threadId)return;
        if(!pump) {
            WNDCLASSW cls{};cls.lpfnWndProc=WindowProcedure;cls.hInstance=module;cls.lpszClassName=BridgeWindowClass;RegisterClassW(&cls);
            pump=CreateWindowExW(0,BridgeWindowClass,L"",0,0,0,0,0,HWND_MESSAGE,nullptr,module,this);
            if(!pump)throw winrt::hresult_error(HRESULT_FROM_WIN32(GetLastError()));
            SetTimer(pump,1,100,nullptr);
        }
        for(auto const& target:targets)if(target.handle==handle)return;
        targets.push_back(Target{handle,winrt::make_weak(border)});lastSequence=-1;
    }
    void Remove(InstanceHandle handle) {
        if(GetCurrentThreadId()!=threadId)return;
        std::erase_if(targets,[&](auto const& target){return target.handle==handle;});lastSequence=-1;
    }
    void Discover(winrt::Windows::Foundation::IInspectable const& value,IXamlDiagnostics* diagnostics) {
        if(!policy||GetCurrentThreadId()!=threadId||!targets.empty())return;
        auto root=value.try_as<xaml::DependencyObject>();if(!root)return;
        std::vector<xaml::DependencyObject> pending{root};int visited=0;
        while(!pending.empty()&&visited++<2048) {
            auto current=pending.back();pending.pop_back();
            if(auto element=current.try_as<xaml::FrameworkElement>()) {
                if(element.Name()==L"OverflowFlyoutBackgroundBorder")if(auto border=element.try_as<Border>()) {
                    InstanceHandle handle=0;diagnostics->GetHandleFromIInspectable(reinterpret_cast<::IInspectable*>(winrt::get_abi(border)),&handle);
                    if(!handle)handle=reinterpret_cast<InstanceHandle>(winrt::get_abi(border));Observe(handle,border);return;
                }
            }
            int count=media::VisualTreeHelper::GetChildrenCount(current);
            for(int i=0;i<count;i++)pending.push_back(media::VisualTreeHelper::GetChild(current,i));
        }
    }
    void Tick() noexcept {
        if(!policy)return;
        if(WaitForSingleObject(owner,0)==WAIT_OBJECT_0) {
            restore();InterlockedExchange(&policy->ownerDead,1);KillTimer(pump,1);return;
        }
        if((GetAsyncKeyState(VK_LBUTTON)&0x8000)!=0||(GetAsyncKeyState(VK_RBUTTON)&0x8000)!=0)return;
        LONG sequence=InterlockedCompareExchange(&policy->sequence,0,0);if(sequence&1)return;
        LONG mode=policy->mode;bool light=policy->light!=0,transparency=policy->transparency!=0;
        MemoryBarrier();if(sequence!=policy->sequence)return;
        try {
            int matched=0;for(auto const& target:targets)if(auto border=target.border.get();border&&validTarget(border))matched++;
            InterlockedExchange(&policy->targets,matched);
            if(sequence==lastSequence)return;
            if(mode==0)restore();else {
                for(auto& target:targets)paint(target,mode,light,transparency);
                InterlockedExchange(&policy->applied,matched);InterlockedExchange(&policy->restored,0);
            }
            InterlockedExchange(&policy->error,0);lastSequence=sequence;InterlockedExchange(&policy->ack,sequence);
        }catch(...){InterlockedExchange(&policy->error,winrt::to_hresult());restore();lastSequence=sequence;InterlockedExchange(&policy->ack,sequence);}
    }
    static LRESULT CALLBACK WindowProcedure(HWND hwnd,UINT message,WPARAM wp,LPARAM lp) {
        auto self=reinterpret_cast<Agent*>(GetWindowLongPtrW(hwnd,GWLP_USERDATA));
        if(message==WM_NCCREATE){self=static_cast<Agent*>(reinterpret_cast<CREATESTRUCTW*>(lp)->lpCreateParams);SetWindowLongPtrW(hwnd,GWLP_USERDATA,reinterpret_cast<LONG_PTR>(self));}
        if(self&&message==WM_TIMER){self->Tick();return 0;}
        if(self&&message==WM_COPYDATA) {
            auto data=reinterpret_cast<COPYDATASTRUCT*>(lp);
            if(data&&data->dwData==BridgeMagic&&data->lpData&&data->cbData>=sizeof(wchar_t)&&data->cbData<=362) {
                auto name=static_cast<wchar_t const*>(data->lpData);size_t count=data->cbData/sizeof(wchar_t);
                if(name[count-1]==0&&self->Bind(std::wstring(name,count-1)))return 1;
            }
            return 0;
        }
        return DefWindowProcW(hwnd,message,wp,lp);
    }
};
static Agent agent;

struct Watcher:winrt::implements<Watcher,IVisualTreeServiceCallback2,winrt::non_agile> {
    winrt::com_ptr<IXamlDiagnostics> diagnostics;
    explicit Watcher(winrt::com_ptr<IXamlDiagnostics> value):diagnostics(std::move(value)){}
    HRESULT __stdcall OnElementStateChanged(InstanceHandle,VisualElementState,LPCWSTR) noexcept override {return S_OK;}
    HRESULT __stdcall OnVisualTreeChange(ParentChildRelation,VisualElement element,VisualMutationType mutation) noexcept override {
        if(mutation==Remove){agent.Remove(element.Handle);return S_OK;}
        agent.TraceCallback(element.Name&&wcscmp(element.Name,L"OverflowFlyoutBackgroundBorder")==0);
        HRESULT result=S_OK;
        try {
            if((element.Name&&wcscmp(element.Name,L"OverflowFlyoutBackgroundBorder")==0)||(element.Type&&(std::wstring_view(element.Type).ends_with(L"Border")||std::wstring_view(element.Type).ends_with(L"Grid")||std::wstring_view(element.Type).ends_with(L"RootScrollViewer")))) {
                winrt::Windows::Foundation::IInspectable value{nullptr};
                winrt::check_hresult(diagnostics->GetIInspectableFromHandle(element.Handle,reinterpret_cast<::IInspectable**>(winrt::put_abi(value))));
                if(auto border=value.try_as<Border>();border&&border.Name()==L"OverflowFlyoutBackgroundBorder")agent.Observe(element.Handle,border);
                else agent.Discover(value,diagnostics.get());
            }
        }catch(...){result=winrt::to_hresult();}
        SysFreeString(element.Name);SysFreeString(element.Type);SysFreeString(element.SrcInfo.FileName);SysFreeString(element.SrcInfo.Hash);
        return result;
    }
};
struct Site:winrt::implements<Site,IObjectWithSite> {
    winrt::com_ptr<IUnknown> site;
    HRESULT __stdcall SetSite(IUnknown* value) noexcept override {
        try {
            if(!value)return S_OK;
            site.copy_from(value);auto diagnostics=site.as<IXamlDiagnostics>();BSTR data=nullptr;
            winrt::check_hresult(diagnostics->GetInitializationData(&data));std::wstring name=data?data:L"";SysFreeString(data);
            if(!agent.Bind(name))return E_ACCESSDENIED;
            auto watcher=winrt::make_self<Watcher>(diagnostics);
            auto service=site.as<IVisualTreeService3>();
            std::thread([service,watcher](){agent.AdviseResult(service->AdviseVisualTreeChange(watcher.get()));}).detach();
            return S_OK;
        }catch(...){return winrt::to_hresult();}
    }
    HRESULT __stdcall GetSite(REFIID iid,void** result) noexcept override {return site?site->QueryInterface(iid,result):E_FAIL;}
};
struct Factory:winrt::implements<Factory,IClassFactory> {
    HRESULT __stdcall CreateInstance(IUnknown* outer,REFIID iid,void** result) noexcept override {
        if(outer)return CLASS_E_NOAGGREGATION;
        try{return winrt::make_self<Site>()->QueryInterface(iid,result);}catch(...){return winrt::to_hresult();}
    }
    HRESULT __stdcall LockServer(BOOL) noexcept override {return S_OK;}
};
extern "C" HRESULT __stdcall DllGetClassObject(REFCLSID cls,REFIID iid,void** result) {
    if(!result)return E_POINTER;*result=nullptr;if(cls!=BridgeClassId)return CLASS_E_CLASSNOTAVAILABLE;
    try{return winrt::make_self<Factory>()->QueryInterface(iid,result);}catch(...){return winrt::to_hresult();}
}
extern "C" HRESULT __stdcall DllCanUnloadNow(){return S_FALSE;}
BOOL WINAPI DllMain(HINSTANCE instance,DWORD reason,LPVOID){if(reason==DLL_PROCESS_ATTACH){module=instance;DisableThreadLibraryCalls(instance);}return TRUE;}
