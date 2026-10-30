# 从源码构建

需要 Windows 11 x64 和 .NET 9 SDK。

```powershell
dotnet publish src/NestedTray/NestedTray.csproj -c Release --self-contained false -o artifacts/publish
```

运行 `artifacts/publish/NestedTray.exe`。此方式生成的程序需要 .NET 9 Desktop Runtime（x64）。

`verification/Shared/NativeTray.cs` 和 `verification/StandaloneProbe/TrayPacket.cs` 是主项目通过相对路径引用的源码，构建时须保留目录结构。

## 自带运行时的构建

使用最新 .NET 9 SDK，并允许访问 NuGet 官方源下载运行时包：

```powershell
dotnet restore src/NestedTray/NestedTray.csproj -r win-x64 -p:SelfContained=true --source https://api.nuget.org/v3/index.json
dotnet publish src/NestedTray/NestedTray.csproj -c Release -r win-x64 --self-contained true --no-restore -o artifacts/self-contained
```

完整分发该目录，并保留构建产生的运行时许可证和通知文件。首次上传的预览包为依赖运行时版本，自带运行时的构建方式尚未作为本次下载包验证。

## 验证入口

主程序包含动画、布局、菜单、拖放与图标恢复的自检入口。完整托盘交互验证需要交互式 Windows 桌面及自有测试来源；这些测试可能移动鼠标，请先保存工作，不要与日常操作同时运行。

```powershell
dotnet build verification/TrayFixture/TrayFixture.csproj -c Release
```

`.NET 9` 将于 2026-11-10 结束支持，长期维护版本应升级到受支持的运行时并重新验证。参考：[微软 .NET 支持周期](https://dotnet.microsoft.com/platform/support/policy/dotnet-core)。
