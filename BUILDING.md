# 源码构建与验证

需要 Windows 11 x64、PowerShell 7 和 .NET 10 SDK。

```powershell
pwsh -File scripts/Prepare-InnoSetup.ps1
pwsh -File scripts/Build-Release.ps1
```

首次构建需联网恢复固定版本 NuGet 包、.NET 运行时、C++/WinRT 与 LLVM-MinGW 工具链。工具位于项目 .tools，不安装全局编译器；准备脚本核对工具链摘要、NuGet 签名和 Inno 官方签名。生成的自带运行时 ZIP、Inno EXE 和同名 SHA256 文件位于 artifacts。

保留 src、installer、scripts，以及 verification/Shared/NativeTray.cs 和 verification/StandaloneProbe/TrayPacket.cs 的相对位置；后两者被主项目直接引用。原生桥接从 src/TrayNest.NativeBridge 构建，载荷带摘要清单和完整许可证。设置 HTML/CSS/JS 本地嵌入，无 CDN；使用固定 WebView2 SDK，运行时另需 Microsoft Edge WebView2 Runtime。

## 隔离验证

```powershell
dotnet run --project src/TrayNest.Installation.Tests -c Release -- artifacts/qa/core
pwsh -File verification/Test-TN017-Installer.ps1 -Payload <完整候选目录> -OutputDirectory artifacts/qa/installer
```

安装测试使用专用 AppId 和配置，实际检查首装、覆盖、损坏包回退和卸载，不启动真实托盘或改变真实自启动。实时 Release 只读查询：

```powershell
dotnet run --project src/TrayNest.Installation.Tests -c Release -- --live 0.2.0-preview.16 artifacts/qa/live.json
```

主程序 --self-test-settings、--self-test-entry-policy、--self-test-opening、--self-test-placement、--self-test-animation、--self-test-layout 等入口接收独立结果目录。settings 使用真实 WebView 页面但不移动鼠标；逻辑和模拟检查不替代真实 DPI、材质或桌面交互。

scripts/Test-Release.ps1 包含会操作桌面的完整测试：先正常退出日常 TrayNest，使用 verification/TrayFixture 自有图标并暂停输入，再顺序运行。不要与日常版并发接管托盘，不重启 Explorer。verification/Test-NativeBridgeFixture.ps1 使用自有 XAML 宿主检查材质及恢复，不能把初始化成功当视觉验收。

安装说明、限制及本版验证边界见 docs/USER_GUIDE.md、docs/COMPATIBILITY.md 与 docs/RELEASE-0.2.0-preview.17.md。
