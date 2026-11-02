const fs=require('node:fs');
const path=require('node:path');
const {Resvg}=require('../../.tools/logo-render/node_modules/@resvg/resvg-js');
const root=path.resolve(__dirname,'../..'),output=path.join(root,'assets/readme'),layers=path.join(__dirname,'layers');
fs.mkdirSync(output,{recursive:true});
const data=p=>'data:image/png;base64,'+fs.readFileSync(p).toString('base64');
const pic=(p,x,y,w,h,extra='')=>`<image href="${data(p)}" x="${x}" y="${y}" width="${w}" height="${h}" ${extra}/>`;
const product=name=>path.join(layers,'product',name+'.png');
const logo=path.join(root,'assets/branding/traynest-256.png');
const font='Microsoft YaHei UI, Microsoft YaHei, Segoe UI, sans-serif';
const text=(s,x,y,size=26,color='#172238',weight=400)=>`<text x="${x}" y="${y}" font-family="${font}" font-size="${size}" font-weight="${weight}" fill="${color}">${s}</text>`;
const rect=(x,y,w,h,r,fill,stroke='none')=>`<rect x="${x}" y="${y}" width="${w}" height="${h}" rx="${r}" fill="${fill}" stroke="${stroke}"/>`;
const defs=`<defs><filter id="shadow" x="-30%" y="-60%" width="160%" height="240%"><feDropShadow dx="0" dy="22" stdDeviation="28" flood-color="#000000" flood-opacity=".25"/></filter><linearGradient id="light" x2="1" y2="1"><stop stop-color="#f6f9ff"/><stop offset="1" stop-color="#e8eef9"/></linearGradient></defs>`;
function panel(name,x,y,w,shadow=true){const h=w*352/832;return `<g ${shadow?'filter="url(#shadow)"':''}>${rect(x-2,y-2,w+4,h+4,20,'#202228','#586278')}<svg x="${x}" y="${y}" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}"><defs><clipPath id="p${x}${y}">${rect(0,0,w,h,18,'white')}</clipPath></defs>${pic(product(name),0,0,w,h,`clip-path="url(#p${x}${y})"`)}</svg></g>`;}
function emit(name,height,groups){
 const body=groups.map(([id,code])=>`<g id="layer-${id}" data-name="${id}">${code}</g>`).join('');
 const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="1600" height="${height}" viewBox="0 0 1600 ${height}">${defs}${body}</svg>`;
 fs.writeFileSync(path.join(layers,name+'.svg'),svg);
 fs.writeFileSync(path.join(output,name+'.png'),new Resvg(svg,{font:{loadSystemFonts:true,defaultFontFamily:'Microsoft YaHei UI'}}).render().asPng());
 for(const [id,code]of groups)fs.writeFileSync(path.join(layers,`${name}.${id}.svg`),`<svg xmlns="http://www.w3.org/2000/svg" width="1600" height="${height}" viewBox="0 0 1600 ${height}">${defs}${code}</svg>`);
 console.log(name);
}
let heroBg=pic(path.join(layers,'background-ai.png'),0,0,1600,900)+rect(0,0,1600,900,0,'#06101b22');
let heroBrand=pic(logo,80,70,76,76)+text('TrayNest',174,111,36,'#ffffff',600)+text('托盘收纳盒',176,144,19,'#9eafc6');
let heroType=text('任务栏的位置有限，得留给最重要的。',89,235,25,'#9fb9d8')+text('隐藏图标太多？',84,325,70,'#ffffff',700)+text('不常用的，再收一层。',84,421,62,'#ffffff',700)+text('常用的留在隐藏区，偶尔用的收进 TrayNest。',89,496,25,'#a9bbd3');
['自己选图标','拖进去收纳','点一下展开'].forEach((s,i)=>{heroType+=rect(88+i*166,550,148,49,24,'#11233a','#294361')+text(s,106+i*166,582,22,'#c4dcf8');});
heroType+=text('WINDOWS 11 x64  /  本地运行  /  预览版',88,804,20,'#819bbf');
let heroUi=text('偶尔才用的工具，放在这里。',876,300,29,'#ecf4ff',500)+panel('panel',870,340,620)+pic(logo,1350,659,60,60)+text('需要时，点开收纳盒',1090,699,22,'#c8daf3')+`<path d="M1315 688h21" stroke="#82bfff" stroke-width="2"/>`+text('TrayNest 收纳面板 · 界面已放大',876,774,19,'#9bb2cf');
emit('hero',900,[['background',heroBg],['product',heroUi],['brand',heroBrand],['typography',heroType]]);

let hierarchyBg=rect(0,0,1600,960,0,'#f1f5fc');
let hierarchyType=text('任务栏放不下，隐藏区也会挤。',72,135,55,'#14213b',700)+text('经常要找的留下，偶尔才用的再收一层。',76,202,27,'#65738c');
let hierarchyCards='',hierarchyUi='';
const levels=[
 ['最需要及时看到的','留在任务栏',['图标多了，任务栏放不下，','先把最重要的留在这里。']],
 ['经常要点开的','留在系统隐藏区',['其他图标进了隐藏区，','常用的也容易混在里面。']],
 ['偶尔才用到的','收进 TrayNest',['把偶尔才操作的工具，','再收进一个可展开的面板。']],
];
levels.forEach(([title,where,lines],i)=>{const x=64+i*504;hierarchyCards+=rect(x,278,464,544,24,'#ffffff',i===2?'#78aaff':'#d9e3f2')+text(title,x+30,345,29,'#17243b',600)+text(where,x+30,389,23,i===2?'#2563eb':'#65738c',500)+text(lines[0],x+30,713,23,'#65738c')+text(lines[1],x+30,752,23,'#65738c');});
// Schematic Windows surfaces use generic symbols; only TrayNest is a product capture.
hierarchyUi+=`<path d="M104 481v-10h384v10" stroke="#8ea7ca" fill="none" stroke-width="2"/>`+text('右下角就这点位置',194,455,21,'#6f86a9')+rect(98,493,396,106,18,'#222a38')+`<g transform="translate(130 515)"><rect width="62" height="62" rx="15" fill="#3979e8"/><path d="M15 17h32v24H30l-9 8v-8h-6z" fill="none" stroke="white" stroke-width="4" stroke-linejoin="round"/></g>`+text('消息',214,559,24,'#e1e9f7')+text('⌃',339,558,32,'#b6c5db')+text('09:41',387,558,24,'#d3dfef');
hierarchyUi+=rect(644,464,280,170,18,'#222a38')+`<g fill="none" stroke="#c6d7ef" stroke-width="5" stroke-linecap="round"><path d="M688 510h34m-34 13h34m-34 13h22"/><path d="M763 532a16 16 0 1 1 32 0h9v14h-50v-14z"/></g>`+pic(logo,836,502,58,58)+text('还有一些工具',669,601,20,'#afc3e2');
hierarchyUi+=panel('panel',1099,469,410,false);
hierarchyType+=text('示例：将 TrayNest 入口放在系统隐藏区。收哪些图标、怎么排列，都按你的习惯来。',76,891,23,'#5e7395')+text('任务栏和系统隐藏区为示意，收纳面板为放大的产品界面。',76,929,18,'#8191a8');
emit('hierarchy',960,[['background',hierarchyBg],['cards',hierarchyCards],['product',hierarchyUi],['typography',hierarchyType]]);

let featureBg=rect(0,0,1600,1030,0,'url(#light)');
let featureTitle=text('留住位置，也留住操作习惯。',72,140,59,'#14213b',700)+text('收纳之后，图标还是你熟悉的用法。',76,207,27,'#65738c');
let featureCards='',featureShots='';
const cards=[['01','拖进来，就收好了。','panel',['从系统隐藏区拖入，','集中到独立的小面板。']],['02','按自己的习惯排列。','reorder',['面板内拖动排序，','松手保存，Esc 取消。']],['03','图标淡了，位置还在。','offline',['保留原来的工具格位，','单击可尝试启动。']]];
cards.forEach(([n,title,shot,lines],i)=>{const x=64+i*504;featureCards+=rect(x,285,464,545,24,'#ffffff','#d9e3f2')+text(n,x+30,340,21,'#2867d8',600)+text(title,x+30,402,29,'#17243b',600)+text(lines[0],x+30,687,24,'#65738c')+text(lines[1],x+30,727,24,'#65738c');featureShots+=panel(shot,x+27,452,410,false);});
featureCards+=rect(64,872,1472,88,20,'#e2eafb')+text('单击 / 双击 / 右键',96,925,27,'#254a83',600)+text('已收纳图标可继续使用原软件的交互。',500,925,25,'#526b91')+text('界面已放大，便于展示；软件处于预览阶段。',76,1000,18,'#7f8ca2');
emit('features',1030,[['background',featureBg],['cards',featureCards],['product',featureShots],['typography',featureTitle]]);

const official=path.join(root,'promo/traynest-4x3/public/icons');
function overflow(x,y){return `<g>${rect(x,y,250,145,16,'#282b32','#515966')}${pic(logo,x+26,y+40,48,48)}<g transform="translate(${x+109} ${y+64})" stroke="#d4dce9" fill="none" stroke-width="3.5" stroke-linecap="round"><path d="M-16-3q16-20 32 0M-10 4q10-12 20 0M-4 11q4-4 8 0"/></g><g transform="translate(${x+191} ${y+62})" stroke="#d4dce9" stroke-width="3" fill="none" stroke-linejoin="round"><path d="M-15-5h7l10-9v28L-8 5h-7zM9-9q11 9 0 18"/></g></g>`;}
let placementBg=rect(0,0,1600,980,0,'#eef3fb');
let placementType=text('弹出的位置，由你决定。',72,140,60,'#14213b',700)+text('右键入口 → 弹出位置 → 左侧 / 上方',77,210,27,'#65738c')+text('系统隐藏区展开时，以隐藏区为参照；入口在任务栏时，以入口为参照。',76,916,23,'#65738c')+text('位置示意 · 靠近屏幕边缘时会自动避让，偏好会保存。',76,953,19,'#7e8ea6');
let placementCards=rect(64,285,720,550,24,'white','#d9e3f2')+rect(816,285,720,550,24,'white','#d9e3f2')+text('左侧',102,355,34,'#17243b',600)+text('侧边展开，左右分开。',104,405,24,'#65738c')+text('上方',855,355,34,'#17243b',600)+text('向上展开，留出横向空间。',857,405,24,'#65738c');
let placementUi=overflow(488,527)+panel('panel',102,520,365)+text('TrayNest',104,490,21,'#2867d8',600)+text('系统隐藏区（示意）',490,713,20,'#65738c')+`<path d="M473 595h9" stroke="#337aea" stroke-width="2"/>`+overflow(1190,640)+panel('panel',962,433,478)+text('系统隐藏区（示意）',1192,817,19,'#65738c');
emit('placement',980,[['background',placementBg],['cards',placementCards],['product',placementUi],['typography',placementType]]);
const preview=`<!doctype html><meta charset="utf-8"><title>TrayNest README 宣传图</title><style>body{margin:0;padding:32px;background:#101620;color:#dae5f5;font:16px system-ui}main{max-width:1200px;margin:auto}img{display:block;width:100%;margin:24px 0 48px;border-radius:12px}a{color:#a5d8ff}</style><main><h1>TrayNest · README 宣传图</h1><p>背景、产品控件、品牌与文字均保留独立图层。输出为GitHub可直接显示的PNG。</p>${['hero','hierarchy','features','placement'].map(n=>`<h2>${n}</h2><a href="layers/${n}.svg">查看可编辑SVG</a><img src="../../assets/readme/${n}.png" alt="${n}">`).join('')}</main>`;
fs.writeFileSync(path.join(__dirname,'index.html'),preview);
fs.writeFileSync(path.join(__dirname,'manifest.json'),JSON.stringify({canvasWidths:1600,images:['hero.png','hierarchy.png','features.png','placement.png'],layers:['background','product','brand/cards','typography'],background:'Built-in imagegen, prompt in CREDITS.md',product:'Current production TrayPanel, public demo fixtures, no user desktop',fonts:['Microsoft YaHei UI','Segoe UI'],renderer:'@resvg/resvg-js 2.6.2'},null,2));
