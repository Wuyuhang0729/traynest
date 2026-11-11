# TrayNest 0.2.0-preview.17

相较上次公开版 preview.13，本次增加独立/联动模式、默认 Alt+D 全局快捷键、可选的系统空白格双击开收；设置窗口采用新的分类布局，支持浅色、深色及跟随系统主题。

新增标准 Inno 安装向导，以及设置内检查、下载、校验和用户确认后退出更新。自动检查与重要提醒可分别关闭；覆盖安装保留配置、布局和自启动，卸载默认保留配置。

改善悬停、拖入和排序反馈，修复底边残影、动效层级及任务栏短条；修复已观察到的 TranslucentTB 路径别名身份、ClickShow 退出后状态清理、TrafficMonitor 1.86 Lite 任务栏定位，以及设置首次打开和图标坐标查询的相关问题。配置与恢复记录替换遇到短暂文件占用时会有界重试，持续失败仍保留回滚保护。

完整逐项变化见[更新日志](../CHANGELOG.md)，当前操作见[使用指南](USER_GUIDE.md)。

## 下载与升级

- 推荐下载 `TrayNest-Setup-v0.2.0-preview.17-win-x64.exe`，运行当前用户安装向导。
- 完整包为 `TrayNest-v0.2.0-preview.17-win-x64.zip`；完整解压后运行外层 `TrayNest.Setup.exe`，或内层 `TrayNest/Install.cmd`。便携使用运行 `TrayNest/NestedTray.exe`，保留完整目录。
- 两个下载文件均提供同名 `.sha256`。ZIP 内的 `traynest-package.json` 记录完整载荷摘要，原生材质组件及许可证一并保留。
- 需要 Windows 11 x64 和 Microsoft Edge WebView2 Runtime；自带 .NET 10，无需另装 .NET。缺少 WebView2 时按向导打开微软下载入口，安装后重试。
- 更新使用正常退出和旧版备份，不强制结束软件或重启 Explorer。建议升级前保留原包；详细恢复边界见使用指南。

## 已知问题

部分图标灰暗、叹号角标及双面板状态差异仍待排查。部分来源无法收纳、拖动禁止标志及未完全复现的权限错误仍保留。RetroBar 暂不支持共存，StartAllBack 未实测；不将定向修复描述为全部第三方兼容。

本版是预览版。历史功能验收与本次发布候选验证分开记录；不声称真实登录/睡眠、多 Windows/DPI、完整拖放菜单、原生材质视觉与全部第三方矩阵通过。[兼容性与验证边界](COMPATIBILITY.md)。


