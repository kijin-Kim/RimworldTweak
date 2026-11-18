# 정착민 몸·의복·헤어 원화 출처

프로젝트용 독자 원화다. 사용자가 선택한 생성 시안 두 장을 보며 윤곽선, 팔레트, 체형과 옷 겹침을 새 곡선으로 그렸다. 상용 게임의 원본 이미지나 모드 이미지를 추출·합성한 에셋은 없다.

| 자료 | 위치 및 용도 |
|---|---|
| 체형과 의상 시안 | `C:/Users/kijin/.codex/generated_images/01a1e95a-07f4-79c0-b49d-218d92d6032b/exec-965faa00-4739-4736-90ee-2cc145c58297.png`에서 얼굴 표현, 외곽선, 기본색, 열린 재킷의 겹침을 참고했다. |
| 헤어와 실루엣 시안 | 같은 폴더의 `exec-9354d018-47c2-49b6-bbc6-21f64beb6fa1.png`에서 짧은 머리·단발·묶음머리, 가는 허리·넓은 어깨·둥근 체형을 참고했다. |
| 원화 소스 | [Build-PawnLayers.py](../../Tools/Build-PawnLayers.py). 몸통은 체형마다 어깨·허리·밑단의 곡선을 따로 설정했다. 옷깃, 얼굴 바탕과 헤어는 방향별로 그렸다. |
| 게임용 출력 | [PawnLayers](../../Assets/Game/Content/Art/Generated/PawnLayers). PNG 39장. |

## 합성 규격

512×512 RGBA 전체 캔버스를 사용한다. 0.95×0.95 월드 크기, 바닥 접점은 왼쪽 아래 기준 (0.5, 0.05)다. 몸 → 셔츠 → 선택한 재킷 → 얼굴 → 헤어 순으로 합성한다. 측면은 오른쪽을 보며 반대 방향에서는 완성 실루엣 전체를 반전한다. PNG별로 투명 여백을 자동 제거하면 목과 옷깃 위치가 달라진다.

| 부위 | 키 | 수량 |
|---|---|---:|
| 몸·셔츠·재킷 | `pawn.{body,shirt,jacket}.{slim,broad,round}.{front,back,side}` | 27 |
| 얼굴 바탕 | `pawn.face.{front,back,side}` | 3 |
| 헤어 | `pawn.hair.{short,bob,bun}.{front,back,side}` | 9 |

## 제작 검증

1. 39장 모두 512×512 RGBA이며 투명도 최솟값 0, 최댓값 255다. 모든 몸과 셔츠의 불투명도 128 이상 밑면이 위에서 485번 픽셀에 있다.
2. 세 체형의 앞 재킷 중앙 (256, 380)은 알파 0, 등판의 같은 지점은 알파 255다. 셔츠를 갈아입히면 열린 앞섶에 아래 셔츠가 보인다.
3. 세 체형과 세 헤어를 각각 세 방향으로 조합한 27칸을 105픽셀 타일로 합성해 목·머리·옷깃을 확인했다. 테스트 이미지 위치는 `TestResults/pawn-layer-proof.png`다. Unity 내부 표시와 실제 플레이의 판독성은 통합 검증 기록을 따른다.

원화를 다시 만들 때는 Python, PyMuPDF, NumPy, Pillow를 준비하고 `python Tools/Build-PawnLayers.py --proof TestResults/pawn-layer-proof.png`를 실행한다. 이 명령은 원화 입력을 갱신하므로 Unity 검증의 입력 동결이 끝난 뒤 사용한다.
