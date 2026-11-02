# 构建与测试

需要 Windows 11 x64、PowerShell 7 和 .NET 10 SDK。

## 完整下载包

```powershell
pwsh -File scripts/Build-Release.ps1
```

首次构建需要访问 NuGet 官方源下载运行时包。脚本在 `artifacts/` 生成自带运行时的 Windows x64 ZIP、校验文件和本次独立构建目录，并保留运行时许可证。

也可以直接构建：

```powershell
dotnet publish src/NestedTray/NestedTray.csproj -c Release -r win-x64 --self-contained true --source https://api.nuget.org/v3/index.json -o artifacts/publish
```

若分发手动构建结果，请一并保留项目许可证及运行时包提供的许可证和第三方声明；可以使用 `scripts/Copy-RuntimeNotices.ps1` 补齐。

`verification/Shared/NativeTray.cs` 和 `verification/StandaloneProbe/TrayPacket.cs` 是项目通过相对路径引用的源码，请保留目录结构。

## 自有测试来源

```powershell
dotnet publish verification/TrayFixture/TrayFixture.csproj -c Release -r win-x64 --self-contained true --source https://api.nuget.org/v3/index.json -o artifacts/fixture
```

主程序提供独立测试入口。动画、布局和诊断可以使用独立目录运行：

```powershell
artifacts/publish/NestedTray.exe --self-test-animation artifacts/qa/animation
artifacts/publish/NestedTray.exe --self-test-layout artifacts/qa/layout
artifacts/publish/NestedTray.exe --self-test-diagnostics artifacts/qa/diagnostics
artifacts/publish/NestedTray.exe --self-test-tray-connection artifacts/qa/tray-connection
artifacts/publish/NestedTray.exe --self-test-startup artifacts/qa/startup
artifacts/publish/NestedTray.exe --self-test-experience artifacts/qa/experience
artifacts/publish/NestedTray.exe --self-test-fixed-tools artifacts/qa/fixed-tools
artifacts/publish/NestedTray.exe --self-test-placement artifacts/qa/placement
```

托盘连接检查使用自有隔离原生窗口，验证超时后保留程序、按间隔重试和响应恢复，不移动鼠标、不操作真实 Explorer。

体验优化检查使用独立配置、实际恢复记录文件、注入的原生状态适配器和屏幕外面板，覆盖批量取消／部分失败、日志与配置故障、拖动越界取消、可控时钟启动状态及模拟DPI文案布局。不接管Explorer、不移动鼠标，也不代替真实拖放、第三方菜单或真实DPI验证。

启动隔离测试核对登录任务配置、Windows 规范化后的配置读取、延迟就绪和有界等待，不写真实启动设置、不重启电脑。真实登录触发应独立验证，不能以此隔离测试代替。`verification/Deploy-StartupFix.ps1` 是本机修复部署脚本，会备份配置、正常退出已有版本、切换真实启动开关，并通过当前用户登录任务启动软件；它会修改本机自启动设置。

完整交互回归还包括入口菜单、旧版/GUID 图标、跨进程拖放、隐藏维护及故障回滚/崩溃恢复。

## 品牌图标与桌面快捷方式

`src/NestedTray/Resources/TrayNest.ico` 是提交到源码的正式多尺寸图标，同时用于Windows EXE资源、嵌入程序集的托盘／窗口图标以及发布目录中的独立ICO。正常构建不需要图形工具或网络生成图标。

完整矢量源、16–1024像素PNG、单色版和六方案展示在 `assets/branding/`。需要重新生成时，执行 `npm install --prefix .tools/logo-render @resvg/resvg-js@2.6.2`，再运行 `node scripts/Build-Icons.cjs`。ICO包含16、20、24、32、40、48、64、96、128、256像素的32位透明帧。

发布脚本会附带 `TrayNest.ico` 和 `Create-DesktopShortcut.ps1`。在解压目录运行后者，可创建显式引用同目录ICO的桌面快捷方式；直接在源码目录调用时使用 `scripts/Create-DesktopShortcut.ps1 -InstallDirectory <程序目录>`。这样桌面图标不会依赖默认程序图标。

```powershell
pwsh -File scripts/Test-Release.ps1 -CandidateDirectory artifacts/publish -FixtureExe artifacts/fixture/TrayFixture.exe
```

执行完整测试前请退出正在使用的 TrayNest，并暂停鼠标键盘操作。测试使用自有图标，会移动鼠标；崩溃恢复用例会强制结束它自己创建的测试收纳进程。勿将未知或用户软件作为测试来源。测试不会启用开机启动，也不会重启 Explorer。

测试结果不代表全部 Windows 构建、第三方软件或显示配置均兼容。范围见 [兼容性说明](docs/COMPATIBILITY.md)。

## 面板内排序交互测试

```powershell
artifacts/publish/NestedTray.exe --self-test-panel-reorder artifacts/qa/panel-reorder
```

此项会移动真实鼠标并发送 Esc，运行前请暂停输入。它只操作独立面板中的十个自有测试图标，使用独立配置目录，不接管 Explorer、不写真实自启动。检查目标框、无悬浮重复图标、实时让位、松手保存、取消、灰色图标、配置写入失败和重开保持顺序，并保存操作中与放下后的截图。此项已加入完整交互回归入口。

`--self-test-reorder-motion <目录>` 则在屏幕外验证 180 毫秒换位动画，检查起点、中间位置、相邻图标移动、连续改向不跳位和取消回位，不移动鼠标。遵循 Windows 的系统动画开关。
