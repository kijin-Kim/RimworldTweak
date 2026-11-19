"""Embed local reference previews into the report. No network or game mutation."""
import base64
import json
import re
from pathlib import Path

root = Path(__file__).resolve().parent
manifest = json.loads((root / 'local-art-inventory.json').read_text(encoding='utf-8'))
focus = {
    'tree': ('나무', '수관이 커져도 선택한 정착민·이름은 식별 가능하게 유지'),
    'body-front': ('정착민 몸체', '머리·몸 접점과 앞/뒤/옆 비율 통일. 이름·소집은 별도 표시'),
    'stove': ('조리대', '원본은 224×96. 그림 폭과 무관하게 기존 단일 위치·Y-1 사용 위치 유지'),
    'door': ('문', '원본은 움직이는 문짝 부품. 새 문 잠금·통행 규칙을 아트에 섞지 않음'),
    'crop-mature': ('작물', '새싹·성장·성숙을 기존 성장값으로 표현. 쌀을 게임 작물로 확정한 것은 아님'),
    'meal': ('식사', '작은 화면에서 식재료와 구분. 수량은 그림 밖에 표시'),
}
data = {}
for row in manifest['samples']:
    if row['key'] not in focus:
        continue
    label, rule = focus[row['key']]
    data[row['key']] = dict(label=label, focus=rule, width=row['width'], height=row['height'],
        filename=Path(row['source']).name,
        image='data:image/png;base64,' + base64.b64encode((root / row['preview']).read_bytes()).decode('ascii'))
path = root / 'art-reference-explorer.html'
html = path.read_text(encoding='utf-8')
html, count = re.subn(r'(<script type="application/json" id="art-data">).*?(</script>)',
    lambda m: m[1] + json.dumps(data, ensure_ascii=False) + m[2], html, flags=re.S)
assert count == 1
assert set(re.findall(r'<option value="([^"]+)"', html)) == set(data)
assert len(html.encode('utf-8')) < 1_000_000
path.write_text(html, encoding='utf-8')
print('Embedded', len(data), 'verified reference previews;', len(html.encode('utf-8')), 'bytes')
