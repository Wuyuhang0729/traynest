#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <roapi.h>
#include <cstdio>
#include <string>
#include <thread>
#include "BridgeProtocol.h"
using Initialize=HRESULT(WINAPI*)(LPCWSTR,DWORD,LPCWSTR,LPCWSTR,CLSID,LPCWSTR);
int wmain(int argc,wchar_t** argv) {
    if(argc==2&&wcscmp(argv[1],L"--owner-wait")==0){Sleep(30000);return 0;}
    if(argc!=4)return 2;
    DWORD pid=wcstoul(argv[1],nullptr,10);if(!pid||!argv[2][0]||!argv[3][0])return 2;
    for(HWND window=FindWindowExW(HWND_MESSAGE,nullptr,BridgeWindowClass,nullptr);window;window=FindWindowExW(HWND_MESSAGE,window,BridgeWindowClass,nullptr)) {
        DWORD owner=0;GetWindowThreadProcessId(window,&owner);if(owner!=pid)continue;
        COPYDATASTRUCT data{};data.dwData=BridgeMagic;data.cbData=static_cast<DWORD>((wcslen(argv[2])+1)*sizeof(wchar_t));data.lpData=argv[2];
        DWORD_PTR reply=0;if(SendMessageTimeoutW(window,WM_COPYDATA,0,reinterpret_cast<LPARAM>(&data),SMTO_ABORTIFHUNG|SMTO_BLOCK,500,&reply)&&reply==1){wprintf(L"Reused existing bridge\n");return 0;}
        return 6;
    }
    auto library=LoadLibraryExW(L"Windows.UI.Xaml.dll",nullptr,LOAD_LIBRARY_SEARCH_SYSTEM32);if(!library)return 3;
    auto initialize=reinterpret_cast<Initialize>(GetProcAddress(library,"InitializeXamlDiagnosticsEx"));if(!initialize)return 4;
    HANDLE mapping=OpenFileMappingW(FILE_MAP_READ,FALSE,argv[2]);
    auto policy=mapping?static_cast<SharedPolicy*>(MapViewOfFile(mapping,FILE_MAP_READ,0,0,sizeof(SharedPolicy))):nullptr;
    if(!policy){if(mapping)CloseHandle(mapping);return 7;}
    HRESULT result=HRESULT_FROM_WIN32(ERROR_NOT_FOUND);
    for(int attempt=1;attempt<=64;attempt++) {
        std::thread worker([&](){
            RoInitialize(RO_INIT_MULTITHREADED);
            auto endpoint=L"VisualDiagConnection"+std::to_wstring(attempt);
            result=initialize(endpoint.c_str(),pid,nullptr,argv[3],BridgeClassId,argv[2]);
            RoUninitialize();
        });worker.join();
        if(SUCCEEDED(result)) {
            for(int wait=0;wait<20&&policy->targets<1;wait++)Sleep(50);
            if(policy->targets>0)break;
        }
        if(FAILED(result)&&result!=HRESULT_FROM_WIN32(ERROR_NOT_FOUND))break;
    }
    for(int wait=0;wait<20&&policy->connected&&policy->targets<1;wait++)Sleep(50);
    bool found=policy->connected&&policy->targets>0;if(found)result=S_OK;UnmapViewOfFile(policy);CloseHandle(mapping);
    wprintf(L"Initialize=0x%08lX TargetFound=%d\n",static_cast<unsigned long>(result),found?1:0);return SUCCEEDED(result)&&found?0:5;
}
