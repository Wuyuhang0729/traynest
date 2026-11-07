const fs = require('node:fs');
const path = require('node:path');
const { Resvg } = require('../../../.tools/logo-render/node_modules/@resvg/resvg-js');
const root = path.resolve(__dirname, '../../..');
const output = path.join(root, 'assets/readme');
const layers = path.join(__dirname, 'layers');
const captures = path.join(__dirname, 'captures');
const assets = path.join(__dirname, 'assets');
fs.mkdirSync(output, { recursive: true });
const font = 'Microsoft YaHei UI, Segoe UI, sans-serif';
const colors = { ink: '#102b46', muted: '#526d88', blue: '#1865da', border: '#d5e2ee' };
const escape = value => String(value).replaceAll('&', '&amp;').replaceAll('<', '&lt;');
const t = (value, x, y, size = 26, color = colors.ink, weight = 400) => `<text x="${x}" y="${y}" fill="${color}" font-family="${font}" font-size="${size}" font-weight="${weight}">${escape(value)}</text>`;
const r = (x, y, w, h, radius, fill, stroke = 'none') => `<rect x="${x}" y="${y}" width="${w}" height="${h}" rx="${radius}" fill="${fill}" stroke="${stroke}"/>`;
const image = (file, x, y, w, h, extra = '') => `<image href="data:image/png;base64,${fs.readFileSync(file).toString('base64')}" x="${x}" y="${y}" width="${w}" height="${h}" ${extra}/>`;
const icon = (id, x, y, size) => image(path.join(assets, id + '.png'), x, y, size, size);
const defs = `<defs><filter id="shadow" x="-40%" y="-60%" width="180%" height="240%"><feDropShadow dx="0" dy="18" stdDeviation="22" flood-color="#123456" flood-opacity=".13"/></filter><linearGradient id="pale" x2="1" y2="1"><stop stop-color="#f7fbff"/><stop offset="1" stop-color="#e7f0fb"/></linearGradient><linearGradient id="dark" x2="1" y2="1"><stop stop-color="#0a1829"/><stop offset="1" stop-color="#18364c"/></linearGradient><linearGradient id="glass" x2=".7" y2="1"><stop stop-color="#f7fcff" stop-opacity=".87"/><stop offset="1" stop-color="#e4f0fb" stop-opacity=".65"/></linearGradient></defs>`;
const selected = ['baidupan', 'xunlei', 'snipaste', 'everything', 'tencent-meeting', 'netease-music', 'qqmusic', 'obs', 'sunlogin', 'wecom'];
const native = ['traynest', 'huorong', 'onedrive', 'steam'];
const task = ['sogou', 'qq', 'wechat'];
function panel(name, x, y, w, shadow = true) {
  const h = w * 352 / 832;
  const clip = 'clip-' + name + '-' + x + '-' + y;
  return `<g ${shadow ? 'filter="url(#shadow)"' : ''}>${r(x, y, w, h, 19, '#fafafa', '#c9d7e4')}<defs><clipPath id="${clip}">${r(x + 1, y + 1, w - 2, h - 2, 18, 'white')}</clipPath></defs>${image(path.join(captures, name + '.png'), x, y, w, h, `clip-path="url(#${clip})"`)}</g>`;
}
function nativePanel(ids, x, y, cell = 68, columns = 5, mode = 'light') {
  const cols = Math.min(ids.length, columns), rows = Math.ceil(ids.length / columns), pad = cell / 10;
  const w = cols * cell + pad * 2, h = rows * cell + pad * 2;
  const fill = mode === 'dark' ? '#202228' : mode === 'glass' ? 'url(#glass)' : '#fafafa';
  return `<g filter="url(#shadow)">${r(x, y, w, h, 18, fill, mode === 'dark' ? '#485667' : '#c9d7e4')}${ids.map((id, i) => icon(id, x + pad + (i % columns) * cell + cell * .3, y + pad + Math.floor(i / columns) * cell + cell * .3, cell * .4)).join('')}</g>`;
}
function taskbar(x, y, w = 520, dark = false) {
  return r(x, y, w, 72, 18, dark ? '#203c52' : '#dce7f1aa', dark ? '#3f6079' : '#c8d8e8') + task.map((id, i) => icon(id, x + 26 + i * 52, y + 21, 30)).join('') + t('⌃', x + 200, y + 47, 34, dark ? '#e9f3ff' : '#476680') + t('09:41', x + w - 99, y + 44, 24, dark ? '#c0d2e5' : '#526d88');
}
function emit(name, h, groups) {
  const wrap = code => `<svg xmlns="http://www.w3.org/2000/svg" width="1600" height="${h}" viewBox="0 0 1600 ${h}">${defs}${code}</svg>`;
  const svg = wrap(groups.map(([id, body]) => `<g id="layer-${id}" data-name="${id}">${body}</g>`).join(''));
  fs.writeFileSync(path.join(layers, name + '.svg'), svg);
  fs.writeFileSync(path.join(output, name + '.png'), new Resvg(svg, { font: { loadSystemFonts: true, defaultFontFamily: 'Microsoft YaHei UI' } }).render().asPng());
  for (const [id, body] of groups) fs.writeFileSync(path.join(layers, `${name}.${id}.svg`), wrap(body));
  console.log(name);
}

emit('hero-v2', 1000, [
  ['background', image(path.join(layers, 'background-ai.png'), 0, 0, 1600, 1000)],
  ['brand', icon('traynest', 82, 70, 62) + t('TrayNest', 163, 113, 35, colors.ink, 700) + t('Windows 11 · 图标面板管理工具', 86, 159, 22, colors.muted)],
  ['product', panel('panel-light', 786, 422, 400) + nativePanel(native, 1203, 502.43, 74, 4) + taskbar(984, 678, 520)],
  ['typography', t('图标多，', 80, 310, 83, colors.ink, 700) + t('也能管理。', 80, 414, 83, colors.blue, 700) + t('从系统隐藏区挑一组，单独收好。', 86, 493, 31) + t('位置记得住，展开按习惯，外观也统一。', 86, 548, 26, colors.muted) + t('自己挑选 · 拖动排序 · 记住布局', 86, 640, 23, colors.blue, 600) + t('收纳面板', 789, 387, 24, colors.blue, 600) + t('系统隐藏区', 1204, 483, 23, colors.muted) + t('QQ、微信等常用入口，继续留在任务栏', 986, 800, 23, colors.muted) + t('整洁、高效、定制', 86, 862, 31, colors.ink, 600) + t('0.2.0-preview.12  /  开源预览版  /  无需另外安装 .NET', 86, 927, 20, colors.muted)],
]);

// The same icon set is partitioned before/after. No fabricated category tabs.
const before = [...selected.slice(0, 4), ...native.slice(1), ...selected.slice(4), 'traynest'];
if (new Set(before).size !== before.length || before.length !== selected.length + native.length) throw new Error('Icon partition mismatch');
emit('workflow-v2', 960, [
  ['background', r(0, 0, 1600, 960, 0, 'url(#pale)')],
  ['cards', r(64, 258, 660, 580, 30, 'white', colors.border) + r(754, 258, 782, 580, 30, '#edf5ff', colors.border)],
  ['product', nativePanel(before, 170, 393, 83, 5) + panel('panel-light', 802, 459, 392) + nativePanel(native, 1225, 544.45, 67, 4) + taskbar(980, 686, 494)],
  ['typography', t('隐藏区也挤了？再添一个自选空间。', 70, 129, 58, colors.ink, 700) + t('消息、网盘、后台工具混在一起。把想放在一起的图标，拖进 TrayNest。', 75, 191, 26, colors.muted) + t('原来', 101, 325, 32, colors.muted, 600) + t('加上 TrayNest', 793, 325, 32, colors.blue, 600) + t('一个隐藏区，所有图标混在一起', 102, 780, 25, colors.muted) + t('收纳面板', 805, 419, 22, colors.blue, 600) + t('系统隐藏区', 1228, 499, 21, colors.muted) + t('选哪些、怎么排，由你决定', 793, 780, 26) + t('拖进来收纳  →  面板内排序  →  拖出去移回', 76, 905, 26, colors.blue, 600)],
]);

emit('control-v2', 1020, [
  ['background', r(0, 0, 1600, 1020, 0, '#f7f9fc')],
  ['cards', r(64, 249, 714, 662, 28, 'white', colors.border) + r(806, 249, 730, 662, 28, 'white', colors.border)],
  ['product', panel('panel-offline', 110, 481, 620) + `<svg x="842" y="421" width="654" height="195" viewBox="238 165 640 190">${image(path.join(captures, 'settings-general.png'), 0, 0, 936, 719)}</svg>`],
  ['typography', t('记住你的布局，也照顾你的习惯。', 70, 126, 60, colors.ink, 700) + t('重启电脑，布局不变。需要时，按你选择的方式展开。', 76, 189, 27, colors.muted) + t('01 / 位置记忆', 105, 315, 22, colors.blue, 600) + t('未启动，也可点击打开', 105, 382, 39, colors.ink, 700) + t('淡色格位保留原位置，点击尝试启动。', 110, 445, 24, colors.muted) + t('02 / 自动展开', 845, 315, 22, colors.blue, 600) + t('展开方式，三选一', 845, 382, 39, colors.ink, 700) + t('手动展开  /  跟随系统隐藏区  /  悬停入口', 848, 751, 24, colors.blue, 600) + t('只启用一种，避免重复触发。', 849, 800, 24, colors.muted) + t('单击、双击、右键，继续使用原软件。', 110, 819, 24) + t('可指定 EXE 或快捷方式；启动结果取决于原软件。', 110, 858, 20, colors.muted) + t('独立设置窗口，集中调整位置、每行图标数、外观与开机启动。', 76, 978, 25, colors.muted)],
]);

// Acrylic is an explicit visual illustration; the real native implementation is tested separately.
function materialPair(x, y, mode) {
  const bg = mode === 'dark' ? '#202228' : mode === 'glass' ? 'url(#glass)' : '#fafafa';
  const selectedSubset = selected.slice(0, 5);
  return nativePanel(selectedSubset, x, y + 108, 63, 5, mode) + nativePanel(native, x + 340, y + 108, 63, 4, mode) + t('收纳面板', x + 5, y + 82, 20, mode !== 'light' ? '#b8cde1' : colors.muted) + t('系统隐藏区', x + 345, y + 82, 20, mode !== 'light' ? '#b8cde1' : colors.muted);
}
emit('appearance-v2', 1080, [
  ['background', r(0, 0, 1600, 1080, 0, 'url(#dark)')],
  ['cards', r(64, 272, 706, 600, 28, '#f3f7fb') + r(798, 272, 738, 600, 28, '#152e44', '#416078')],
  ['product', materialPair(107, 343, 'light') + materialPair(842, 343, 'dark') + `<svg x="95" y="669" width="642" height="153" viewBox="239 576 637 87">${image(path.join(captures, 'settings-panel.png'), 0, 0, 936, 719)}</svg>` + materialPair(842, 606, 'glass')],
  ['typography', t('两个面板，一起换个外观。', 70, 126, 64, '#f1f7ff', 700) + t('在设置中开启材质同步，让系统隐藏区与收纳面板采用对应外观。', 76, 192, 27, '#b5cce1') + t('浅色', 106, 339, 36, colors.ink, 700) + t('深色', 842, 339, 36, '#edf5ff', 700) + t('亚克力 · 随背景透光', 842, 602, 29, '#b6daff', 600) + t('关闭同步或退出，恢复系统原背景。', 77, 954, 31, '#eff6ff', 600) + t('左侧底部为当前设置界面；材质组合为示意，透光细节随背景与系统设置变化。', 77, 1010, 21, '#99b4ce')],
]);

const sections = [
  ['hero-v2', '图标多，也能管理', 'TrayNest · Windows 11 图标面板管理工具'],
  ['workflow-v2', '给隐藏区再添一个自选空间', '同一组图标的收纳前后对比'],
  ['control-v2', '记住布局，按习惯展开', '当前生产控件演示与独立设置窗口'],
  ['appearance-v2', '统一两个面板的外观', '浅色、深色与亚克力材质示意'],
];
fs.writeFileSync(path.join(__dirname, 'manifest.json'), JSON.stringify({ version: '0.2.0-preview.12', created: '2026-11-07', sections, selected, native, task, actualProduct: 'captures/provenance.json', nativePanels: 'vector illustration, not a recording of user desktop', acrylic: 'illustrated; actual implementation verified independently', background: 'built-in image_gen; full prompt in CREDITS.md', renderer: '@resvg/resvg-js 2.6.2', layers: ['background', 'cards', 'brand', 'product', 'typography'] }, null, 2));
const html = `<!doctype html><html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><meta name="description" content="TrayNest：从系统隐藏区挑一组图标，单独收好。支持拖动排序、布局记忆、按习惯展开与双面板材质同步。"><title>TrayNest · 图标多，也能管理</title><style>*{box-sizing:border-box}html{scroll-behavior:smooth}body{margin:0;background:#f1f5fa;color:#102b46;font:16px 'Microsoft YaHei UI','Segoe UI',sans-serif}header{max-width:1280px;margin:auto;padding:24px;display:flex;align-items:center;justify-content:space-between;gap:20px}header strong{display:flex;align-items:center;gap:10px;font-size:20px}header img{width:32px;height:32px}nav{display:flex;gap:20px;align-items:center}a{color:#1865da;text-decoration:none}.download{background:#1865da;color:white;padding:12px 20px;border-radius:24px}main{max-width:1280px;margin:auto}figure{margin:0 0 32px;background:white;border-radius:24px;overflow:hidden;box-shadow:0 12px 45px #142e4d0b}figure>img{width:100%;height:auto;display:block}figcaption{padding:16px 28px;color:#526d88;font-size:14px;display:flex;justify-content:space-between;gap:20px}footer{max-width:1280px;padding:24px 28px 60px;margin:auto;color:#526d88;line-height:1.9}footer strong{color:#102b46}@media(max-width:700px){header{padding:16px}nav{gap:12px;font-size:13px}.source{display:none}main{padding:0 8px}figure{border-radius:14px;margin-bottom:16px}figcaption{padding:12px 16px;font-size:12px}figcaption span{display:none}}@media(prefers-reduced-motion:reduce){html{scroll-behavior:auto}}</style></head><body><header><strong><img src="assets/traynest.png" alt="">TrayNest</strong><nav><a class="source" href="https://github.com/Wuyuhang0729/traynest">GitHub</a><a href="#install">如何安装</a><a class="download" href="https://github.com/Wuyuhang0729/traynest/releases/tag/v0.2.0-preview.12">下载预览版</a></nav></header><main>${sections.map(([name, title, description], i) => `<figure><img ${i ? 'loading="lazy"' : 'fetchpriority="high"'} src="../../../assets/readme/${name}.png" alt="${title}：${description}"><figcaption><span>${description}</span><a href="layers/${name}.svg">可编辑 SVG ↗</a></figcaption></figure>`).join('')}</main><footer id="install"><strong>Windows 11 x64 · 开源预览版</strong><p>下载并完整解压，双击 Install.cmd。安装后从桌面打开 TrayNest，把想收纳的图标拖进面板。无需另外安装 .NET，默认不开启开机启动。</p><p>右键入口 → 设置。可选择展开方式，调整左侧／上方位置、5–8 列及材质。材质同步关闭后恢复系统原背景。</p><p>当前在 Windows 11 25H2 验证；第三方图标为公开演示素材，不代表逐项兼容认证。<a href="../../../docs/COMPATIBILITY.md">兼容性说明</a> · <a href="CREDITS.md">图层与素材说明</a> · <a href="https://github.com/Wuyuhang0729/traynest/issues/new?template=bug_report.yml">反馈问题</a></p></footer></body></html>`;
fs.writeFileSync(path.join(__dirname, 'index.html'), html);

// Small, single-purpose illustrations for the repository overview.
// The larger launch boards remain editable source material, outside the README flow.
const detailBackground = r(0, 0, 1600, 660, 0, '#f5f8fc');
emit('organize-detail', 660, [
  ['background', detailBackground],
  ['product', panel('panel-reorder', 180, 68, 1240)],
  ['gesture', '<path d="M828 323l0-103 73 73-40-1-18 39z" fill="#1765d9" stroke="white" stroke-width="8" stroke-linejoin="round"/>'],
]);
emit('remember-detail', 660, [
  ['background', detailBackground],
  ['product', panel('panel-offline', 180, 68, 1240)],
]);
emit('expansion-detail', 660, [
  ['background', detailBackground],
  ['product', `<svg x="315" y="190" width="970" height="182" viewBox="695 247 166 32">${image(path.join(captures, 'settings-general.png'), 0, 0, 936, 719)}</svg>`],
  ['typography', t('手动展开', 163, 501, 52, colors.muted) + t('跟随系统隐藏区', 570, 501, 52, colors.muted) + t('悬停入口', 1190, 501, 52, colors.blue, 600)],
]);
emit('material-detail', 660, [
  ['background', detailBackground],
  ['product', panel('panel-dark', 105, 154, 760) + nativePanel(native, 913, 323.14, 127, 4, 'dark')],
  ['typography', t('收纳面板', 110, 115, 43, colors.muted) + t('系统隐藏区', 915, 264, 43, colors.muted)],
]);
const compactSections = [
  ['organize-detail', '拖进来，排顺手', '拖入收纳，拖动排序，拖出移回。'],
  ['remember-detail', '重启电脑，布局不变', '未启动，也可点击打开。'],
  ['expansion-detail', '展开方式，三选一', '手动、跟随隐藏区或悬停入口。'],
  ['material-detail', '两个面板，材质统一', '浅色、深色、亚克力，按习惯选。'],
  ['interaction-detail', '原来的操作，照常使用', '单击、双击和右键菜单。'],
  ['placement-detail', '展开位置，由你决定', '左侧或上方，按边缘对齐。'],
];
emit('interaction-detail', 660, [
  ['background', detailBackground],
  ['product', `<svg x="180" y="135" width="1240" height="263" viewBox="0 0 832 176">${image(path.join(captures, 'panel-light.png'), 0, 0, 832, 352)}</svg>`],
  ['typography', t('单击', 310, 535, 58, colors.muted) + t('双击', 745, 535, 58, colors.muted) + t('右键菜单', 1115, 535, 58, colors.muted)],
]);
emit('placement-detail', 660, [
  ['background', detailBackground],
  ['product', panel('panel-light', 65, 340, 370) + nativePanel(native, 460, 412.54, 70, 4) + panel('panel-light', 950, 130, 370) + nativePanel(native, 950, 315, 70, 4)],
  ['typography', t('左侧 · 底部对齐', 65, 270, 42, colors.muted) + t('上方 · 左侧对齐', 950, 80, 42, colors.muted)],
]);
const manifestPath = path.join(__dirname, 'manifest.json');
const manifest = JSON.parse(fs.readFileSync(manifestPath));
manifest.compactSections = compactSections;
manifest.overview = 'README uses four small, single-purpose images; launch boards remain archived in this project.';
fs.writeFileSync(manifestPath, JSON.stringify(manifest, null, 2));
const compactHtml = `<!doctype html><html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>TrayNest · 图标多，也能管理</title><style>*{box-sizing:border-box}body{margin:0;color:#102b46;background:#f5f8fc;font:16px 'Microsoft YaHei UI','Segoe UI',sans-serif}header,main,footer{max-width:1100px;margin:auto;padding:24px}header{display:flex;align-items:center;justify-content:space-between;gap:16px}header strong{display:flex;align-items:center;gap:10px;font-size:22px}header img{width:32px;height:32px}a{color:#1865da;text-decoration:none}.download{background:#1865da;color:white;border-radius:24px;padding:12px 20px;white-space:nowrap}.hero{width:100%;display:block;border-radius:22px}.intro{font-size:21px;line-height:1.8;margin:28px 0 40px}.grid{display:grid;grid-template-columns:1fr 1fr;gap:24px}article{background:white;border:1px solid #e3eaf2;border-radius:22px;overflow:hidden}article img{width:100%;display:block}article h2{font-size:22px;margin:22px 24px 8px}article p{color:#526d88;margin:0 24px 24px;line-height:1.7}footer{color:#526d88;line-height:1.9;padding-bottom:50px}@media(max-width:650px){header,main,footer{padding:16px}header strong{font-size:18px}.download{padding:10px 14px;font-size:14px}.intro{font-size:17px}.grid{grid-template-columns:1fr;gap:16px}article h2{font-size:20px}.hero{border-radius:14px}}</style></head><body><header><strong><img src="assets/traynest.png" alt="">TrayNest</strong><a class="download" href="https://github.com/Wuyuhang0729/traynest/releases/tag/v0.2.0-preview.12">下载预览版</a></header><main><img class="hero" src="../../../assets/readme/hero-v2.png" alt="TrayNest：图标多，也能管理"><p class="intro">从系统隐藏区挑一组图标，单独收好。位置记得住，展开按习惯，外观也统一。</p><div class="grid">${compactSections.map(([name,title,caption])=>`<article><h2>${title}</h2><img src="../../../assets/readme/${name}.png" alt="${title}"><p>${caption}</p></article>`).join('')}</div></main><footer>Windows 11 x64 · 开源预览版。完整解压后双击 Install.cmd，从桌面打开即可。<br><a href="https://github.com/Wuyuhang0729/traynest">GitHub 与使用说明</a> · <a href="CREDITS.md">可编辑图层与素材来源</a></footer></body></html>`;
fs.writeFileSync(path.join(__dirname, 'index.html'), compactHtml);


