const fs = require('node:fs');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const css = fs.readFileSync('MuseTheme.css','utf8');
const script = fs.readFileSync('MuseTheme.js','utf8').replace('__MUSEBUBBLE_CSS__', JSON.stringify(css));
function setup(host='muse.ai', framed=false, early=false) {
 const attrs={}; const styles=[]; const listeners={}; const marked={};
 const root={setAttribute:(k,v)=>attrs[k]=v,removeAttribute:k=>delete attrs[k]};
 const box={parentElement:null,getBoundingClientRect:()=>({height:72,width:350}),querySelector:()=>({}),querySelectorAll:()=>[{}],setAttribute:(k,v)=>marked[k]=v};
 const input={parentElement:box,getClientRects:()=>[{}],onclick:'untouched',value:'draft survives'};
 const doc={documentElement:early?null:root,head:early?null:{appendChild:x=>styles.push(x)},body:root,
 getElementById:id=>styles.find(x=>x.id===id),createElement:()=>({}),querySelectorAll:()=>[input],addEventListener:(k,v)=>listeners[k]=v};
 const win={};win.top=framed?{}:win;
 const context={window:win,document:doc,location:{hostname:host,protocol:'https:'},MutationObserver:class{observe(){}},requestAnimationFrame:fn=>fn()};
 vm.runInNewContext(script,context);
 return {attrs,styles,listeners,root,doc,win,input,marked};
}
let s=setup();s.win.__museBubbleTheme('light',true);assert.equal(s.attrs['data-musebubble-theme'],'light');
assert.equal(s.styles.length,1);assert.equal(s.styles[0].textContent,css);assert.ok('data-musebubble-composer' in s.marked);
s.win.__museBubbleTheme('dark',true);assert.equal(s.attrs['data-musebubble-theme'],'dark');assert.equal(s.styles.length,1);
s.win.__museBubbleTheme('dark',false);assert.equal(s.attrs['data-musebubble-theme'],undefined);
assert.equal(s.input.onclick,'untouched');assert.equal(s.input.value,'draft survives');
assert.equal(setup('muse.ai.evil.example').win.__museBubbleTheme,undefined);
assert.equal(setup('muse.ai',true).win.__museBubbleTheme,undefined);
s=setup('muse.ai',false,true);s.doc.documentElement=s.root;s.doc.head={appendChild:x=>s.styles.push(x)};
s.listeners.DOMContentLoaded();s.win.__museBubbleTheme('light',true);assert.equal(s.styles.length,1);
console.log('PASS page theme: origin/frame guard, early document mounting, light/dark toggle, disable, single stylesheet, composer tagging, input handler/draft preservation.');
function luminance(hex){let rgb=hex.match(/\w\w/g).map(x=>parseInt(x,16)/255).map(x=>x<=.04045?x/12.92:((x+.055)/1.055)**2.4);return rgb[0]*.2126+rgb[1]*.7152+rgb[2]*.0722;}
function contrast(a,b){let x=luminance(a),y=luminance(b);return (Math.max(x,y)+.05)/(Math.min(x,y)+.05);}
for(const [name,inks,surfaces,accent] of [['light',['20212A','626574'],['F8F8FC','FFFFFF','EEEFF7','E9EBF6','F6F5FF'],'0066E8'],['dark',['F5F5FA','B6BAC9'],['20222D','303340','383C4B','3B3D50','2C2F3E'],'6AA5FF']]){
 for(const ink of inks)for(const surface of surfaces){const ratio=contrast(ink,surface);assert.ok(ratio>=4.5,`${name} ${ink} on ${surface}: ${ratio}`);}
 for(const surface of surfaces)assert.ok(contrast(accent,surface)>=3,`${name} focus on ${surface}`);
 console.log(`PASS ${name} palette: all text roles >=4.5:1, focus >=3:1.`);
}
