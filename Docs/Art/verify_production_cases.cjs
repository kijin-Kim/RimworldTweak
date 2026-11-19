// Offline control/data checks only. No browser render or UI automation.
const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm'),assert=require('node:assert/strict');
const html=fs.readFileSync(path.join(__dirname,'art-production-cases.html'),'utf8');
const payload=html.match(/<script type="application\/json" id="production-art-data">([\s\S]*?)<\/script>/)[1];
const code=html.match(/<script>\s*([\s\S]*?)<\/script>/)[1];
const nodes=Object.fromEntries([...html.matchAll(/\bid="([^"]+)"/g)].map(m=>[m[1],{
  value:'',hidden:false,attrs:{},events:{},style:{setProperty(k,v){this[k]=v;}},
  setAttribute(k,v){this.attrs[k]=v;},addEventListener(k,f){this.events[k]=f;}
}]));
nodes['production-art-data'].textContent=payload;
nodes['production-case'].value='view';nodes['crop-size'].value='48';nodes['sprite-version'].value='100';
const images=[...html.matchAll(/<(image|img)\b[^>]*data-art="([^"]+)"/g)].map(m=>({tagName:m[1],dataset:{art:m[2]},setAttribute(k,v){assert.ok(v?.startsWith('data:image/png;base64,'));this[k]=v;}}));
const root=nodes['art-production-cases'];
root.querySelector=s=>{assert.ok(nodes[s.slice(1)],s);return nodes[s.slice(1)];};
root.querySelectorAll=s=>{assert.equal(s,'[data-art]');return images;};
const listeners={};
vm.runInNewContext(code,{document:{getElementById:id=>nodes[id]},window:{addEventListener(k,f){listeners[k]=f;}}});
for(const topic of ['view','read','anchor']){
 nodes['production-case'].value=topic;nodes['production-case'].events.change();
 for(const k of ['view','read','anchor'])assert.equal(nodes['case-'+k].hidden,k!==topic);
}
for(const size of [32,48,64,80,96]){
 nodes['crop-size'].value=String(size);nodes['crop-size'].events.input();
 assert.equal(root.style['--crop-size'],size+'px');
}
for(const h of [60,100]){
 nodes['sprite-version'].value=String(h);nodes['sprite-version'].events.change();
 const a=nodes['foot-building'].attrs,b=nodes['center-building'].attrs;
 assert.equal(+a.y + +a.height,150);assert.equal(+b.y + +b.height,120+h/2);
 assert.ok(+a.y>=0 && +b.y>=0);
}
listeners['openai:set_globals']({detail:{globals:{widgetState:{modelContent:{topic:'read',size:32,height:60}}}}});
assert.equal(nodes['case-read'].hidden,false);
assert.equal(root.style['--crop-size'],'32px');
console.log('PASS: 3 topics, 5 sizes, 2 replacement heights, state restore and all embedded image references. Rendering not inspected.');
