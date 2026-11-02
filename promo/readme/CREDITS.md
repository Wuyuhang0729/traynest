# README 宣传图与分层工程

制作日期：2026-11-02；文案与使用场景示意更新于2026-11-03。四张成图位于 `assets/readme/`：hero（1600×900）、hierarchy（1600×960）、features（1600×1030）、placement（1600×980）。README使用相对路径引用PNG，GitHub无需运行脚本即可显示。图片展示本地开发版，公开下载包以Release说明为准。

宣传先说明任务栏右下角空间有限、需要优先留给最重要的图标，再说明隐藏区里常用与不常用图标混在一起，最后介绍TrayNest的额外收纳空间。用户提供的任务栏截图只用于理解这个背景，未纳入任何公开图片；图中的任务栏和系统隐藏区均单独绘制为示意。

## 分层与重建

`promo/readme/layers/`保留每张完整SVG，以及背景、产品控件、品牌／卡片、文字的独立SVG图层。原生产品素材在layers/product/，AI背景原图为layers/background-ai.png。图层为透明底，文字可在矢量编辑器中修改；当前SVG还内嵌了引用图像，移走文件也不会丢图。

主合成工程为 `promo/readme/compose.cjs`，运行 `node promo/readme/compose.cjs` 可重建三张图与本地预览 `promo/readme/index.html`。渲染使用项目内@resvg/resvg-js 2.6.2与本机Microsoft YaHei UI／Segoe UI，未分发字体文件。

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

这些名称及标志属于各自作者，仅用于识别工具与演示收纳概念，不表示合作、背书或逐项兼容认证。TrayNest新标识为本项目依据logo-generator技能创作的几何图标。

## 视觉检查

四张1600px成图保留独立图层；README图片路径使用相对地址并提供中文替代文本。主图背景采用既有AI生成素材，使用场景／功能／位置图采用独立矢量背景。没有把生成背景当作软件界面，也没有把示意层宣称为用户桌面录像。
