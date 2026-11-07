# TrayNest preview.12 宣传页与图层

制作日期：2026-11-07。本次按当前迭代重做四张宣传图，README 不再列举三个典型人群案例。旧版工程保留归档。

## 输出

- assets/readme/hero-v2.png：1600×1000，产品主视觉。
- assets/readme/workflow-v2.png：1600×960，同一批图标的收纳前后对比。
- assets/readme/control-v2.png：1600×1020，布局记忆与互斥展开方式。
- assets/readme/appearance-v2.png：1600×1080，双面板材质与真实设置开关。

本目录 index.html 为静态宣传页，可通过 HTTP 服务打开；不需要前端构建。layers/ 保留完整 SVG，以及背景、界面、文字、卡片和品牌等独立 SVG 图层。图片内的文字来自矢量排版，可独立修改；字体不随包分发。

## 制作方式

沿用背景、产品界面、文案分别制作再合成的方法。背景使用内置 image_gen 生成；产品收纳面板与设置来自本版本生产代码的隔离绘制；文字、布局、系统隐藏区示意和材质组合由 compose.cjs 合成。没有使用用户桌面、个人软件列表、设置或截图。

capture/ReadmeCaptureV2.csproj 直接编译 src/NestedTray 当前生产控件。面板按 4 倍逻辑尺寸绘制，16 像素图标／40 像素格位、固定 5 列。captures/provenance.json 记录版本、公开演示图标与状态。淡色格位、拖动高亮以及设置中的同步开启为演示状态，不会连接真实 Explorer。系统隐藏区和任务栏是矢量示意，亚克力不是实拍材质截图；真实材质桥接已单独在目标系统验证。

收纳前后使用同一批图标，收纳面板、系统隐藏区与任务栏之间不重复分配应用；收纳图标由用户手动选择，不表示软件提供自动分类或多分类页。QQ、微信、搜狗输入法用于任务栏常用入口示意，其余选用百度网盘、迅雷、腾讯会议、企业微信、网易云音乐、QQ音乐等国内用户熟悉的应用，以及 Snipaste、Everything、OBS、OneDrive、Steam 等工具。

## 重建

先用 .NET 10 SDK 构建 capture/ReadmeCaptureV2.csproj，再运行生成的 EXE，参数依次为输出 captures/ 目录及 ../review/personas/assets/ 图标目录。截图使用独立测试配置，不接管系统托盘。

安装 @resvg/resvg-js 2.6.2 到项目 .tools/logo-render/ 后，在仓库根目录运行 `node promo/readme/v2/compose.cjs` 可重新生成 PNG、完整 SVG、独立图层、manifest.json 和 HTML。渲染使用 Microsoft YaHei UI 与 Segoe UI。

## 新背景提示词

使用内置 image_gen 生成，输出为单张无文字背景。原图保存在 layers/background-ai.png。

> Use case: ads-marketing. Asset: background-only plate for a NEW editorial product launch page for TrayNest, a Windows 11 tray icon panel manager. Wide landscape 16:10. Premium Apple-inspired studio art direction, physically plausible light and spatial depth, pearl-white and very pale ice-blue environment, a gently curved matte architectural plinth and two large very restrained translucent frosted glass planes receding on the far right, soft blue refracted caustics near the bottom right and elegant diffused daylight from the upper left. Sophisticated minimal product photography set. The left 55 percent must remain very clean nearly flat off-white negative space for later dark typography. The center right is also calm enough for actual software panels to be composited later; glass planes mostly at rightmost edge. Restrained cobalt-blue accent only in light reflections, delicate contact shadows, high-quality believable materials and space, no decorative icon confetti, no strong gradients across the typography area. This image is ONLY an atmosphere background, not an infographic or a screenshot. Absolutely no text, letters, logos, brand marks, software UI, icons, devices, people, hands, watermarks, frame borders, collages, or multiple panels. Produce one seamless landscape image.

## 来源与权利

沿用已冻结的公开软件标识，仅转换格式，不重新绘制第三方标识。来源见 [常用图标来源](../review/SOURCES.md)、[补充来源](../review/personas/说明与来源.md)及[旧版制作说明](../CREDITS.md)。本轮使用的 PNG 独立保存在 assets/。

这些名称和标识属于各自作者，仅为产品演示，不代表合作、背书或逐项兼容认证。TrayNest 标识沿用本项目正式几何图标，本次没有更换品牌标识。
