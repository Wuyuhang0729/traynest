// Rebuild with: node scripts/Build-Icons.cjs
// Renderer is project-local: npm install --prefix .tools/logo-render @resvg/resvg-js
const fs = require('node:fs');
const path = require('node:path');
const {Resvg} = require('../.tools/logo-render/node_modules/@resvg/resvg-js');
const root = path.resolve(__dirname, '..');
const out = path.join(root, 'assets/branding');
fs.mkdirSync(path.join(out, 'variants'), {recursive:true});
const svg = (body, color='#2563eb') => `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100" color="${color}">${body}</svg>`;
const nested = '<rect x="24" y="24" width="44" height="44" rx="10" fill="none" stroke="currentColor" stroke-width="8"/><rect x="47" y="47" width="29" height="29" rx="7" fill="#2563eb" stroke="#a5e6ff" stroke-width="7"/>';
const variants = [
  ['nested','嵌套收纳','采用：两个错位圆角方框，直接表达把托盘图标收入独立面板；小尺寸保持清楚。',nested],
  ['drawer','归入抽屉','开口容器与落入的方块，以收纳动作建立识别。','<path d="M22 44v24q0 10 10 10h36q10 0 10-10V44M22 48h18l6 10h8l6-10h18" fill="none" stroke="currentColor" stroke-width="7" stroke-linejoin="round"/><rect x="41" y="20" width="18" height="18" rx="5" fill="currentColor"/>'],
  ['grid','一格归位','四个模块中以一个容器框建立层次，突出固定工具格位。','<rect x="20" y="20" width="60" height="60" rx="15" fill="none" stroke="currentColor" stroke-width="7"/><g fill="currentColor"><rect x="34" y="34" width="12" height="12" rx="3"/><rect x="54" y="34" width="12" height="12" rx="3"/><rect x="34" y="54" width="12" height="12" rx="3"/><rect x="54" y="54" width="12" height="12" rx="3"/></g>'],
  ['fold','折叠面板','两层折页构成简洁N形，强调展开与收起。','<path d="M23 76V25l27 18V25l27 18v33L50 58v18L23 58" fill="none" stroke="currentColor" stroke-width="8" stroke-linecap="round" stroke-linejoin="round"/>'],
  ['orbit','围合','开放圆环围住中心模块，表达图标聚合。','<path d="M71 25a31 31 0 1 0 9 28" fill="none" stroke="currentColor" stroke-width="8" stroke-linecap="round"/><rect x="40" y="40" width="21" height="21" rx="6" fill="currentColor"/><circle cx="78" cy="30" r="6" fill="currentColor"/>'],
  ['stack','轻叠','两片卡片叠放，以负空间保留窗口层级。','<rect x="22" y="22" width="43" height="43" rx="10" fill="none" stroke="currentColor" stroke-width="7"/><path d="M76 39v27q0 10-10 10H39" fill="none" stroke="currentColor" stroke-width="8" stroke-linecap="round"/>'],
];
for (const [id,,,body] of variants) fs.writeFileSync(path.join(out,`variants/${id}.svg`),svg(body));
const app = svg(`<rect x="6" y="6" width="88" height="88" rx="22" fill="#2563eb"/><g color="#ffffff">${nested}</g>`);
const mono = color => svg('<rect x="24" y="24" width="44" height="44" rx="10" fill="none" stroke="currentColor" stroke-width="8"/><rect x="47" y="47" width="29" height="29" rx="7" fill="none" stroke="currentColor" stroke-width="7"/>',color);
fs.writeFileSync(path.join(out,'traynest-app.svg'),app);
fs.writeFileSync(path.join(out,'traynest-mark-dark.svg'),mono('#172238'));
fs.writeFileSync(path.join(out,'traynest-mark-light.svg'),mono('#ffffff'));
const sizes=[16,20,24,32,40,48,64,96,128,256,512,1024];
const frames=[];
for(const size of sizes){
  const png=new Resvg(app,{fitTo:{mode:'width',value:size}}).render().asPng();
  fs.writeFileSync(path.join(out,`traynest-${size}.png`),png);
  if(size<=256)frames.push({size,png});
}
// ICO directory with 32-bit alpha PNG frames, supported by Windows Vista and later.
const header=Buffer.alloc(6+16*frames.length);header.writeUInt16LE(1,2);header.writeUInt16LE(frames.length,4);
let offset=header.length;
frames.forEach(({size,png},i)=>{const at=6+16*i;header[at]=size===256?0:size;header[at+1]=header[at];header.writeUInt16LE(1,at+4);header.writeUInt16LE(32,at+6);header.writeUInt32LE(png.length,at+8);header.writeUInt32LE(offset,at+12);offset+=png.length;});
const ico=Buffer.concat([header,...frames.map(frame=>frame.png)]);
fs.writeFileSync(path.join(out,'TrayNest.ico'),ico);
fs.copyFileSync(path.join(out,'TrayNest.ico'),path.join(root,'src/NestedTray/Resources/TrayNest.ico'));
fs.copyFileSync(path.join(out,'TrayNest.ico'),path.join(out,'favicon.ico'));
const cards=variants.map(([id,title,reason,body],i)=>`<article><div class="canvas">${svg(body)}</div><small>0${i+1} / ${id}</small><h2>${title}${i===0?' · 已应用':''}</h2><p>${reason}</p><a href="variants/${id}.svg" download>下载 SVG</a></article>`).join('');
const stages=[['void','深色对比'],['paper','浅色对比'],['container','应用容器'],['swiss','纯色对比']].map(([id,label])=>`<div class="stage ${id}"><img src="traynest-256.png" alt="TrayNest正式应用图标"><span>${label}</span></div>`).join('');
fs.writeFileSync(path.join(out,'index.html'),`<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>TrayNest · 图标规范</title><style>*{box-sizing:border-box}body{margin:0;background:#f6f7f9;color:#172238;font:15px/1.6 system-ui}main{max-width:1120px;margin:auto;padding:64px 32px}header{padding-bottom:36px;border-bottom:1px solid #ddd}h1{font-size:48px;letter-spacing:-2px;margin:0}header p{color:#637087}button,a{color:inherit}button{padding:10px 16px;margin-right:8px;border:1px solid #ccc;border-radius:8px;cursor:pointer}.grid{display:grid;grid-template-columns:repeat(3,1fr);gap:24px;margin:36px 0}article{background:#fff;padding:26px;border-radius:16px;transition:transform .2s}article:hover{transform:translateY(-4px)}.canvas{display:grid;place-items:center;height:180px}.canvas svg{width:140px}h2{font-size:20px}small{color:#748095;font:12px monospace}p{color:#637087}body.dark{background:#101620;color:#f1f5ff}body.dark article{background:#1b2534}body.dark .canvas{background:#fff;border-radius:12px}.stages{display:grid;grid-template-columns:repeat(4,1fr);gap:16px}.stage{height:220px;display:flex;align-items:center;justify-content:center;flex-direction:column;border-radius:14px;gap:18px}.stage img{width:96px;height:96px}.stage span{font-size:12px}.void{background:#101620;color:white}.paper{background:#f4efe5}.container{background:#dce9fb}.swiss{background:#153d35;color:white}.sizes{display:flex;gap:22px;align-items:center;padding:26px;background:white;border-radius:12px;color:#172238}.sizes img{image-rendering:auto}footer{padding-top:40px;color:#748095}@media(max-width:720px){.grid,.stages{grid-template-columns:1fr 1fr}}</style><main><header><small>TRAYNEST / BRAND ASSETS / 2026</small><h1>小图标，也有归处。</h1><p>蓝色嵌套方框 · SVG / PNG / Windows ICO</p><button onclick="document.body.classList.toggle('dark')">切换深浅背景</button><a href="TrayNest.ico" download>下载 Windows 图标</a></header><section class="grid">${cards}</section><h2>实际尺寸</h2><div class="sizes">${[16,20,24,32,48,64].map(n=>`<div><img width="${n}" height="${n}" src="traynest-${n}.png" alt="${n}像素图标"><br><small>${n}px</small></div>`).join('')}</div><h2>背景适配</h2><section class="stages">${stages}</section><footer>依据 op7418/logo-generator-skill 的几何、留白、尺度和六方案探索方法制作。选择嵌套方案并直接应用；展示采用本地SVG/PNG，无外部API生成背景。</footer></main></html>`);
fs.writeFileSync(path.join(out,'manifest.json'),JSON.stringify({sourceSkill:'https://github.com/op7418/logo-generator-skill',selected:'nested',sizes,icoFrames:frames.map(f=>f.size),palette:{blue:'#2563eb',accent:'#a5e6ff',white:'#ffffff'},renderer:'@resvg/resvg-js',usage:'ApplicationIcon + embedded tray/window icon + desktop shortcut'},null,2));
console.log(`Generated ${sizes.length} PNGs, ${frames.length}-frame ICO, SVG variants and local showcase.`);
