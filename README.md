# TrayNest · 图标面板管理工具

<p align="center"><img src="assets/readme/hero-v2.png" width="880" alt="TrayNest：图标多，也能管理"></p>

从 Windows 11 系统隐藏区挑一组图标，单独收好。自己选、自己排，常用入口照常留在任务栏。

**[下载预览版](https://github.com/Wuyuhang0729/traynest/releases/tag/v0.2.0-preview.12)** · [安装与使用](docs/USER_GUIDE.md) · [更新说明](docs/RELEASE-0.2.0-preview.12.md)

<table>
<tr>
<td width="50%" align="center"><strong>自由拖动排序</strong><br><img src="assets/readme/organize-detail.png" width="360" alt="面板内拖动排序"><br>拖入收纳，拖动排序，拖出移回。</td>
<td width="50%" align="center"><strong>重启电脑，布局不变</strong><br><img src="assets/readme/remember-detail.png" width="360" alt="淡色格位保留未启动软件的位置"><br>未启动，也可点击打开。</td>
</tr>
<tr>
<td align="center"><strong>展开方式自由选择</strong><br><img src="assets/readme/expansion-detail.png" width="360" alt="手动、跟随隐藏区、悬停入口三选一"><br>手动、跟随隐藏区或悬停入口。</td>
<td align="center"><strong>两个面板，材质统一</strong><br><img src="assets/readme/material-detail.png" width="360" alt="收纳面板与系统隐藏区使用一致的深色材质"><br>浅色、深色、亚克力，按习惯选。</td>
</tr>
<tr>
<td align="center"><strong>不改变图标使用方式</strong><br><img src="assets/readme/interaction-detail.png" width="360" alt="已收纳图标继续支持原软件操作"><br>单击、双击和右键菜单。</td>
<td align="center"><strong>展开位置，由你决定</strong><br><img src="assets/readme/placement-detail.png" width="360" alt="左侧底部对齐、上方左侧对齐"><br>左侧或上方，按边缘对齐。</td>
</tr>
</table>

| 还有这些 | 功能 |
| --- | --- |
| 独立设置 | 调整展开方式、位置、材质和每行 5–8 个图标 |
| 未启动软件 | 保留原位；可指定 EXE 或快捷方式，点击尝试启动 |
| 开机启动 | 登录后运行 TrayNest，默认关闭 |
| 正常退出 | 恢复原图标和系统背景，保留收纳布局 |
| 移回全部 | 确认后恢复图标并清空收纳项目 |
| 诊断与反馈 | 在设置中复制或导出脱敏报告 |

## 安装

下载并完整解压，双击 **Install.cmd**，以后从桌面打开即可。无需另外安装 .NET。

更新前在设置左下方退出 TrayNest，再安装新包；已有布局会保留。

## 说明

当前仅在 **Windows 11 25H2 x64** 实机验证，部分图标与任务栏美化工具仍有兼容限制。[兼容性说明](docs/COMPATIBILITY.md) · [提交问题](https://github.com/Wuyuhang0729/traynest/issues/new?template=bug_report.yml)

MIT 开源。演示图标属于各自作者，不代表逐项兼容认证。[源码构建](BUILDING.md) · [宣传图层与来源](promo/readme/v2/CREDITS.md)
