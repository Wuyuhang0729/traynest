const fs=require('node:fs'),path=require('node:path');
const {Resvg}=require('../../../../.tools/logo-render/node_modules/@resvg/resvg-js');
const root=path.resolve(__dirname,'../../../..'),assets=path.join(__dirname,'assets');
const cases=JSON.parse(fs.readFileSync(path.join(__dirname,'cases.json'),'utf8'));
const appIcon=path.join(root,'assets/branding/traynest-256.png');
const W=1080,H=1820;
const escape=s=>String(s).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;');
const pic=(file,x,y,w,h,extra='')=>`<image href="data:image/png;base64,${fs.readFileSync(file).toString('base64')}" x="${x}" y="${y}" width="${w}" height="${h}" ${extra}/>`;
const icon=(id,x,y,s=52)=>pic(id==='traynest'?appIcon:path.join(assets,id+'-normal.png'),x,y,s,s);
const rect=(x,y,w,h,r,fill,stroke='none')=>`<rect x="${x}" y="${y}" width="${w}" height="${h}" rx="${r}" fill="${fill}" stroke="${stroke}"/>`;
const text=(s,x,y,n=24,c='#65738c',weight=400,anchor='start')=>`<text x="${x}" y="${y}" text-anchor="${anchor}" font-family="Microsoft YaHei UI,Segoe UI" font-size="${n}" fill="${c}" font-weight="${weight}">${escape(s)}</text>`;
const names={qq:'QQ',wechat:'微信','tencent-meeting':'腾讯会议',wecom:'企业微信',baidupan:'百度网盘',onedrive:'OneDrive',keepassxc:'KeePassXC',huorong:'火绒',rustdesk:'RustDesk',everything:'Everything',xunlei:'迅雷',traynest:'TrayNest'};
const defs=`<defs><linearGradient id="bg" x2="0" y2="1"><stop stop-color="#f7f9ff"/><stop offset="1" stop-color="#e9f0fc"/></linearGradient><filter id="shadow" x="-25%" y="-35%" width="150%" height="180%"><feDropShadow dx="0" dy="8" stdDeviation="12" flood-color="#283e68" flood-opacity=".13"/></filter><clipPath id="customClip">${rect(250,1260,660,279,20,'white')}</clipPath></defs>`;
function badge(n,y,blue=false){return `<circle cx="87" cy="${y}" r="31" fill="${blue?'#2563eb':'#dce5f2'}"/>`+text(n,87,y+11,30,blue?'white':'#48627f',700,'middle');}
function arrow(y1,y2){return `<path d="M87 ${y1}V${y2}m-8-9 8 9 8-9" stroke="#9bb2d3" stroke-width="3" fill="none" stroke-linecap="round" stroke-linejoin="round"/>`;}
function pill(s,x,y,w,blue=false){return rect(x,y,w,39,12,blue?'#dbeaff':'#edf2f9')+text(s,x+w/2,y+27,21,blue?'#2563eb':'#5e7290',500,'middle');}
const manifest=[];
for(const item of cases){
 const source=['traynest',...item.outer];
 const ratio=[item.important.length,source.length,item.personal.length];
 let background=rect(0,0,W,H,0,'url(#bg)');
 background+=rect(150,300,860,285,24,'#fff')+rect(150,650,860,410,24,'#fff')+rect(150,1130,860,535,24,'#f5f9ff','#87b3fa');
 let labels=text(item.role+' · 一种个人安排',60,65,25,'#74839b');
 const titleLines=item.title.split('，');
 labels+=text(titleLines[0]+'，',60,126,43,'#15253f',700)+text(titleLines.slice(1).join('，'),60,183,43,'#15253f',700);
 labels+=text('任务栏空间有限，给图标多一种安排。',62,238,25,'#6c7f9c');
 labels+=badge('1',348)+badge('2',698)+badge('3',1178,true);
 labels+=text('最重要',184,357,37,'#243b5b',700)+pill('任务栏',838,321,135);
 labels+=text(item.importantLabel,184,402,25,'#657b98');
 labels+=text(item.important.length+' 个应用 · '+item.importantNames,184,555,22,'#617594');
 labels+=text('次要重要',184,707,37,'#243b5b',700)+pill('系统隐藏区',792,671,181);
 labels+=text(item.outerLabel,184,752,25,'#657b98');
 labels+=text(source.length+' 个入口 · 点 TrayNest，展开下面的自选面板 ↓',184,1032,22,'#54759f');
 labels+=text('个人筛选',184,1187,37,'#2563eb',700)+pill('TrayNest',792,1151,181,true);
 labels+=text(item.personalLabel+' · '+item.personal.length+' 个应用',184,1232,25,'#4b75ae');
 item.personalLines.forEach((line,i)=>labels+=text(line,184,1581+i*34,23,'#54759f'));
 labels+=text('第 3 层由你挑选，内外都按自己的习惯安排。',60,1720,26,'#395878',600);
 labels+=text('示例比例 '+ratio.join(' : ')+'；系统隐藏区的数量包含 TrayNest 入口。',60,1764,20,'#7b8ca5');
 let ui=arrow(388,610)+arrow(738,1090);
 ui+=text('展开隐藏区',87,635,18,'#6f87a8',400,'middle');
 ui+=text('再展开自选面板',87,1114,17,'#527fbd',400,'middle');
 ui+=`<g filter="url(#shadow)">${rect(184,437,790,82,14,'#263043')}${rect(184,782,790,214,17,'#202228','#4b5362')}${rect(247,1257,666,285,23,'#202228','#6a9fe8')}${pic(path.join(__dirname,'captures',item.id,'panel.png'),250,1260,660,279,'clip-path="url(#customClip)"')}</g>`;
 item.important.forEach((id,i)=>ui+=icon(id,210+i*74,456,44));
 ui+=`<path d="M790 480l10-10 10 10" stroke="#d4dfed" stroke-width="3" fill="none" stroke-linecap="round"/>`+text('09:41',869,485,23,'#e2eafa');
 source.forEach((id,index)=>{
  const cx=184+790/6+(index%3)*(790/3),y=797+Math.floor(index/3)*98;
  ui+=icon(id,cx-24,y,48)+text(names[id],cx,y+73,18,'#d8e1ef',400,'middle');
  if(index===0)ui+=rect(cx-31,y-6,62,62,13,'none','#77afff');
 });
 const body=[['background',background],['interface',ui],['labels',labels]];
 const wrap=content=>`<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}" viewBox="0 0 ${W} ${H}">${defs}${content}</svg>`;
 const svg=wrap(body.map(([id,content])=>`<g id="${id}">${content}</g>`).join(''));
 fs.writeFileSync(path.join(__dirname,item.id+'.svg'),svg);
 const font={loadSystemFonts:true,defaultFontFamily:'Microsoft YaHei UI'};
 fs.writeFileSync(path.join(__dirname,item.id+'.png'),new Resvg(svg,{font}).render().asPng());
 fs.writeFileSync(path.join(__dirname,item.id+'-800.png'),new Resvg(svg,{fitTo:{mode:'width',value:800},font}).render().asPng());
 for(const [id,content]of body)fs.writeFileSync(path.join(__dirname,item.id+'.'+id+'.svg'),wrap(content));
 const scale=ratio[0]/3,nearRatio=Math.abs(ratio[1]-6*scale)/(6*scale)<=.15&&Math.abs(ratio[2]-5*scale)/(5*scale)<=.15;
 manifest.push({case:item.id,role:item.role,width:W,height:H,orientation:'portrait',tierOrder:['最重要 / 任务栏','次要重要 / 系统隐藏区','个人筛选 / TrayNest'],ratio:ratio.join(':'),near3_6_5:nearRatio,important:item.important,outer:item.outer,personal:item.personal,outerAlsoCategorized:true,setsDisjoint:new Set([...item.important,...item.outer,...item.personal]).size===item.important.length+item.outer.length+item.personal.length,inputMethodOnlyPersonal:!item.important.includes('sogou')&&!item.outer.includes('sogou'),published:false});
}
fs.writeFileSync(path.join(__dirname,'verification.json'),JSON.stringify(manifest,null,2));
fs.writeFileSync(path.join(__dirname,'index.html'),`<!doctype html><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>典型用户收纳案例 · 竖版</title><style>body{background:#e8eef8;margin:0;padding:24px;font:16px system-ui;color:#243550}main{max-width:800px;margin:auto}img{display:block;width:100%;height:auto;margin:15px 0 48px;border-radius:15px}a{color:#2563eb}</style><main><h1>三个案例，三级安排</h1><p>从上往下：任务栏 → 系统隐藏区 → TrayNest 自选面板。本地预览。</p>${cases.map(x=>`<h2>${x.role}</h2><a href="${x.id}.svg">可编辑SVG</a><img src="${x.id}.png" alt="${x.role}的三级托盘安排，依次为最重要、次要重要、个人筛选">`).join('')}</main>`);
console.log(manifest.map(x=>x.role+' '+x.ratio+' '+x.width+'×'+x.height).join('\n'));
