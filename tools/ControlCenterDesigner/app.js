'use strict';
(() => {
  const $ = id => document.getElementById(id);
  const KEY = 'dockforwindows.control-center-designer.v2';
  const paths = {
    Wifi:'M2 8a15 15 0 0 1 20 0M5 12a10 10 0 0 1 14 0M8.5 16a5 5 0 0 1 7 0M12 20h.01',
    Bluetooth:'M7 7l10 10-5 5V2l5 5L7 17',
    Focus:'M20.5 14A9 9 0 0 1 10 3a9 9 0 1 0 10.5 11Z',
    Projection:'M3 4h17v12H3zM8 20h7M11.5 16v4M6 8h4v5H6z',
    Brightness:'M12 2v2M12 20v2M2 12h2M20 12h2M5 5l1.5 1.5M17.5 17.5 19 19M5 19l1.5-1.5M17.5 6.5 19 5M16.5 12a4.5 4.5 0 1 1-9 0 4.5 4.5 0 0 1 9 0Z',
    Volume:'M3 9h4l5-4v14l-5-4H3zM16 8a6 6 0 0 1 0 8M19 5a10 10 0 0 1 0 14',
    Battery:'M3 6h15a1 1 0 0 1 1 1v10a1 1 0 0 1-1 1H3a1 1 0 0 1-1-1V7a1 1 0 0 1 1-1ZM22 10v4M5 9h10v6H5z',
    Media:'M10 17V4l10-2v13M10 6l10-2M10 17c0 2-5 4-5 1s5-4 5-1ZM20 15c0 2-5 4-5 1s5-4 5-1Z',
    Settings:'M9 3h6l1 3 3 1 2 5-2 4-3 1-1 4H9l-1-4-3-1-2-4 2-5 3-1zM15.5 12a3.5 3.5 0 1 1-7 0 3.5 3.5 0 0 1 7 0Z',
    Chevron:'M9 5l7 7-7 7',Close:'M6 6l12 12M6 18 18 6',
    Play:'m7 3 14 9-14 9Z',Pause:'M7 4v16M17 4v16',Previous:'M4 5v14M20 4 7 12l13 8Z',Next:'M20 5v14M4 4l13 8-13 8Z',
    Headphones:'M4 15v-4a8 8 0 0 1 16 0v4M4 11h3v9H4zM17 11h3v9h-3z',Speaker:'M4 3h16v18H4zM14 7a2 2 0 1 1-4 0 2 2 0 0 1 4 0ZM16 15a4 4 0 1 1-8 0 4 4 0 0 1 8 0Z',
    Keyboard:'M2 6h20v12H2zM6 10h.1M10 10h.1M14 10h.1M18 10h.1M7 14h10',Mouse:'M6 9a6 6 0 0 1 12 0v6a6 6 0 0 1-12 0ZM12 4v6'
  };
  const icon = (name, cls='') => `<svg class="${cls}" viewBox="0 0 24 24" aria-hidden="true"><path d="${paths[name] || paths.Media}"/></svg>`;
  const names = {Wifi:'Wi-Fi',Bluetooth:'蓝牙',Media:'正在播放',Focus:'专注模式',Projection:'屏幕投影',Battery:'电池',Brightness:'屏幕亮度',Volume:'音量'};
  const shapes={Circle:[1,1,'圆形'],Capsule:[2,1,'胶囊'],Square:[2,2,'正方形'],Rectangle:[4,1,'矩形']};
  const specs = [
    ['PanelWidth','基础面板宽度',300,480,1,'px','geometry'],['PanelScale','整体面板缩放',.65,1.5,.01,'×','geometry'],['ComponentScale','组件等比例缩放',.75,1.5,.01,'×','geometry'],['Gap','模块间距',6,24,1,'px','geometry'],
    ['CapsuleRoundness','胶囊圆滑程度',90,99.9,.1,'%','geometry'],['SquareRoundness','正方形圆角',0,75,.1,'%','geometry'],['RectangleRoundness','矩形圆角',0,60,.1,'%','geometry'],['DetailRoundness','二级面板圆角',0,60,.1,'%','geometry'],
    ['FontSize','文字大小',11,16,.5,'px','content'],['IconSize','图标大小',18,32,1,'px','content'],['ContentGap','图标与文字间距',4,18,1,'px','content'],['CardPadding','卡片内边距',6,20,1,'px','content']
  ];
  const defaults = () => ({PanelWidth:345,PanelScale:1,ComponentScale:1,Gap:12,CapsuleRoundness:99,SquareRoundness:41,RectangleRoundness:28,DetailRoundness:24,FontSize:12,IconSize:18,ContentGap:6,CardPadding:18,ContentAlignment:'Center',Modules:[['Wifi','Capsule'],['Media','Square'],['Bluetooth','Capsule'],['Focus','Capsule'],['Battery','Circle'],['Projection','Circle'],['Brightness','Rectangle'],['Volume','Rectangle']].map(([Module,Shape])=>({Module,Shape,Span:shapes[Shape][0],Visible:true}))});
  let state=defaults(), view='main', editing=false, dragId=null, previewBlur=6, previewTint=8, ink='light', background='twilight', playing=false, volume=24, brightness=70, currentCode='json', timer;
  let pointerEdit=null,lastView='main';
  function validate(input) {
    const value=input.Geometry || input;
    if(!value || typeof value!=='object' || Array.isArray(value))throw Error('参数必须是 JSON 对象');
    if(input.Schema && !['DockForWindows.ControlCenterDesign/v1','DockForWindows.ControlCenterDesign/v2'].includes(input.Schema))throw Error('文件不是此调节器的设计参数');
    if(!specs.some(([key])=>typeof value[key]==='number'))throw Error('未找到控制中心形状参数');
    const result=defaults();
    for(const [key,,min,max] of specs)if(typeof value[key]==='number'&&Number.isFinite(value[key]))result[key]=Math.round(Math.max(min,Math.min(max,value[key]))*100)/100;
    if(Number.isFinite(value.MediaRoundness)&&!Object.hasOwn(value,'SquareRoundness'))result.SquareRoundness=Math.min(75,value.MediaRoundness);
    result.ContentAlignment=value.ContentAlignment==='Left'?'Left':'Center';
    const modules=input.Modules || value.Modules;
    if(Array.isArray(modules)){
      const seen=new Set();result.Modules=[];
      for(const entry of modules)if(entry&&Object.hasOwn(names,entry.Module)&&!seen.has(entry.Module)){
        const Shape=Object.hasOwn(shapes,entry.Shape)?entry.Shape:entry.Span===1?'Circle':entry.Span===4?'Rectangle':entry.Module==='Media'?'Square':'Capsule';seen.add(entry.Module);result.Modules.push({Module:entry.Module,Shape,Span:shapes[Shape][0],Visible:entry.Visible!==false});
      }
      for(const entry of defaults().Modules)if(!seen.has(entry.Module))result.Modules.push({...entry,Visible:false});
    }
    return result;
  }
  try{const saved=localStorage.getItem(KEY);if(saved)state=validate(JSON.parse(saved));}catch{}
  function notify(text){$('toast').textContent=text;$('toast').hidden=false;clearTimeout(timer);timer=setTimeout(()=>$('toast').hidden=true,3200);}
  function save(){try{localStorage.setItem(KEY,JSON.stringify(state));$('save-state').textContent='已保存 · 此浏览器';}catch{$('save-state').textContent='浏览器存储不可用，请导出保存';}}
  function controls(){
    for(const [key,label,min,max,step,unit,group] of specs){
      const wrapper=document.createElement('label');wrapper.className='range-field';
      wrapper.innerHTML=`<span>${label}<output id="value-${key}" for="range-${key}"></output></span><input type="range" id="range-${key}" min="${min}" max="${max}" step="${step}" aria-label="${label}">`;
      const input=wrapper.querySelector('input');input.value=state[key];
      input.addEventListener('input',()=>{state[key]=Number(input.value);updateValues();render();save();});
      $(group==='geometry'?'geometry-controls':'content-controls').append(wrapper);
    }
    $('alignment').addEventListener('change',e=>{state.ContentAlignment=e.target.value;render();save();});updateValues();
  }
  function updateValues(){for(const [key,,,,,unit] of specs){$('range-'+key).value=state[key];$('value-'+key).textContent=state[key]+' '+unit;}$('alignment').value=state.ContentAlignment;}
  function moveModule(id,delta){const index=state.Modules.findIndex(m=>m.Module===id),to=Math.max(0,Math.min(state.Modules.length-1,index+delta));const [entry]=state.Modules.splice(index,1);state.Modules.splice(to,0,entry);changedModules();}
  function swapModules(from,to){const a=state.Modules.findIndex(m=>m.Module===from),b=state.Modules.findIndex(m=>m.Module===to);if(a<0||b<0||a===b)return;const [item]=state.Modules.splice(a,1);state.Modules.splice(b,0,item);changedModules();}
  function changedModules(){moduleList();render();save();}
  function moduleList(){
    $('module-list').replaceChildren();
    for(const m of state.Modules){const row=document.createElement('div');row.className='module-row';row.draggable=true;row.dataset.module=m.Module;
      row.innerHTML=`<span class="drag-handle" aria-hidden="true">⠿</span><label class="module-name"><input type="checkbox" aria-label="显示${names[m.Module]}" ${m.Visible?'checked':''}>${icon(m.Module)}${names[m.Module]}</label><span></span><div class="row-bottom"><select aria-label="${names[m.Module]}尺寸">${Object.entries(shapes).map(([key,v])=>`<option value="${key}">${v[2]} ${v[0]}×${v[1]}</option>`).join('')}</select><button aria-label="上移${names[m.Module]}">↑</button><button aria-label="下移${names[m.Module]}">↓</button></div>`;
      row.querySelector('input').onchange=e=>{m.Visible=e.target.checked;render();save();};row.querySelector('select').value=m.Shape;
      row.querySelector('select').onchange=e=>{m.Shape=e.target.value;m.Span=shapes[m.Shape][0];render();save();};const buttons=row.querySelectorAll('button');buttons[0].onclick=()=>moveModule(m.Module,-1);buttons[1].onclick=()=>moveModule(m.Module,1);
      row.addEventListener('dragstart',e=>{if(e.target!==row)return;dragId=m.Module;e.dataTransfer.setData('text/plain',m.Module);row.classList.add('dragging');});row.addEventListener('dragend',()=>row.classList.remove('dragging'));row.addEventListener('dragover',e=>e.preventDefault());row.addEventListener('drop',e=>{e.preventDefault();swapModules(e.dataTransfer.getData('text/plain')||dragId,m.Module);});$('module-list').append(row);
    }
  }
  // Same roundness mapping as DockSurface.CreateGeometry: n=3-fraction^8,
  // and the 100% endpoint is circular. The page uses sampled SVG curves.
  function outline(w,h,roundness){
    const inset=.6,left=inset,top=inset,right=w-inset,bottom=h-inset,f=Math.max(0,Math.min(1,roundness/100));
    const r=Math.min(right-left,bottom-top)/2*f,n=3-Math.pow(f,8),power=2/n;
    if(r<.05)return `M${left} ${top}H${right}V${bottom}H${left}Z`;
    let d=`M${left+r},${top}H${right-r}`;
    for(let corner=0;corner<4;corner++){
      for(let i=1;i<=64;i++){const t=i/64*Math.PI/2,s=Math.pow(Math.sin(t),power),c=Math.pow(Math.cos(t),power);let x,y;
        if(corner===0){x=right-r+r*s;y=top+r-r*c;}
        if(corner===1){x=right-r+r*c;y=bottom-r+r*s;}
        if(corner===2){x=left+r-r*s;y=bottom-r+r*c;}
        if(corner===3){x=left+r-r*c;y=top+r-r*s;}
        d+=`L${x.toFixed(3)},${y.toFixed(3)}`;
      }
      if(corner===0)d+=`V${bottom-r}`;if(corner===1)d+=`H${left+r}`;if(corner===2)d+=`V${top+r}`;
    }return d+'Z';
  }
  function skin(element,w,h,r,id){
    element.querySelectorAll(':scope > .glass-skin, :scope > .glass-edge').forEach(layer=>layer.remove());
    const d=outline(w,h,r),layer=document.createElement('div');layer.className='glass-skin';layer.style.clipPath=`path('${d}')`;element.prepend(layer);
    const edge=document.createElementNS('http://www.w3.org/2000/svg','svg');edge.setAttribute('class','glass-edge');edge.setAttribute('viewBox',`0 0 ${w} ${h}`);edge.setAttribute('aria-hidden','true');
    edge.innerHTML=`<defs><linearGradient id="edge-${id}" x1="0" y1="0" x2="0" y2="1"><stop stop-color="#fff" stop-opacity=".65"/><stop offset=".13" stop-color="#fff" stop-opacity=".20"/><stop offset=".46" stop-color="#000" stop-opacity=".25"/><stop offset=".83" stop-color="#fff" stop-opacity=".20"/><stop offset="1" stop-color="#fff" stop-opacity=".58"/></linearGradient></defs><path d="${d}" fill="none" stroke="url(#edge-${id})"/>`;element.prepend(edge);
  }
  function layout(){
    const gap=state.Gap*state.ComponentScale,inner=(state.PanelWidth-36)*state.ComponentScale,unit=(inner-3*gap)/4,occupied=new Set(),items=[];let maxRow=0;
    for(const m of state.Modules.filter(m=>m.Visible)){const [cols,rows]=shapes[m.Shape];let row=0,col=0,found=false;
      while(!found){for(let x=0;x<=4-cols;x++){let free=true;for(let y=row;y<row+rows;y++)for(let z=x;z<x+cols;z++)if(occupied.has(`${z}:${y}`))free=false;if(free){col=x;found=true;break;}}if(!found)row++;}
      for(let y=row;y<row+rows;y++)for(let x=col;x<col+cols;x++)occupied.add(`${x}:${y}`);items.push({m,row,col,rows,cols});maxRow=Math.max(maxRow,row+rows);
    }
    for(const p of items){p.x=p.col*(unit+gap);p.y=p.row*(unit+gap);p.w=p.cols*unit+(p.cols-1)*gap;p.h=p.rows*unit+(p.rows-1)*gap;}
    return {items,height:maxRow*unit+Math.max(0,maxRow-1)*gap,inner,unit,gap};
  }
  function labels(title,subtitle){return `<div class="tile-labels"><b>${title}</b>${subtitle?`<small>${subtitle}</small>`:''}</div>`;}
  function mediaButtons(){return `<div class="media-buttons"><button data-media="previous" aria-label="上一首">${icon('Previous')}</button><button data-media="play" aria-label="播放或暂停">${icon(playing?'Pause':'Play')}</button><button data-media="next" aria-label="下一首">${icon('Next')}</button></div>`;}
  function renderMain(){
    const tiles=$('tiles'),previous=new Map([...tiles.querySelectorAll('.tile')].map(t=>[t.dataset.module,t.getBoundingClientRect()]));tiles.hidden=false;$('detail-panel').hidden=true;$('edit-controls').hidden=false;tiles.replaceChildren();const metrics=layout();tiles.style.height=metrics.height+'px';
    const statuses={Wifi:'家庭无线网络',Bluetooth:'已打开',Focus:'减少干扰',Projection:'仅电脑',Battery:'81%',Brightness:brightness+'%',Volume:volume+'%'};
    for(const p of metrics.items){const m=p.m,id=m.Module,tile=document.createElement('div');tile.className='tile'+(m.Shape==='Circle'?' circle-tile':['Brightness','Volume'].includes(id)?' slider-tile':id==='Media'?' media-tile':'')+(editing?' editing':'');tile.dataset.module=id;tile.dataset.shape=m.Shape;Object.assign(tile.style,{left:p.x+'px',top:p.y+'px',width:p.w+'px',height:p.h+'px'});
      tile.style.setProperty('--diameter',p.w+'px');tile.style.setProperty('--module-height',p.h+'px');tile.setAttribute('aria-label',names[id]);tile.setAttribute('role','group');const content=document.createElement('div');content.className='tile-content';
      if(m.Span===1)content.innerHTML=icon(id,'main-icon')+labels(id==='Battery'?statuses[id]:id==='Projection'?statuses[id]:names[id],'');
      else if(id==='Media'){content.innerHTML=`<div class="album">${icon('Media')}</div>${labels('示例音乐','艺术家 · 设计预览')}${mediaButtons()}`;}
      else if(['Brightness','Volume'].includes(id)){const val=id==='Brightness'?brightness:volume;content.innerHTML=`<div class="slider-heading">${icon(id,'main-icon')}${labels(names[id],val+'%')}</div><input class="preview-slider" type="range" min="0" max="100" value="${val}" style="--value:${val}%" aria-label="${names[id]}预览数值">`;
        const input=content.querySelector('input');input.oninput=()=>{const v=Number(input.value);if(id==='Brightness')brightness=v;else volume=v;input.style.setProperty('--value',v+'%');content.querySelector('small').textContent=v+'%';};input.onclick=e=>e.stopPropagation();
      }else content.innerHTML=icon(id,'main-icon')+labels(names[id],statuses[id]);
      tile.append(content);if(m.Span!==1){const expand=document.createElement('button');expand.className='expand';expand.ariaLabel='预览'+names[id]+'详情';expand.innerHTML=icon('Chevron');expand.onclick=e=>{e.stopPropagation();if(!editing)openModule(id);};tile.append(expand);}
      skin(tile,p.w,p.h,m.Shape==='Circle'?100:state[m.Shape+'Roundness'],'module-'+id);
      tile.onclick=e=>{if(!editing&&!['Brightness','Volume'].includes(id)&&!e.target.closest('[data-media]'))openModule(id);};
      if(editing){const grip=document.createElement('button');grip.className='resize-grip';grip.ariaLabel='调整'+names[id]+'形状';grip.innerHTML='⌟';tile.append(grip);tile.addEventListener('pointerdown',e=>startPointer(e,id,e.target.closest('.resize-grip')?'resize':'move'));}
      tiles.append(tile);
    }
    const factor=$('cc-panel').getBoundingClientRect().width/$('cc-panel').offsetWidth||1;
    for(const tile of tiles.querySelectorAll('.tile')){const old=previous.get(tile.dataset.module);if(old&&pointerEdit?.id!==tile.dataset.module){const next=tile.getBoundingClientRect(),dx=(old.x-next.x)/factor,dy=(old.y-next.y)/factor;tile.animate([{transform:`translate(${dx}px,${dy}px) scale(${old.width/next.width},${old.height/next.height})`},{transform:`translate(${dx*.5-dy*.12}px,${dy*.5+dx*.12}px)`},{transform:'none'}],{duration:360,easing:'cubic-bezier(.25,.7,.25,1)'});}}
    $('edit-controls').textContent=editing?'完成排列':'编辑控制';bindMedia(tiles);updatePointerTile();if(!metrics.items.length){tiles.innerHTML='<p class="detail-note">暂未显示任何模块，请在左侧添加。</p>';tiles.style.height='80px';}
  }
  function startPointer(e,id,type){if(!editing||e.button!==0)return;e.preventDefault();e.stopPropagation();const item=layout().items.find(p=>p.m.Module===id),box=$('tiles').getBoundingClientRect(),factor=box.width/layout().inner;pointerEdit={id,type,x:e.clientX,y:e.clientY,startX:e.clientX,startY:e.clientY,base:item,grabX:(e.clientX-box.left)/factor-item.x,grabY:(e.clientY-box.top)/factor-item.y,last:null};$('tiles').setPointerCapture(e.pointerId);}
  function updatePointerTile(){if(!pointerEdit||pointerEdit.type!=='move')return;const tile=$('tiles').querySelector(`[data-module="${pointerEdit.id}"]`),box=$('tiles').getBoundingClientRect(),factor=box.width/layout().inner;if(tile){tile.style.transition='none';tile.style.zIndex='10';tile.style.opacity='.88';tile.style.transform=`translate(${(pointerEdit.x-box.left)/factor-pointerEdit.grabX-parseFloat(tile.style.left)}px,${(pointerEdit.y-box.top)/factor-pointerEdit.grabY-parseFloat(tile.style.top)}px) scale(1.025)`;}}
  window.addEventListener('pointermove',e=>{if(!pointerEdit)return;pointerEdit.x=e.clientX;pointerEdit.y=e.clientY;const metrics=layout(),box=$('tiles').getBoundingClientRect(),factor=box.width/metrics.inner;
    if(pointerEdit.type==='resize'){const requested={w:pointerEdit.base.w+(e.clientX-pointerEdit.startX)/factor,h:pointerEdit.base.h+(e.clientY-pointerEdit.startY)/factor},item=state.Modules.find(m=>m.Module===pointerEdit.id),distance=key=>{const [cols,rows]=shapes[key];return (requested.w-cols*metrics.unit-(cols-1)*metrics.gap)**2+(requested.h-rows*metrics.unit-(rows-1)*metrics.gap)**2;},best=Object.keys(shapes).sort((a,b)=>distance(a)-distance(b))[0];if(best!==item.Shape&&distance(item.Shape)-distance(best)>(metrics.unit*.2)**2){item.Shape=best;item.Span=shapes[best][0];render();moduleList();}}
    else{const x=(e.clientX-box.left)/factor,y=(e.clientY-box.top)/factor,target=metrics.items.find(p=>p.m.Module!==pointerEdit.id&&x>p.x+10&&x<p.x+p.w-10&&y>p.y+10&&y<p.y+p.h-10);if(target&&target.m.Module!==pointerEdit.last){pointerEdit.last=target.m.Module;swapModules(pointerEdit.id,target.m.Module);}if(!target)pointerEdit.last=null;updatePointerTile();}});
  window.addEventListener('pointerup',()=>{if(!pointerEdit)return;pointerEdit=null;render();moduleList();save();});
  function openModule(id){const target={Wifi:'wifi',Bluetooth:'bluetooth',Media:'media',Volume:'audio'}[id];if(target){view=target;render();}else notify('此处只调整外观，不操作系统功能');}
  const row=(glyph,name,status='',action='')=>`<div class="device-row"><span class="device-icon">${icon(glyph)}</span><div class="device-name">${name}${status?`<small>${status}</small>`:''}</div><span class="row-action">${action}</span></div>`;
  function renderDetail(){
    $('tiles').hidden=true;$('edit-controls').hidden=true;const panel=$('detail-panel');panel.hidden=false;
    const titles={bluetooth:'蓝牙',wifi:'Wi-Fi',audio:'音频输出',media:'正在播放'};
    panel.innerHTML=`<div class="detail-header"><b>${titles[view]}</b><button aria-label="返回主面板" id="detail-close">${icon('Close')}</button></div>`;
    if(view==='bluetooth')panel.innerHTML+=`<div class="device-row"><span class="device-icon">${icon('Bluetooth')}</span><div class="device-name">蓝牙<small>已打开</small></div><button class="preview-switch on" aria-label="蓝牙开关外观"></button></div>`+row('Headphones','蓝牙耳机','已连接','✓')+row('Keyboard','无线键盘','已配对','连接')+row('Mouse','无线鼠标','已配对','连接')+'<button class="detail-footer">蓝牙设置…</button>';
    if(view==='wifi')panel.innerHTML+=row('Wifi','家庭无线网络','已连接 · 安全网络','✓')+row('Wifi','Office Wi-Fi','需要密码','›')+row('Wifi','访客网络','开放网络','›')+'<button class="detail-footer">其他网络…</button>';
    if(view==='audio')panel.innerHTML+=row('Speaker','扬声器','系统默认输出','✓')+row('Headphones','蓝牙耳机','已连接','选择')+row('Projection','显示器音频','HDMI','选择')+'<button class="detail-footer">声音设置…</button>';
    if(view==='media')panel.innerHTML+=`<div class="large-media"><div class="album">${icon('Media')}</div><div class="large-media-info"><b>示例音乐</b><small>艺术家 · 专辑</small>${mediaButtons()}</div></div><div class="media-progress"><span></span></div><div class="media-time"><span>2:36</span><span>−1:43</span></div><p class="detail-note">网页示例会话 · 桌面版使用真实系统媒体状态</p>`;
    $('detail-close').onclick=()=>{view='main';render();};panel.querySelectorAll('.preview-switch').forEach(b=>b.onclick=()=>b.classList.toggle('on'));
    bindMedia(panel);requestAnimationFrame(()=>{skin(panel,layout().inner,panel.offsetHeight,state.DetailRoundness,'detail');fit();});
  }
  function bindMedia(root){root.querySelectorAll('[data-media]').forEach(b=>b.onclick=e=>{e.stopPropagation();if(b.dataset.media==='play'){playing=!playing;render();}else notify('示例播放器 · 仅用于外观预览');});}
  function render(){
    const panel=$('cc-panel');let ghost=null;if(lastView!==view){const source=lastView==='main'?$('tiles'):$('detail-panel');ghost=source.cloneNode(true);ghost.removeAttribute('id');ghost.querySelectorAll('[id]').forEach(e=>e.removeAttribute('id'));Object.assign(ghost.style,{position:'absolute',top:source.offsetTop+'px',left:'18px',width:source.offsetWidth+'px',height:source.offsetHeight+'px',pointerEvents:'none'});}
    panel.style.width=(layout().inner+36)+'px';panel.dataset.alignment=state.ContentAlignment;panel.dataset.ink=ink;
    for(const [key,value] of Object.entries({'--font-size':state.FontSize*state.ComponentScale+'px','--icon-size':state.IconSize*state.ComponentScale+'px','--content-gap':state.ContentGap*state.ComponentScale+'px','--card-padding':Math.min(12,state.CardPadding)*state.ComponentScale+'px','--glass-blur':previewBlur+'px','--glass-tint':previewTint/100}))panel.style.setProperty(key,value);
    $('view-tabs').querySelectorAll('button').forEach(b=>{const selected=b.dataset.view===view;b.classList.toggle('selected',selected);b.setAttribute('aria-pressed',selected);});
    view==='main'?renderMain():renderDetail();if(ghost){panel.append(ghost);const incoming=view==='main'?$('tiles'):$('detail-panel');incoming.animate([{opacity:0,transform:`translateX(${view==='main'?-14:14}px)`},{opacity:1,transform:'none'}],{duration:290,easing:'ease-in-out'});ghost.animate([{opacity:1,transform:'none'},{opacity:0,transform:`translateX(${view==='main'?14:-14}px)`}],{duration:250,easing:'ease-in-out'}).finished.finally(()=>ghost.remove());}lastView=view;requestAnimationFrame(fit);
  }
  function fit(){const panel=$('cc-panel'),host=panel.parentElement;const scale=Math.min(state.PanelScale,Math.max(.25,host.clientWidth/(layout().inner+36)));panel.style.transform=`scale(${scale})`;panel.style.transformOrigin='top center';host.style.height=panel.offsetHeight*scale+'px';$('scene').style.minHeight=Math.max(430,panel.offsetHeight*scale+80)+'px';$('size-status').textContent=`面板 ${Math.round(panel.offsetWidth*scale)} × ${Math.round(panel.offsetHeight*scale)} px · ${Math.round(scale*100)}% 预览`;}
  function documentData(){const {Modules,...Geometry}=state;return {Schema:'DockForWindows.ControlCenterDesign/v2',ApplyScope:'ControlCenterGeometryOnly',Geometry:{...Geometry,CornerProfile:'DockSurfaceExistingContinuousCurve',SmallControls:'Circle',BrightnessAndVolume:'HorizontalWithInlineHeader',Grid:'Circle1x1-Capsule2x1-Square2x2-Rectangle4x1'},Modules:Modules.map(m=>({...m})),Preserve:['DockBehavior','PinnedApplications','GlassEngine','GlobalMaterial']};}
  function csharp(){const {Modules,...geometry}=state;const constants=specs.map(([key,label])=>`    // ${label}\n    public const double ${key} = ${geometry[key].toFixed(1)};`).join('\n');const rects=layout().items.map(p=>`        ["${p.m.Module}"] = new Rect(${p.x.toFixed(3)}, ${p.y.toFixed(3)}, ${p.w.toFixed(3)}, ${p.h.toFixed(3)}),`).join('\n');return `// DockForWindows 控制中心外观参数 · 由网页调节器导出\n// 仅数据和布局坐标，等待接入 ControlCenterWindow；不要替换共享玻璃引擎。\n// 圆角值是 0–100 的幅度，直接对应 DockSurface.Radius。\n// 小按钮使用等宽高并设置 Radius=100；亮度/音量保持原排版。\nusing System.Collections.Generic;\nusing System.Windows;\n\nnamespace DockForWindows.Views;\n\ninternal static class ControlCenterGeometry\n{\n${constants}\n    public const string ContentAlignment = "${geometry.ContentAlignment}";\n    public const bool SmallControlsAreCircles = true;\n    public const bool PreserveBrightnessAndVolumeLayout = true;\n    public static readonly IReadOnlyDictionary<string, Rect> Cards = new Dictionary<string, Rect>\n    {\n${rects}\n    };\n}\n`;
  }
  function nativeCode(){const properties=specs.map(([key])=>`            ${key} = ${state[key]},`).join('\n');const modules=state.Modules.map(m=>`            new() { Module = ControlModule.${m.Module}, Shape = ControlCardShape.${m.Shape}, Span = ${m.Span}, Visible = ${m.Visible?'true':'false'} },`).join('\n');return `// 数据工厂：仅应用控制中心几何与排列，不改变共享材质。\nusing DockForWindows.Models;\nnamespace DockForWindows.Generated;\npublic static class ControlCenterDesignSnapshot\n{\n    public static void Apply(ControlCenterOptions options)\n    {\n        options.Geometry = new ControlCenterGeometry\n        {\n${properties}\n            ContentAlignment = "${state.ContentAlignment}"\n        };\n        options.Modules = new()\n        {\n${modules}\n        };\n        options.Normalize();\n    }\n}\n`;}
  function setCode(kind){currentCode=kind;$('export-code').value=kind==='json'?JSON.stringify(documentData(),null,2):nativeCode();$('show-json').classList.toggle('selected',kind==='json');$('show-csharp').classList.toggle('selected',kind==='csharp');$('copy-state').textContent='';}
  function openExport(kind='json'){setCode(kind);$('export-dialog').showModal();}
  function download(kind){const text=kind==='json'?JSON.stringify(documentData(),null,2):nativeCode(),blob=new Blob([text],{type:kind==='json'?'application/json;charset=utf-8':'text/plain;charset=utf-8'}),url=URL.createObjectURL(blob),a=document.createElement('a');a.href=url;a.download=kind==='json'?'ControlCenter-design.json':'ControlCenterGeometry.cs';a.click();setTimeout(()=>URL.revokeObjectURL(url),2000);notify('已生成 '+a.download);}
  $('view-tabs').querySelectorAll('button').forEach(b=>b.onclick=()=>{view=b.dataset.view;render();});
  document.querySelectorAll('.inspector-tabs button').forEach(b=>b.onclick=()=>{document.querySelectorAll('.inspector-tabs button').forEach(x=>{x.classList.toggle('selected',x===b);x.setAttribute('aria-pressed',x===b);});$('shape-controls').hidden=b.dataset.tab!=='shape';$('module-controls').hidden=b.dataset.tab!=='modules';});
  $('panel-settings').innerHTML=icon('Settings');$('panel-settings').onclick=()=>{document.querySelector('[data-tab="shape"]').click();document.querySelector('.inspector').scrollIntoView({behavior:'smooth',block:'start'});};
  $('edit-controls').onclick=()=>{editing=!editing;if(editing)document.querySelector('[data-tab="modules"]').click();render();};
  $('preview-blur').oninput=e=>{previewBlur=Number(e.target.value);$('blur-output').textContent=previewBlur+' px';$('cc-panel').style.setProperty('--glass-blur',previewBlur+'px');};
  $('preview-tint').oninput=e=>{previewTint=Number(e.target.value);$('tint-output').textContent=previewTint+'%';$('cc-panel').style.setProperty('--glass-tint',previewTint/100);};$('preview-ink').onchange=e=>{ink=e.target.value;render();};
  const backgrounds=['wallpaper','twilight','light'];$('background-button').onclick=()=>{background=backgrounds[(backgrounds.indexOf(background)+1)%backgrounds.length];$('scene').dataset.background=background;if(background==='light'){ink='dark';$('preview-ink').value='dark';}else{ink='light';$('preview-ink').value='light';}render();};
  $('show-background-window').onchange=e=>$('sample-window').hidden=!e.target.checked;
  $('wallpaper-file').onchange=e=>{const file=e.target.files[0];if(!file)return;if(!file.type.startsWith('image/')||file.size>15*1024*1024){notify('请选择小于 15 MB 的图片');return;}const reader=new FileReader();reader.onload=()=>{background='custom';$('scene').dataset.background='custom';$('wallpaper').style.background=`url("${reader.result}") center / cover`;};reader.readAsDataURL(file);};
  $('reset-button').onclick=()=>{state=defaults();updateValues();moduleList();render();save();notify('已恢复建议的形状和布局');};
  $('import-button').onclick=()=>$('import-file').click();$('import-file').onchange=async e=>{const file=e.target.files[0];if(!file)return;try{if(file.size>128*1024)throw Error('参数文件超过 128 KB');const next=validate(JSON.parse(await file.text()));state=next;updateValues();moduleList();render();save();notify('参数已导入');}catch(error){notify('导入失败：'+error.message);}finally{e.target.value='';}};
  $('export-button').onclick=()=>{download('json');openExport('json');};$('code-button').onclick=()=>openExport('csharp');$('close-dialog').onclick=()=>$('export-dialog').close();$('show-json').onclick=()=>setCode('json');$('show-csharp').onclick=()=>setCode('csharp');$('download-json').onclick=()=>download('json');$('download-csharp').onclick=()=>download('csharp');
  $('copy-code').onclick=async()=>{const text=$('export-code').value;try{await navigator.clipboard.writeText(text);$('copy-state').textContent='已复制';}catch{$('export-code').select();$('copy-state').textContent=document.execCommand('copy')?'已复制':'请按 Ctrl+C 复制';}};
  const sample=$('sample-window');let pointer=null;
  sample.querySelector('.sample-title').addEventListener('pointerdown',e=>{pointer={x:e.clientX,y:e.clientY,left:sample.offsetLeft,top:sample.offsetTop};e.currentTarget.setPointerCapture(e.pointerId);});
  sample.querySelector('.sample-title').addEventListener('pointermove',e=>{if(!pointer)return;sample.style.left=Math.max(-sample.clientWidth+40,Math.min($('scene').clientWidth-40,pointer.left+e.clientX-pointer.x))+'px';sample.style.top=Math.max(0,Math.min($('scene').clientHeight-40,pointer.top+e.clientY-pointer.y))+'px';});sample.querySelector('.sample-title').addEventListener('pointerup',()=>pointer=null);
  $('scene').dataset.background=background;new ResizeObserver(fit).observe($('scene'));controls();moduleList();render();
  // Read-only hooks for a repeatable local browser check, with no Windows API bridge.
  window.ControlCenterDesigner={export:documentData,layout,validate,outline};
})();
