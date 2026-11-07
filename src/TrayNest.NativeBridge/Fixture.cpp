#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <unknwn.h>
#include <exception>
#undef GetCurrentTime
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.UI.Xaml.Controls.h>
#include <winrt/Windows.UI.Xaml.Hosting.h>
#include <winrt/Windows.UI.Xaml.Media.h>
#include <winrt/Windows.UI.Xaml.Markup.h>
#include <cstdio>

struct __declspec(uuid("3cbcf1bf-2f76-4e9c-96ab-e84b37972554")) NativeXamlSource:IUnknown {
    virtual HRESULT __stdcall AttachToWindow(HWND window)=0;
    virtual HRESULT __stdcall get_WindowHandle(HWND* window)=0;
};
__CRT_UUID_DECL(NativeXamlSource,0x3cbcf1bf,0x2f76,0x4e9c,0x96,0xab,0xe8,0x4b,0x37,0x97,0x25,0x54)
static HWND child;
static LRESULT CALLBACK WindowProc(HWND window,UINT message,WPARAM wp,LPARAM lp) {
    if(message==WM_SIZE&&child)SetWindowPos(child,nullptr,0,0,LOWORD(lp),HIWORD(lp),SWP_NOZORDER);
    if(message==WM_CLOSE){DestroyWindow(window);return 0;}
    if(message==WM_DESTROY){PostQuitMessage(0);return 0;}
    return DefWindowProcW(window,message,wp,lp);
}
int wmain(int argc,wchar_t** argv) {
    if(argc!=2)return 2;
    try {
        winrt::init_apartment(winrt::apartment_type::single_threaded);
        auto manager=winrt::Windows::UI::Xaml::Hosting::WindowsXamlManager::InitializeForCurrentThread();
        WNDCLASSW cls{};cls.lpfnWndProc=WindowProc;cls.hInstance=GetModuleHandleW(nullptr);cls.lpszClassName=L"TopLevelWindowForOverflowXamlIsland";RegisterClassW(&cls);
        HWND window=CreateWindowExW(0,cls.lpszClassName,L"TrayNest material isolated fixture",WS_OVERLAPPEDWINDOW,60,60,520,320,nullptr,nullptr,cls.hInstance,nullptr);
        winrt::Windows::UI::Xaml::Hosting::DesktopWindowXamlSource source;
        auto native=source.as<NativeXamlSource>();winrt::check_hresult(native->AttachToWindow(window));native->get_WindowHandle(&child);
        auto root=winrt::Windows::UI::Xaml::Markup::XamlReader::Load(LR"(<Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" x:Name="TrayNestMaterialTestRoot"><Border x:Name="OverflowFlyoutBackgroundBorder" Background="#2D73BE" /></Grid>)");
        source.Content(root.as<winrt::Windows::UI::Xaml::UIElement>());
        RECT rect{};GetClientRect(window,&rect);SetWindowPos(child,nullptr,0,0,rect.right,rect.bottom,SWP_SHOWWINDOW);ShowWindow(window,SW_SHOW);
        auto file=_wfopen(argv[1],L"wb");if(file){fprintf(file,"{\"Process\":%lu,\"Window\":%llu}",GetCurrentProcessId(),static_cast<unsigned long long>(reinterpret_cast<ULONG_PTR>(window)));fclose(file);}
        MSG message{};while(GetMessageW(&message,nullptr,0,0)>0){TranslateMessage(&message);DispatchMessageW(&message);}
        source.Close();manager.Close();return 0;
    }catch(winrt::hresult_error const& error){fwprintf(stderr,L"XAML fixture HRESULT 0x%08lX %s\n",static_cast<unsigned long>(error.code()),error.message().c_str());return 1;}
}
