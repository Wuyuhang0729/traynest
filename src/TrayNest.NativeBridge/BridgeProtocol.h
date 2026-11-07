#pragma once
#include <windows.h>
#include <cstdint>

inline constexpr DWORD BridgeMagic=0x544e4d31;
inline constexpr DWORD BridgeVersion=1;
#if __has_include("BridgeBuild.h")
#include "BridgeBuild.h"
inline constexpr auto& BridgeWindowClass=GeneratedBridgeWindowClass;
inline constexpr auto BridgeClassId=GeneratedBridgeClassId;
#else
inline constexpr wchar_t BridgeWindowClass[]=L"TrayNest.MaterialBridge.development";
inline constexpr CLSID BridgeClassId={0x982a0c91,0x4e11,0x47d6,{0xa3,0x04,0x5e,0x78,0x16,0x7d,0x9b,0x34}};
#endif
struct SharedPolicy {
    DWORD magic,version,ownerPid,explorerPid;
    std::uint64_t rootWindow;
    volatile LONG sequence;
    LONG mode,light,transparency;
    volatile LONG connected,targets,applied,ack,error,restored,ownerDead,nativeVersion;
    volatile LONG callbacks,namedCallbacks,callbackThread,expectedThread,adviseResult;
    volatile LONG originalAcrylic,originalSource,originalFallback,installedFallback;
};
static_assert(offsetof(SharedPolicy,connected)==40);
static_assert(sizeof(SharedPolicy)==112);
