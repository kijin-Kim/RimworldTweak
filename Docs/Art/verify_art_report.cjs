// Offline behavior check; does not verify browser rendering or human interaction.
const fs = require('node:fs');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const path = require('node:path');
const html = fs.readFileSync(path.join(__dirname, 'art-reference-explorer.html'), 'utf8');
const json = html.match(/<script type="application\/json" id="art-data">([\s\S]*?)<\/script>/)[1];
const code = html.match(/<script>\s*([\s\S]*?)<\/script>/)[1];
const ids = [...html.matchAll(/\bid="([^"]+)"/g)].map(m => m[1]);
assert.equal(new Set(ids).size, ids.length);
const nodes = Object.fromEntries(ids.map(id => [id, {
  value: id === 'art-target' ? 'tree' : id === 'art-scale' ? '100' : '',
  textContent: id === 'art-data' ? json : '', attrs: {}, events: {},
  setAttribute(k,v) { this.attrs[k]=v; },
  addEventListener(k,f) { this.events[k]=f; }
}]));
nodes['art-reference-explorer'].querySelector = selector => {
  assert.ok(nodes[selector.slice(1)], selector);
  return nodes[selector.slice(1)];
};
vm.runInNewContext(code, {document:{getElementById:id=>nodes[id]},window:{addEventListener(){}}});
const data = JSON.parse(json);
for (const key of Object.keys(data)) {
  nodes['art-target'].value=key;
  nodes['art-target'].events.change();
  assert.ok(nodes['art-detail'].textContent.includes(data[key].focus));
  assert.equal(nodes['art-source'].src, data[key].image);
  for (const scale of [60,100,160]) {
    nodes['art-scale'].value=String(scale);
    nodes['art-scale'].events.input();
    const a=nodes['art-map-image'].attrs;
    assert.ok(+a.x>=0 && +a.x + +a.width<=280);
    assert.ok(+a.y>=0 && +a.y + +a.height<=240);
    assert.equal(Math.round(+a.y + +a.height),200);
  }
}
console.log('PASS: six targets, three scales, image/focus update, fixed baseline, bounds. No browser render tested.');
