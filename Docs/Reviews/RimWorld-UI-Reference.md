# 림월드 PC 기본판 화면과 조작 분석

림월드의 정착지 화면은 지도를 화면 전체에 깔고, 가장자리에 자주 보는 정보와 조작만 남긴다. 하단 메뉴는 정착지 전체를 관리하는 입구다. 좌하단은 지금 고른 대상이나 배치 도구에 따라 내용이 바뀐다. 맵의 작업 표시와 선택 테두리가 바깥쪽 UI의 정보를 실제 대상에 연결한다. RimworldTweak에서 적용할 배치, 데이터 연결, 입력 우선순위, 인수 기준은 [구현 인계](../Plans/RimWorld-UI-Handoff.md)를 따른다.

## 1. 분석 기준과 이미지

- 설치본은 `C:\Program Files (x86)\Steam\steamapps\common\RimWorld`의 PC판 `1.7.5011 rev24`다. `Version.txt`, `Assembly-CSharp.dll`의 관련 화면·입력 타입, `Data/Core/Defs/Misc`의 `MainButtonDefs/MainButtons.xml`, `KeyBindings/KeyBindings.xml`, `Designations/DesignationCategories.xml`을 읽었다. 타입과 정의는 설치 버전의 배치·호출 흐름을 확인하는 근거다.
- 아래 공식 이미지와 위키의 화면 예시를 직접 열어 확인했다. 캡처별 버전, 배율, 활성 확장팩은 확인할 수 있는 범위가 다르다. 표의 픽셀은 해당 이미지에서 읽은 근삿값이다. 설치본에서 직접 조작하거나 새로 캡처하는 검사는 수행하지 않았다.
- 1차 범위는 기본판의 시작·정착지·관리 화면·행성 지도·저장 UI다. 확장팩은 기존 슬롯에 어떤 종류의 메뉴가 들어가는지 파악하는 참고로만 사용한다. 이미지는 비교 자료이고 구현에 쓸 아이콘·초상·패널 원화는 프로젝트 아트에서 준비한다. [공식 스토어](https://store.steampowered.com/app/294100/RimWorld/) · [UI 사진 모음](https://rimworldwiki.com/wiki/User_interface#Gallery) · [아트 인계](../Art/Handoff.md)

**공식 정착지 화면 A · 1920×1080**

![림월드 공식 상점 스크린샷. 화면 가장자리의 정착민 초상, 물자 목록, 하단 메뉴, 선택 정보, 우측 경고와 시간 버튼](https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/294100/80e383ef19353058791efe17a6485849246c9c17/ss_80e383ef19353058791efe17a6485849246c9c17.1920x1080.jpg)

좌하단의 작은 Lopez 정보 요약에서 Health 탭을 열면 위쪽에 큰 상세 카드가 붙는다. 같은 정착민을 상단 초상 테두리와 맵 중앙의 흰 선택 표시에서 찾을 수 있다. 소집 해제와 근접 공격은 요약 오른쪽에 있고, 상태를 읽는 카드와 누르는 버튼을 시각적으로 구별한다. 우측 경고는 붉은 긴급 상태와 일반 안내를 한 열에 표시하고 아래에 날씨·시각·배속을 배치한다. [공식 화면 A](https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/294100/80e383ef19353058791efe17a6485849246c9c17/ss_80e383ef19353058791efe17a6485849246c9c17.1920x1080.jpg) · [공식 화면 B](https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/294100/698ad21bff61006f31650270446d68cd1073c770/ss_698ad21bff61006f31650270446d68cd1073c770.1920x1080.jpg)

## 2. 정착지 화면 구조

좌상단을 원점으로 본 공식 화면 A의 관찰값이다. 선택 상세가 열린 사례이므로 대기 화면의 가림 면적은 더 작다.

| 화면 구역 | 공식 화면 A에서 보이는 위치와 표현 | 플레이어가 여기서 하는 일 |
|---|---|---|
| 정착지 지도 | 화면 전체에 지형·시설·캐릭터가 이어진다. 상단·우측 UI 뒤에도 지도 픽셀이 보인다 | 상황 파악, 직접 선택, 대상·범위 지정 |
| 자원 목록 | 좌상단부터 약 75 px 폭의 아이콘·수량 열. B에서 접고 펼칠 수 있는 분류 화살표 확인 | 보유 물자 읽기, 분류로 보기 |
| 정착민 초상 | 상단 중앙. A의 10명은 약 x=595–1298, y=8–80. 이름·상태 아이콘을 초상에 붙임 | 정착민 선택, 더블클릭으로 카메라 이동, 소집·쓰러짐 등 상태 인식 |
| 하단 주 메뉴 | y≈1045부터 화면 끝까지 약 35 px. 어두운 바탕과 가는 테두리의 연속 버튼 | 건축·작업·일정·정책·동물·야생동물·연구·퀘스트·세계·기록·세력·메뉴 접근 |
| 선택 요약 | x≈0–431, y≈880–1044. 위쪽 약 30 px에 세부 탭. 요약에는 이름·상태·현재 행동 | 선택 대상과 활동 확인. 정착민이면 기록·장비·사회·정보·욕구·건강으로 들어감 |
| 선택 상세 | Health 예시 x≈0–629, y≈420–850. 요약 위에 탭별 내용 추가 | 더 많은 정보를 필요할 때 읽거나 설정. 조사 패널의 크기는 내용에 따라 다름 |
| 대상 명령 | A에서 x≈445–746, y≈955–1042. 상태 위젯과 약 75 px 정사각형 명령 타일, 아이콘·핫키·캡션 | 소집, 이동·공격 관련 명령, 선택물에 따라 철거·허용·저장 등 실행 |
| 우측 상황 정보 | 우측 가장자리를 기준으로 쌓인다. A의 긴급 목록은 y≈574부터, B는 y≈648부터 | 지속 문제 파악. 사건 편지는 열어 내용을 읽고 대상 위치로 이동 |
| 시간·표시 | 우하단. 시간·날짜·계절/날씨 아래에 일시정지와 배속 아이콘, 그 아래에 표시 토글 | 시뮬레이션 시간 제어. 구역·지붕·뷰 정보 등 표시 전환 |
| 커서 칸 정보·메시지 | 칸 정보는 좌하단의 상황별 작은 텍스트. 메시지·학습 도우미는 상태와 표시 옵션에 따라 노출 | 타일·시설 판독, 즉시 알림, 사용법 확인 |

배경이 복잡한 곳에서는 자원 수치·이름의 외곽선이나 그림자가 읽기를 돕고, 편집·상세 패널은 진한 불투명 바탕으로 여러 줄을 읽기 좋게 만든다. 중립 상태에는 밝은 본문과 낮은 채도의 버튼을 쓰고, 선택 브래킷·체크·금지 X·긴급 배경 같은 표식을 실제 상태와 연결한다. 화면을 덮는 진한 패널은 주로 읽거나 편집할 때만 등장한다. [자원 목록](https://rimworldwiki.com/wiki/File:Interface_resource_list_categorized.png) · [선택 요약](https://rimworldwiki.com/wiki/File:Interface_inspect_pane.png) · [시간 아이콘](https://rimworldwiki.com/wiki/File:Interface_time_speed_control.png) · [경고와 편지](https://rimworldwiki.com/wiki/File:Interface_alerts_suggestions.png)

설치본 확인 위치는 `MapInterface`와 `UIRoot_Play`(그리기/입력), `MainButtonsRoot`(하단 높이 35 논리 단위), `MainTabWindow_Inspect`(하단 탭 위 선택 요약), `GizmoGridDrawer`(문맥 명령 타일), `ResourceReadout`, `ColonistBar`, `GlobalControls`, `TimeControls`, `AlertsReadout`, `LetterStack`이다. 예를 들어 `GlobalControls`는 우하단부터 날짜·날씨 등을 쌓으며, `LetterStack`은 별도로 편지 버튼을 그린다. 위키의 우상단 안내 설명과 공식 캡처를 함께 볼 때에는 지속 경고·편지·학습 도우미를 각각 확인한다.

## 3. 맵에서 수행하는 일

| 조작 | 원본의 정보·상태 변화 | 확인 근거 |
|---|---|---|
| 맵에 도착해서 탐색 | 이동·줌으로 월드 위치를 바꾼다. HUD의 논리 위치는 그대로다. 클릭 시 대상에 선택 표시가 생기고 좌하단이 바뀐다 | 키·카메라 기본 동작 [Controls](https://rimworldwiki.com/wiki/Controls). 설치본 `CameraDriver`, `Selector` |
| 정착민 선택 | 좌클릭으로 조회. 복수 선택은 Shift·박스 선택 등. 초상 좌 더블클릭 시 위치로 이동. 같은 칸의 선택 후보를 바꾸는 경로도 있다 | 설치본 `Selector`, `ColonistBarColonistDrawer.HandleClicks`. [Inspect](https://rimworldwiki.com/wiki/User_interface) |
| 정착민에게 직접 명령 | 대상에 맞는 우클릭 선택지가 포인터 위치에 열릴 수 있다. 명령 후보가 바로 실행되는 경로도 있다. 소집된 정착민의 이동과 일반 작업 강제 우선은 서로 다른 명령이다 | 설치본 `Selector.HandleMapClicks` 및 `FloatMenuMakerMap`. [Orders](https://rimworldwiki.com/wiki/Orders) |
| 건축가 열기 | 좌하단 카테고리 버튼과 검색. 카테고리 선택 시 작업 지정 버튼과 설명·비용. 도구 활성 상태에서는 클릭/드래그가 대상 선택 대신 지도에 작업을 표시 | 설치본 `MainTabWindow_Architect`, `DesignatorManager`. 좌하단 카테고리 기본 폭 200, 버튼 높이 32 논리 단위. [Architect](https://rimworldwiki.com/wiki/Architect) · [카테고리 화면](https://rimworldwiki.com/wiki/File:ArchitectMenu.png) |
| 구역/건설 지정 | 지도 위 면·선·고스트로 범위를 판단하고 확정 뒤 구역 또는 설계 표시가 남는다. 가령 벽 건설은 설계 표시가 먼저 생기고 정착민이 이후 수행한다 | [Zone](https://rimworldwiki.com/wiki/Zone) · [1.6 공식 변경 기록의 그리기 방식](https://ludeon.com/blog/2025/06/announcing-odyssey-and-update-1-6/) |
| 집중 조회와 작업 편집 | 저장 구역은 저장 허용 규칙, 재배 구역은 작물과 파종, 시설은 해당 시설 명령을 제공한다. 조리대처럼 작업 가능한 시설에서 주문 목록을 열면 목표·중지·삭제·상세 조건을 다룬다 | [Zone](https://rimworldwiki.com/wiki/Zone) · [Bills](https://rimworldwiki.com/wiki/Bills) · [조리 주문 예시](https://rimworldwiki.com/wiki/File:Work_bill_meals_1.png) |
| 취소·정지 | Esc로 현재 대화나 도구를 벗어나며 마지막에 메뉴로 간다. Space로 정지 전환. 시간 아이콘은 현재 단계와 별도 표시 옵션을 보여 준다 | 설치본 `UIRoot_Play`, `DesignatorManager`, `WindowStack`, `TimeControls`. [Controls](https://rimworldwiki.com/wiki/Controls) |

설치본에서 창의 입력 처리, 맵 클릭, 선택된 지정 도구, 낮은 우선순위의 단축키 처리 순서를 확인했다. 원본의 복수 선택, 우클릭 상황 메뉴, 여러 그리기 방식은 프로젝트에서도 유용할 수 있으나 각각 대상 모델과 명령 규칙이 생기는 시점에 따로 설계할 항목이다.

## 4. 전역 화면과 메뉴 진입점

| 화면 | 정보 배치와 용도 | RimworldTweak 연결 |
|---|---|---|
| 메인 / 일시정지 메뉴 / 설정 | 시작 배경 위 진입 버튼. 플레이 중 메뉴는 오른쪽 앵커의 정지 메뉴에서 저장·불러오기·설정·종료 등으로 이어짐. 저장은 파일 목록과 이름, 덮어쓰기 확인. 불러오기는 목록과 검색 | 메인/정지/슬롯/배율 화면 대응. 현재 프로젝트의 메뉴 차단·최근 슬롯·확인 흐름 사용. 설치본 `MainMenuDrawer`, `MainTabWindow_Menu`, `Dialog_SaveFileList`, `Dialog_SaveFileList_Load/Save`. [공식 저장 검색 변경 기록](https://ludeon.com/blog/2025/06/announcing-odyssey-and-update-1-6/) |
| 새 게임 | 시나리오 → 이야기꾼·난이도 → 세계 생성 옵션 → 시작 타일 → 초기 정착민 → 지역 지도 진입. 타일 선택은 행성과 좌측 타일 정보, 정착민 구성은 명단 옆 특성·능력·건강·요약 | 현재는 고정 시나리오 새 정착지. 설정 단계는 관련 시뮬레이션이 생길 때 연결. [게임 생성](https://rimworldwiki.com/wiki/Game_Creation) · [공식 초기 정착민 화면](https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/294100/57c3e8d556d47bb5ad048699643528aefc652aa6/ss_57c3e8d556d47bb5ad048699643528aefc652aa6.1920x1080.jpg) |
| Work | 하단에서 넓은 표. 행은 정착민, 열은 작업 유형. 켜기/끄기나 수동 우선순위를 셀에 배치. 원본 숫자는 1–4 | 작업 배분 표로 대응. 프로젝트는 끔/1–3. [우선순위 화면](https://rimworldwiki.com/wiki/File:Work_priorities.jpg) · [Work](https://rimworldwiki.com/wiki/Work) |
| Schedule / Assign | 일정의 24시간 셀과 허용구역은 정착민별. 정책 표는 복장·음식·약물 등의 설정을 각 정착민에 할당 | 각각 시간표와 정책 확장 시 같은 행 구조 활용. [일정 화면](https://rimworldwiki.com/wiki/File:Restrict_menu.png) · [정책 화면](https://rimworldwiki.com/wiki/File:Mainmenu_assign.png) |
| Animals / Wildlife | 정착지 소유 동물의 설정과 맵 야생동물 목록을 나누어 보여 줌 | 동물 기능의 후속 표. [동물 목록 화면](https://rimworldwiki.com/wiki/File:Animals_menu.png) · [Menus](https://rimworldwiki.com/wiki/Menus) |
| Research | 연구 항목과 선행 관계를 넓은 그래프에서 읽고 해당 연구를 고름 | 연구가 생길 때 그래프와 선택 상세 사용. [연구 화면](https://rimworldwiki.com/wiki/File:Research_menu.png) |
| Quests / History / Factions | 퀘스트 목록+상세, 사건·통계 기록, 세력별 관계와 정보 | 해당 도메인의 데이터가 생긴 뒤 각 화면 추가. [Menus](https://rimworldwiki.com/wiki/Menus) |
| World | 정착지 맵에서 행성 지도로 전환. 타일·정착지·이동 집단을 고르면 타일·세력 정보와 원정 관련 명령. 원래 정착지로 복귀 가능 | 현재 `WorldMapView`는 로컬 정착지의 탑다운 카메라. 원정용 행성 화면은 별개 기능. [World](https://rimworldwiki.com/wiki/World) · [공식 시작 위치 화면](https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/294100/59a1400eff99e2b620d623d3d76a02c1592a72e7/ss_59a1400eff99e2b620d623d3d76a02c1592a72e7.1920x1080.jpg) |

설치본에서는 `MainButtons.xml`이 기본 메뉴 순서, `MainTabWindow_PawnTable`이 관리 표의 공통 틀, `WorldInterface`가 로컬과 행성 지도 모드 전환의 근거다. 시작 위치의 공식 캡처는 새 게임 흐름의 사진이다. 플레이 도중의 World 조작은 설치본과 World 문서에서 확인했다.
