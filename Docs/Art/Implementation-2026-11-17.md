# 교체 가능한 아트 연결과 기준 샘플 구현

작성: 2026-11-17. 담당: verification_review. 상태: 코드·독자 기준 원화 작성, Unity import/실제 게임 렌더 검증 전.

## 현재 범위와 제작 결과

2026-11-17 접점 후속 보정: tree 원본 1254×1254의 alpha≥128 밑면 y=1122를 기준으로 groundAnchorNormalized.y를 0.12→132/1254(0.10526316)로 조정했다. 원본 PNG/크기/맵 좌표는 그대로이며 Catalog와 BuildArtAssets를 함께 갱신했다. 기본 해상도에서 기존 약 2.5px 밑면 오차를 줄였다. `20261117T144621106Z-ux-capture/capture/sample-five-offscreen.png`에서 정착민·나무·침대·지면·선택 패널을 함께 정적 검토했고 UI 가림/게임 상태 변화는 없었다. 진단 RenderTexture이며 정상 창과 최종 아트 채택은 미확인이다.

후속 제작/검증 (root,2026-11-17): 기준 앞모습을 참조해 내장 imagegen으로 뒤/오른쪽 옆모습을 제작하고 Generated 폴더에 원본 그대로 복사했다. 뒤/옆은 앞과 같은1254×1254 RGBA,alpha0~255이며 같은.95×.95 WorldSize와(.5,.05)바닥 접점을 사용한다. alpha≥128영역은 뒤(387,81,866,1198),옆(405,75,798,1194)다. 앞의 하단1195와 비교하면3픽셀 이내이며 이미지를 별도로 보정하지 않았다.

| 키 | 프로젝트 파일 | 생성 원본 |
|---|---|---|
| pawn.back | `Assets/Game/Content/Art/Generated/pawn-back.png` | `C:/Users/kijin/.codex/generated_images/01a1e95a-07f4-79c0-b49d-218d92d6032b/exec-fcb9e933-db88-40b3-a325-621d7616192c.png` |
| pawn.side | `Assets/Game/Content/Art/Generated/pawn-side.png` | `C:/Users/kijin/.codex/generated_images/01a1e95a-07f4-79c0-b49d-218d92d6032b/exec-bc6fde77-316a-44e6-b547-548f2c53f007.png` |

앞과 같은 갈색 머리·청록 작업복·회색 바지·갈색 부츠, 위쪽 왼쪽 광원·외곽선·전체 높이·중앙 발 위치를 유지하도록 방향별로 지시했다. 생성 원본의 방향과 외형을 눈으로 확인했고 BuildArtAssets에서 해당 키를 새 원화로 교체했다. `TestResults/20261117-art-directions.log`의 Unity import 종료0을 확인했다. 최종 전체 아트 채택/사람 확인은 대기다.

`20261117T122332034Z-ux-capture`와 `20261117T122944437Z-ux-capture`의 `*-offscreen.png`는 실제 Windows Player의 URP 카메라를 RenderTexture에 렌더한 진단 이미지다. 맵 카메라 출력과 같은 HUD를 화면용 카메라 캔버스로 합성하며 일반 창 backbuffer 캡처와 구분한다. 후자의 `pawn-directions-offscreen.png`에서 세 방향의 연결과 한 칸 지면을 정적 확인했다. 원래 창 캡처 PNG는 여전히 단색 실패다.

정적 이미지에서 확인한 지면 오류를 수정했다.512크기/128PPU의 지면 Sprite가4칸을 덮던 것을 Tile.transform으로1칸에 맞췄다. 이름이 작게 보이던 문제는 화면 크기16픽셀 기준으로 보완했으며, 확대된 이름끼리 겹치는 문제에 대해 화면 영역 배치 처리를 추가했다. 최신 이름 겹침 수정의 렌더 재검토는 다음 실행에서 확인한다. 아래 최초 작성 기록의 미실행 표기는 당시 상태다.

imagegen skill을 읽고 **기본 내장 이미지 생성 도구**를 사용했다. CLI/API fallback은 사용하지 않았다. RimWorld 원본은 반입하지 않았다. 자연물·정착민·침대·지면은 독자 원화이며 규격 UI/선택/벽은 코드 도형이다. 실제 Unity 기준 장면을 확인하기 전 나머지 원화를 대량 생성하지 않는다.

| 키 | 프로젝트 파일 | 출처/현재 판정 |
|---|---|---|
| tree | `Assets/Game/Content/Art/Generated/tree.png` | 내장 imagegen 독자 생성, 원본 표시 확인·RGBA/alpha 확인, 게임 미검증 |
| pawn.front | `Assets/Game/Content/Art/Generated/pawn-front.png` | 내장 imagegen 독자 생성, 원본 표시 확인·RGBA/alpha 확인, 게임 미검증 |
| building.bed | `Assets/Game/Content/Art/Generated/bed.png` | 내장 imagegen 독자 생성, 원본 표시 확인·RGBA/alpha 확인, 게임 미검증 |
| terrain.soil | `Assets/Game/Content/Art/Generated/soil.png` | 내장 imagegen 독자 생성, 불투명 RGB, 반복 타일 게임 검증 전 |
| ui.panel | `Assets/Game/Content/Art/Structural/panel.png` | Editor 함수가 생성하는 코드 도형, 실행 전 파일 미생성 |

원화4개는 모두1254×1254이며 원본을 그대로 복사했다. Unity importer에서 최대512,128PPU 후보로 가져오고 Catalog의 WorldSize로 실제 표시 크기를 정한다. 원화 자체를 Python 등으로 편집하지 않았다. PIL은 크기/alpha/가시 영역 검사만 수행했다. 투명 이미지 alpha 범위는0~255. alpha≥128의 실측 영역(왼쪽,위,오른쪽,아래): tree(86,134,1167,1122), pawn-front(374,84,873,1195), bed(370,154,883,1128). 아주 낮은 alpha의 외곽 픽셀이 있어 실제 작은 크기의 투명 가장자리는 게임 렌더 확인이 필요하다.

원본 보존 폴더: `C:/Users/kijin/.codex/generated_images/01a1e8ef-051f-7853-856b-95632ce9e8cc/`.

| 프로젝트 이름 | 생성 원본 |
|---|---|
| tree | exec-bf93eebd-d4af-4632-8c9b-1592e59b8235.png |
| pawn-front | exec-6160f572-2453-438c-a4f3-8b898b3ae79c.png |
| bed | exec-2d51feb6-2e3f-437c-b5ed-ca20a2c854ef.png |
| soil | exec-daebace5-eab6-4176-a0ca-5c078a73b66e.png |

## 실행 및 API

root가 Unity를 직렬 실행해 `RimworldTweak.Editor.BuildArtAssets.Build()`를 호출한다. 원화가 없으면 실패하고 기존 그림을 조용히 다른 파일로 대체하지 않는다. 생성 전용 Structural 폴더에 PNG를 만들고 importer·9-slice panel·DefaultVisualCatalog.asset을 저장한다. 제품 테스트 입력 동결 **전** 수행한다. 함수는 원화/게임 규칙을 수정하지 않는다.

Catalog asset: `Assets/Game/Content/Visuals/DefaultVisualCatalog.asset`. 런타임에서는 `Resources.Load` 대신 root가 Scene/Bootstrap에 명시 참조로 연결한다. `VisualCatalog.TryGet(key,out VisualDefinition)`로 조회하며 Definition은 Sprite/WorldSize/GroundAnchorNormalized/Tint/IsFinalArt/Source를 제공한다. `PivotOffset`과 `Scale`로 그림 pivot이 바뀌어도 기록된 바닥 접점을 유지한다. 맵 담당과 north=1/east=2/south=4/west=8 벽 mask, terrain.soil size1×1/center anchor를 합의했다.

키는 terrain2종, pawn3방향, tree/gather, crop3단계, item3종, building4종+wall16조합, ui.panel, overlay4종, icon3종이다. **미제작 방향/작물/물자/조리대는 명시적인 코드 placeholder로 연결**한다. 원화4종도 실제 게임 채택 전이므로 IsFinalArt=false다. 벽/문/선택/UI 규격 도형만 code-native 최종 후보로 true이며 최종 전체 아트 완료를 뜻하지 않는다.

## 검사와 다음 단계

작성한 EditMode 검사는 잘못된 Catalog 교체 거부, pivot·크기 교체의 바닥 접점 보존, 성장 표시 경계, 현재 종류/방향 키, 실제 생성 Catalog 필수 연결이다. 아직 실행하지 않았다. 마지막 검사는 BuildArtAssets.Build 후에만 유효하다.

다음: root가 빌드 함수 실행→기준5종 실제 장면 캡처→크기/정렬/반복/선택 가림 확인→필요한 원화 수정→기준 채택 후 나머지 원화 제작. Renderer 코드 작성·그림 파일 존재·실제 사용자 가독성 확인은 별도 단계로 기록한다.

## 사용한 원화 생성 지시문

아래는 내장 도구에 전달한 최종 프롬프트다. tree/pawn/bed는 transparent_background=true, soil은 false.

### tree

Use case: stylized-concept. Asset type: single original sprite for a top-down 2D colony simulation. Create ONE broad-canopy oak tree, compact rounded layered muted olive-green foliage and a short warm brown trunk. A slightly front-visible top-down game view, consistent upper-left soft lighting, clear dark brown outline, flat painted shapes, restrained detail readable at 64 screen pixels. Center the entire tree with 10 percent transparent margin, tree base at horizontal center near the bottom of its visible bounds. Genuine transparent background. No ground tile, no cast shadow outside tree, no text, no icon, no other objects. Original design, not copied from any game. Square image.

### pawn-front

Use case: stylized-concept. Single original top-down colony game character sprite, facing south/front with visible face. Compact adult colonist, warm skin, short dark brown hair, muted blue-teal work shirt, dark charcoal trousers and sturdy boots. Head slightly large for clear small-scale readability, body simply shaped, arms close to sides. Slightly front-visible top-down orthographic game view, dark brown clean outline and flat softly painted shapes, upper-left light. Centered entire body with empty transparent margin, feet near bottom center. One character only, no ground, no shadow outside body, no props, no text, no UI. Original art, no existing game likeness. Square image. Genuine alpha transparent background.

### bed

Use case: stylized-concept. Single original 2D top-down colony-game wooden bed sprite, headboard toward top/north and footboard at bottom/south. Compact simple timber frame, one cream pillow, muted dusty teal blanket. Mostly viewed from above with a small visible front face. Clean dark brown outlines, flat softly painted shapes, limited warm palette and upper-left light, readable at 64 screen pixels. Center entire furniture in square canvas with transparent margin, bottom of frame is ground anchor. No person, no room, no floor, no drop shadow outside object, no text. Genuine transparent background.

### soil

Use case: stylized-concept. One seamless square ground tile texture for a top-down 2D colony game. Low-contrast desaturated warm grey-brown earth, subtle softly painted irregular patches and tiny sparse stones. Direct overhead view, diffuse light, no directional shadow. Nearly flat and quiet so character silhouettes stand out. Tile fills entire square edge to edge; opposing edges match for seamless repeat. No border, no grid, no plants, no objects, no text. Opaque background.

## 추가 원화 5종 반입 (2026-11-17)

조리대, 채집 식물, 목재, 식재료, 식사에 내장 imagegen 독자 원화를 연결했다. 다섯 원본은 모두 1254×1254 RGBA, alpha 범위 0~255이며 편집 없이 복사했다. 게임 규칙 변경은 없다. Unity BuildArtAssets 실행 exit0, 로그는 TestResults/20261117-art-five-import.log다. 실제 축소 렌더와 사람 식별 확인 전이므로 IsFinalArt=false를 유지한다. 작물 3단계는 아직 코드 placeholder다.

원본 폴더: C:/Users/kijin/.codex/generated_images/01a1e95a-07f4-79c0-b49d-218d92d6032b/

| 반입 파일 | 원본 |
|---|---|
| Generated/stove.png | exec-74274300-33b4-4f5c-ab86-20e8216b7460.png |
| Generated/gather.png | exec-b7ec0cb7-e2b6-4984-baa7-c4400ccaaf39.png |
| Generated/wood.png | exec-175820c6-a5e7-4f0b-81df-76bd17307708.png |
| Generated/ingredients.png | exec-56b3fc84-d562-46f1-a403-378e4ecdbdc5.png |
| Generated/meal.png | exec-84d9ef63-b281-4f89-aeae-52f3a127289f.png |
## 작물 단계 원화 추가 (2026-11-17)

내장 imagegen으로 제작했으며 원본을 편집 없이 Generated 폴더로 복사했다. 세 단계의 크기와 이삭으로 성장 상태를 구분한다. IsFinalArt=false이며 실제 게임 렌더/사람 확인은 남아 있다.

### crop-young

원본: `C:\Users\kijin\.codex\generated_images\01a1e95a-07f4-79c0-b49d-218d92d6032b\exec-c1f4abec-3f76-4acd-8e6b-159c00eb4dbe.png`

프롬프트: Use case stylized-concept. One original sprite for a top-down 2D colony game: a single clump of cultivated grain. Slightly front-visible overhead orthographic view, dark brown clean outline, flat softly painted shapes, restrained desaturated olive green and warm palette, upper-left lighting. Simple strong silhouette readable at 32 screen pixels. Square canvas. Whole plant within canvas, root junction exactly centered horizontally at 82 percent of image height. Transparent background, no soil, pot, floor, shadow outside plant, text, UI or other objects. EARLIEST STAGE: tiny seedling with just three short narrow green leaves, no grain heads. Visible plant from 55 percent down to 82 percent of canvas height, only 25 percent canvas width. Preserve large empty transparent space above the small plant.

### crop-growing

원본: `C:\Users\kijin\.codex\generated_images\01a1e95a-07f4-79c0-b49d-218d92d6032b\exec-4516c3b8-4ff8-45cd-98b9-9dd1ec196a44.png`

프롬프트: Use case stylized-concept. One original sprite for a top-down 2D colony game: a single clump of cultivated grain. Slightly front-visible overhead orthographic view, dark brown clean outline, flat softly painted shapes, restrained desaturated olive green and warm palette, upper-left lighting. Simple strong silhouette readable at 32 screen pixels. Square canvas. Whole plant within canvas, root junction exactly centered horizontally at 82 percent of image height. Transparent background, no soil, pot, floor, shadow outside plant, text, UI or other objects. MIDDLE STAGE: upright leafy tuft with five long broad pointed green leaves and two slim stems, no grain or flowers. Visible plant from 28 percent down to 82 percent of canvas height, 45 percent canvas width.

### crop-mature

원본: `C:\Users\kijin\.codex\generated_images\01a1e95a-07f4-79c0-b49d-218d92d6032b\exec-38267b7c-cdb2-4b03-ac81-d715a40a5925.png`

프롬프트: Use case stylized-concept. One original sprite for a top-down 2D colony game: a single clump of cultivated grain. Slightly front-visible overhead orthographic view, dark brown clean outline, flat softly painted shapes, restrained desaturated olive green and warm palette, upper-left lighting. Simple strong silhouette readable at 32 screen pixels. Square canvas. Whole plant within canvas, root junction exactly centered horizontally at 82 percent of image height. Transparent background, no soil, pot, floor, shadow outside plant, text, UI or other objects. HARVEST STAGE: upright leafy tuft with three thick clearly visible golden grain ears above several olive leaves. Grain heads dominate upper silhouette. Visible plant from 12 percent down to 82 percent of canvas height, 60 percent canvas width.

