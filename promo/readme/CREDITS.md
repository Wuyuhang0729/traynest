# README 宣传图与分层工程

制作日期：2026-11-02；文案与使用场景示意更新于2026-11-03。成图位于 `assets/readme/`：hero（1600×900）、features（1600×1030）、placement（1600×980），以及case-developer、case-hr、case-gamer三张1080×1820竖向人群案例。hierarchy为程序员案例的兼容副本。README使用相对路径引用PNG，GitHub无需运行脚本即可显示。图片展示本地开发版，公开下载包以Release说明为准。

README先说明任务栏空间有限，再用竖向的“任务栏 → 系统隐藏区 → TrayNest”三级案例展示新增的自定义收纳层。程序员和游戏影音案例为3:6:5，人事为2:4:3近似比例，隐藏区计数包含TrayNest入口。外层和个人筛选层都有用途分类，输入法只出现在个人筛选层；不将收纳限定为不常用工具。用户提供的截图只用于理解背景，未纳入任何公开图片。

## 分层与重建

`promo/readme/layers/`保留每张完整SVG，以及背景、产品控件、品牌／卡片、文字的独立SVG图层。原生产品素材在layers/product/，AI背景原图为layers/background-ai.png。图层为透明底，文字可在矢量编辑器中修改；当前SVG还内嵌了引用图像，移走文件也不会丢图。

主合成工程为 `promo/readme/compose.cjs`，运行 `node promo/readme/compose.cjs` 可重建全部成图与本地预览 `promo/readme/index.html`。竖版子工程为review/personas/render-portrait.cjs，使用同目录冻结的assets和captures中的panel.png；保留完整SVG及背景、界面、文字三个独立图层。渲染使用项目内@resvg/resvg-js 2.6.2与本机Microsoft YaHei UI／Segoe UI，未分发字体文件。

产品截图由capture/ReadmeCapture.csproj编译当前生产TrayPanel，4倍逻辑分辨率绘制，图标16px／格位40px、固定5列。只使用独立图标夹具，未采集用户桌面、配置、窗口标题或真实托盘。排序、淡色和等待状态为生产控件上的演示状态。系统隐藏区为额外拼接的示意层，含TrayNest与通用音量／网络符号，收纳图标没有重复画进系统隐藏区。

## AI背景

使用内置imagegen生成背景，无文字、品牌、界面和人物；排版、真实产品层与品牌图标在生成后独立叠加。未调用外部付费API或要求密钥。

最终提示词：

> Use case: ads-marketing. Asset type: background-only layer for a premium GitHub README product hero for TrayNest, a lightweight Windows tray organizer. Create a wide 16:9 landscape abstract studio environment. Very dark midnight navy (#081120) dominates, with a restrained cobalt blue volumetric light bloom and pale cyan rim lighting concentrated on the far right, a few large translucent frosted glass folded rectangles receding into depth on the rightmost third, suggesting nesting and organization. Lower right subtle soft reflections; left 55 percent is nearly flat deep navy with clean negative space for typography which will be composited later. Elegant quiet desktop software art direction, editorial product photography lighting, refined physically plausible frosted glass, extremely subtle fine grain, crisp but calm. Keep center clear for a product screenshot overlay. No text, no letters, no logos, no icons, no UI, no people, no laptop, no phone, no watermark, no busy small details. Output one single seamless background, not a collage or grid.

## 公开图标来源

沿用项目宣传视频中已经冻结的公开素材，本轮冻结清单为promo/readme/icon-sources.json；历史来源和原始哈希另见promo/traynest-4x3/public/asset-manifest.json及public/icons/common/manifest.json。本轮不从用户电脑提取任何软件图标。

- qBittorrent：[官方仓库](https://github.com/qbittorrent/qBittorrent/blob/master/src/icons/qbittorrent.ico)
- ShareX：[官方资源](https://github.com/ShareX/ShareX/blob/develop/ShareX.HelpersLib/Resources/ShareX_Logo.png)
- LocalSend：[官方资源](https://github.com/localsend/localsend/blob/main/app/assets/img/logo-256.png)
- KeePassXC：[官方资源](https://github.com/keepassxreboot/keepassxc/blob/develop/share/icons/application/256x256/apps/keepassxc.png)
- OBS Studio：[官方资源](https://github.com/obsproject/obs-studio/blob/master/frontend/cmake/linux/icons/obs-logo-256.png)
- Bitwarden、Syncthing、AutoHotkey、RustDesk、Tailscale：项目已下载的[Simple Icons公开品牌符号](https://github.com/simple-icons/simple-icons)，规范化底色和尺寸沿用既有演示工程。

人群案例还使用QQ、微信、企业微信、腾讯会议、百度网盘、火绒、Steam、迅雷、搜狗输入法、Docker、PowerToys、Snipaste、QQ音乐、网易云音乐、VLC和Discord等公开标识，具体来源见[案例来源](review/personas/说明与来源.md)及[常用软件图标来源](review/SOURCES.md)。

这些名称及标志属于各自作者，仅用于识别工具与演示收纳概念，不表示合作、背书或逐项兼容认证。TrayNest新标识为本项目依据logo-generator技能创作的几何图标。

## 视觉检查

成图保留独立图层；README图片路径使用相对地址并提供中文替代文本。主图背景采用既有AI生成素材，人群案例／功能／位置图采用独立矢量背景。三个竖版案例均已查看。没有把生成背景当作软件界面，也没有把示意层宣称为用户桌面录像。
