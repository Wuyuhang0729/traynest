# TrayNest 标准图标

采用蓝色嵌套圆角方框，延续托盘收纳的产品意象。根据 [logo-generator](https://github.com/op7418/logo-generator-skill) 的简洁几何、尺度适配与六方案探索方法制作，已选择并应用 nested 方案，无需再等待选型。

- `traynest-app.svg`：可编辑应用主图标；`traynest-mark-dark.svg`／`traynest-mark-light.svg`：深浅单色标识。
- `traynest-16.png` 至 `traynest-1024.png`：12种尺寸透明PNG。
- `TrayNest.ico`：Windows正式应用图标，10个尺寸，最大256像素；`favicon.ico`：相同多尺寸资源。
- `variants/`：6个不同结构方向的SVG；`index.html`：本地方案对比、实际尺寸和4种背景预览，支持切换深浅背景。
- `manifest.json`：配色、尺寸和资源用途。

重建：项目根目录运行 `node scripts/Build-Icons.cjs`。渲染器为项目内 `@resvg/resvg-js` 2.6.2；当前应用使用代码生成的SVG和本地矢量渲染，没有调用Gemini或其他付费图片API，也不需要提供密钥。

应用位置：EXE的ApplicationIcon、托盘入口、面板、诊断窗口、批量操作确认窗口及桌面快捷方式。桌面快捷方式显式引用发布目录中的 `TrayNest.ico`；发布包附有创建快捷方式脚本。
