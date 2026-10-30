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
```

完整交互回归还包括入口菜单、旧版/GUID 图标、跨进程拖放、隐藏维护及故障回滚/崩溃恢复。

```powershell
pwsh -File scripts/Test-Release.ps1 -CandidateDirectory artifacts/publish -FixtureExe artifacts/fixture/TrayFixture.exe
```

执行完整测试前请退出正在使用的 TrayNest，并暂停鼠标键盘操作。测试使用自有图标，会移动鼠标；崩溃恢复用例会强制结束它自己创建的测试收纳进程。勿将未知或用户软件作为测试来源。测试不会启用开机启动，也不会重启 Explorer。

测试结果不代表全部 Windows 构建、第三方软件或显示配置均兼容。范围见 [兼容性说明](docs/COMPATIBILITY.md)。
