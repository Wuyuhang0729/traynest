# TrayNest · 图标面板管理工具

<p align="center"><img src="assets/readme/hero-v2.png" width="880" alt="TrayNest：图标多，也能管理"></p>

从 Windows 11 系统隐藏区挑一组图标，单独收好。自己选、自己排，常用入口照常留在任务栏。

**[下载预览版](https://github.com/Wuyuhang0729/traynest/releases/tag/v0.2.0-preview.17)** · [安装与使用](docs/USER_GUIDE.md) · [更新说明](CHANGELOG.md)

<table>
<tr>
<td width="50%" align="center"><strong>自由拖动排序</strong><br><img src="assets/readme/organize-detail.png" width="360" alt="面板内拖动排序"></td>
<td width="50%" align="center"><strong>重启电脑，布局不变</strong><br><img src="assets/readme/remember-detail.png" width="360" alt="淡色格位保留未启动软件的位置"></td>
</tr>
<tr>
<td align="center"><strong>展开方式自由选择</strong><br><img src="assets/readme/expansion-detail.png" width="360" alt="手动、跟随隐藏区、悬停入口三选一"></td>
<td align="center"><strong>两个面板，材质统一</strong><br><img src="assets/readme/material-detail.png" width="360" alt="收纳面板与系统隐藏区使用一致的亚克力，背景色彩透出"></td>
</tr>
<tr>
<td align="center"><strong>不改变图标使用方式</strong><br><img src="assets/readme/interaction-detail.png" width="360" alt="已收纳图标继续支持原软件操作"></td>
<td align="center"><strong>展开位置，由你决定</strong><br><img src="assets/readme/placement-detail.png" width="360" alt="左侧底部对齐、上方左侧对齐"></td>
</tr>
</table>

| 还有这些 | 功能 |
| --- | --- |
| 独立设置 | 调整展开方式、位置、材质和每行 5–8 个图标 |
| 出场顺序 | 跟随系统隐藏区时，可选“同时”或“先后”；其他模式不受影响 |
| 未启动软件 | 保留原位；可指定 EXE 或快捷方式，点击尝试启动 |
| 开机启动 | 登录后运行 TrayNest，默认关闭 |
| 正常退出 | 恢复原图标和系统背景，保留收纳布局 |
| 移回全部 | 确认后恢复图标并清空收纳项目 |
| 诊断与反馈 | 在设置中复制或导出脱敏报告 |

## 安装与使用

需要 **Windows 11 x64** 和 **Microsoft Edge WebView2 Runtime**。下载包自带 .NET 10；缺少 WebView2 时，向导提供微软手动下载入口。

1. 下载 **TrayNest-Setup-v0.2.0-preview.17-win-x64.exe**，运行当前用户安装向导；也可完整解压 ZIP，运行外层 TrayNest.Setup.exe。
2. 将系统隐藏区图标拖入收纳面板，面板内拖动排序、拖出移回，继续使用原软件单击、双击和右键菜单。
3. 右键入口打开设置，选择独立或联动模式。默认 **Alt+D** 开收，可修改或清除；联动默认控制两个面板，也可只控制收纳。
4. 独立模式使用蓝色箭头入口，面板在入口上方居中；联动模式保留手动、跟随或悬停展开，并可开启完整空白格双击开收。入口位置由 Windows 管理，可自行拖动调整。

设置按常规、打开方式、外观与布局、关于与反馈分类；窗口有浅色、深色和跟随系统主题，与面板材质独立。设置页面全部本机加载。

在“常规”手动检查更新，自动检查与重要提醒分别控制。先下载并校验，再点击“退出并更新”。覆盖安装保留配置、布局和原自启动；失败保留旧版恢复。卸载默认保留配置，清除数据需再次确认。

便携使用完整解压 ZIP，运行 TrayNest/NestedTray.exe 并保留整个目录。[安装与使用](docs/USER_GUIDE.md)。

## 已知问题

部分图标灰暗、叹号角标与双面板状态差异仍待排查；部分来源无法收纳、原生拖动禁止标志仍存在。RetroBar 暂不支持共存，StartAllBack 未完成实测。当前实机依据为 **Windows 11 25H2 x64**，其他 Windows/DPI、登录睡眠与完整第三方矩阵未全部覆盖。

[兼容性说明](docs/COMPATIBILITY.md) · [详细更新日志](CHANGELOG.md) · [提交问题](https://github.com/Wuyuhang0729/traynest/issues/new?template=bug_report.yml)

MIT 开源。演示图标属于各自作者，不代表逐项兼容认证。[源码构建](https://github.com/Wuyuhang0729/traynest/blob/main/BUILDING.md) · [宣传图层与来源](promo/readme/v2/CREDITS.md)
