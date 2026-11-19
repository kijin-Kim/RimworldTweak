"""Embed already inspected reference PNGs; check markup without UI automation."""
import base64
import json
import re
from pathlib import Path

root=Path(__file__).resolve().parent
path=root/'art-production-cases.html'
html=path.read_text(encoding='utf-8')
keys=set(re.findall(r'data-art="([^"]+)"',html))
data={key:'data:image/png;base64,'+base64.b64encode((root/'local-reference'/f'{key}.png').read_bytes()).decode('ascii') for key in keys}
html,n=re.subn(r'(<script type="application/json" id="production-art-data">).*?(</script>)',lambda m:m[1]+json.dumps(data)+m[2],html,flags=re.S)
assert n==1
assert len(html.encode('utf-8'))<1_000_000
ids=re.findall(r'\bid="([^"]+)"',html)
assert len(ids)==len(set(ids))
for target in re.findall(r"querySelector\('#([^']+)'\)",html):
    assert target in ids,target
assert 'ベ' not in html
path.write_text(html,encoding='utf-8')
print('PASS: five embedded reference images, unique IDs and literal selector targets;',len(html.encode('utf-8')),'bytes')
