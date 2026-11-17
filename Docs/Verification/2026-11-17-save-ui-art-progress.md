# 저장·UI·아트 구현 검증 진행

기준: [수락 기준](../Plans/SaveLoad-UX-Art-Acceptance.md). 전체 완료 기록이 아닌 진행 기록이다. 자급 플레이테스트는 제외한다.

## 실행한 검사

### 작은 화면의 기준 5종 재검사 (2026-11-17)

- 공용 runner.lock을 사용 중인 전투 통합 검사 부모 PID 18696/Unity 실행을 확인하고 입력 변경 없이 대기했다. EditMode→PlayMode→Windows 빌드 진행과 부모 종료를 관측한 뒤 Capture를 실행했다. 다른 검사/잠금을 강제 종료·삭제하지 않았다.
- `20261117T145149033Z-ux-capture`: 1280×720/UI150, Player PID 25076 종료 0/내부 passed true. sample-five의 세 Sprite 경계 UI 가림 없음/전체 저장 상태 불변 true, 전체 layout-failures 0.
- sample-five-offscreen.png를 직접 열어 정착민·나무·침대·지면·좌하단 선택 정보가 동시에 보이는 것을 확인했다. 세 Sprite는 선택 패널 위에 있고, 정착민 이름과 선택 테두리를 구분할 수 있다. 이 검사는 해당 정적 장면과 화면 조합에 한정된다.
- 정상 창 PNG 24개는 단색 실패로 wrapper 종료 1이다. 정상 창 표시/M1~M4 결과가 아니며 전체 완료로 판정하지 않는다. 코드·에셋 변경은 없다.

### 기준 5종 비교 장면과 나무 바닥 접점 (2026-11-17)

- 기존 sample-five는 정착민/나무 일부가 HUD에 가려져 있었다. 해당 기존 화면은 selected로 보존하고, capture 모드 마지막에 8×8 검사 전용 월드의 정착민 (2,3), 나무 (4,3), 침대 (6,3)를 배치했다. 실제 지면과 정착민 선택 패널을 함께 표시한다. 일반 시작 시나리오는 변경하지 않는다.
- Windows 빌드 `20261117T144304513Z-build` 성공/입력 변경 0. `20261117T144342506Z-ux-capture`에서 sample-five 진단 PNG를 직접 열어 세 Sprite가 모두 보이고 지면/선택 패널과 비교 가능한 것을 확인했다. 각 Sprite bounds의 네 모서리/중심이 화면 안·GraphicRaycaster UI 밖인지 검사했고 캡처 전후 전체 상태 해시는 동일했다. sample-five-state.json의 두 조건 true, 전체 배치 문제 0. 정상 창 PNG 24개는 단색 실패 유지다.
- 원본 RGBA의 alpha≥128 밑면을 읽기 전용으로 측정했다. 1254×1254 원본에서 pawn-front의 마지막 불투명 행 경계 y=1195, tree=1122, bed=1128이다. 기존 anchor와의 차이를 130px/칸 기준으로 환산하면 정착민 약 -0.36px, 나무 -2.53px, 침대 +0.07px다. 그림자/안티앨리어싱 전체가 아닌 불투명 밑면 기준이며 기준점 투영 수학 검사와 구분한다.
- 나무 groundAnchorNormalized.y를 0.12에서 (1254-1122)/1254=0.10526316으로 조정했다. 제작 스크립트와 Catalog를 함께 수정하고 원본 이미지는 그대로 유지했다. 나무의 맵 좌표·규칙·크기는 변경하지 않는다. 이 값은 선택한 원본 밑면에 대한 표시 규격이며 모든 대체 이미지의 의미상 접점을 자동 추정하는 규칙이 아니다.
- 접점 수정 후 EditMode 188/188(`20261117T144455347Z-d42c2282`), Windows 빌드 성공/입력 변경 0(`20261117T144548277Z-build`). `20261117T144621106Z-ux-capture`의 1920×1080/UI100 sample-five 진단 PNG를 직접 확인했다. Player 내부 성공/종료 0, UI 가림 검사와 상태 불변 true, 배치 문제 0. 원본 alpha 밑면과 새 anchor의 계산상 오차는 반올림 오차 수준이다. 정상 창 PNG 24개 단색으로 wrapper 종료 1은 유지한다. 정상 창·최종 아트 스타일 수락·M4는 여전히 별도다.

### 건설 도구의 입력 경로 (2026-11-17)

- V-U07: `BuildingPointerPreviewRejectsOccupiedCellAndCancelNeverMovesDraftedPawn`는 소집한 정착민을 선택하고 Wall/Door/Bed/Stove 네 종류에 대해 InputSystem MouseState/KeyboardState → Shell.Update/WorldMapInput.Update/LateUpdate 경로를 사용한다.
- 빈 (4,4)의 hover는 지정 가능, 목재가 있는 (3,2)의 hover는 지정 불가 안내다. 두 hover는 게임 저장 상태를 바꾸지 않는다. 불가 위치에서 press/release하면 도구를 유지하고 오류 이유를 남기며 진행·물자·ID·설계·이동 상태를 보존해야 한다. 우클릭은 도구만 취소하고 소집 정착민을 이동시키지 않는다. Esc는 도구만 취소하고 메뉴를 열지 않는다. 유효 칸 press/release 후 추가 release는 설계도 하나/해당 종류·좌표/도구 종료/Tick 0/대기 명령 0/목재 20이어야 한다.
- 최초 `20261117T143933141Z-ac02cc7e`는 102/103. 실패 원인은 검사에서 배치 거부의 LastCommandError 변경까지 금지한 것이었다. 실패 이유 갱신은 요구된 동작이므로 오류가 비어 있지 않은지 따로 검사하고, 캡처 DTO의 해당 진단 문자열만 정규화해 나머지 전체 상태를 비교하도록 수정했다. 실제 World는 정규화하지 않는다.
- 제품 코드 변경은 없다. SetPreview 소스의 유효/불가 녹색/적색 분기를 확인했으나 이 입력 검사는 색의 시각적 식별성이나 사람 손 조작을 입증하지 않는다. 재실행 `20261117T144108824Z-4520e00e`: PlayMode 103/103, 종료 0, 필수 목록 일치/issues 0.

### 전환 중 키 홀드의 Shell 통합 검사 (2026-11-17)

- V-U11의 드래그 통합 검사와 달리 키 홀드는 WorldMapInput 단독 차단 검사까지만 있었다. `HeldKeysRequireFreshPressAfterMenuFocusResetAndLoad`를 추가해 실제 InputSystem KeyboardState → SettlementShell.Update/WorldMapInput.Update 경로로 W와 R을 전달했다.
- 메뉴 열기/닫기, Unity 포커스 알림, Runner.ResetWorld+Shell 갱신, 저장 목록을 통한 Load의 네 조합을 검사했다. 각 조합에서 최초 W는 카메라 이동/R은 소집 전환으로 양성 대조한다. 전환 후 기존 키 홀드를 두 번 처리해도 카메라 위치와 전체 저장 bytes가 유지된다. release도 상태 불변이며, 새 W는 카메라 이동/새 R은 소집 한 번만 전환하고 계속 누르고 있어도 추가 토글하지 않는다.
- 포커스 검사는 OnApplicationFocus 알림이며 OS AltTab 조작이 아니다. 메뉴/Load 진입은 UI callback 경로이고 키만 합성 장치 이벤트다. 실제 사람의 키 홀드 감각과 포커스 전환은 M2 확인 범위다.
- `TestResults/20261117T143703546Z-c359cc40`: PlayMode 102/102, 종료 0, 필수 목록 일치, issues 0. 제품 코드 변경 없음. 최신 Windows 실행 파일은 `20261117T143426821Z-build`를 유지한다.
- 명세의 “모든 V/M 미실행”, “아직 실행 파일 없음” 문장을 작성 당시 상태로 명확히 구분하고 현재 진행 기록/사람 확인 카드로 연결했다. 완료 조건 자체를 축소하지 않았다.

### 현재 남은 수락 증거 (2026-11-17 재대조)

- 아래의 오래된 “남은 증거 보완의 우선순위”는 당시 이력이다. 그 후 무효 경로/요청 차단/legacy 전환, 두 구역 드래그와 키 홀드, 상태별 진단 렌더를 보완했다. 해당 후속 결과로 모든 V-ID 하위 사례가 완료됐다고 일괄 판정하지 않는다.
- V-A01: 교체 후 저장 상태·투영 접점 검사와 기준 샘플 진단 이미지가 있으나 기준 다섯 종류의 이미지 자체 발/바닥 접점 오차 증거를 최종 대조해야 한다. `20261117T142552118Z-ux-capture/capture/sample-five-offscreen.png`를 다시 확인하니 선택된 정착민 1이 정보 패널 뒤에 가려지고 위쪽 나무가 HUD에 일부 가려져 있다. 이 프레임만으로 5종 동시 시각 비교를 통과 처리하지 않는다. 검사 장면의 배치 보완이 필요하다.
- V-U07: 건설 hover·불가 배치·Esc/우클릭·성공 후 1회 종료의 네 시설 입력 연결은 위 후속 검사로 보완했다. 색/문구를 실제 창에서 읽고 조작하는 확인은 M3로 남긴다.
- V-U09/A03/A04: 진단 렌더와 일반 창 PNG는 구분한다. 일반 창 PNG 단색 실패는 해결되지 않았고 정상 창·시각 수락의 통과 증거가 아니다.
- M1~M4: 사용자 응답 없음. 실제 IME, 두 필수 화면 조합에서 탐색/명령, 최종 아트 식별은 미확인이다. 최종 아트 채택 및 전체 완료는 보류한다.

### 설정 파일 교체 실패 재현과 제한 재시도 (2026-11-17)

- 이전 UI 배율 150→재읽기 100 실패를 추적하기 위해 SaveFileService의 JSON 읽기/기록에 경로·내용을 제외한 단계/예외형/HResult 진단을 추가했다. 판정과 사용자 메시지는 기존 ErrorCode를 유지한다.
- `20261117T143105501Z-c8b0603f`: EditMode 187/187. 격리된 설정 파일을 2,000회 교체하며 453/746/1014/1194/1821번째에서 `Replace:IOException:-2147023721`을 관측했다. 모두 직전 파일 bytes 보존, 정상 재읽기, 임시 파일 정리를 확인했다. 성공한 쓰기는 요청한 배율과 일치했다.
- 오류는 `0x80070497`, Win32 1175다. [Microsoft ReplaceFile 계약](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-replacefilea)은 교체 대상 제거 실패이며 기존/교체 파일 이름이 유지된다고 명시한다. 실패를 유발한 외부 프로세스나 OS 내부 원인은 확인하지 않았다. 과거 UI 검사 실패와 동일한 오류라고 소급 단정하지 않는다.
- 이름 슬롯/설정 공통 JSON 교체 경로에서 이 오류만 최대 3회 시도하고 사이에 10/20ms 대기한다. 성공 전 원본 삭제나 다른 쓰기 방식으로 우회하지 않는다. 계속 실패하거나 다른 코드면 기존 실패 안내/원본 보존/임시 정리 경로를 따른다. 최대 추가 동기 지연 30ms를 감수한다. 기존 legacy Save 경로는 변경하지 않았다.
- 결정적 주입 검사에서 1175 두 번 후 성공, 지속 1175 세 번 후 실패, 1176/공유 위반은 한 번 후 실패와 원본 bytes 보존을 검사한다. 이는 1176의 OS 부분 교체 상황을 재현한 것이 아니라 해당 오류에 재시도하지 않는 정책 검사다. 수정 후 EditMode `20261117T143243244Z-216899e0` 188/188, PlayMode `20261117T143325417Z-b0175bad` 101/101 통과(각 종료 0/필수 목록 일치/issues 0). 2,000회 저장·재읽기에서 최종 I/O 실패는 0회였다. 오류의 영구 소멸이나 외부 원인 제거를 뜻하지 않는다.

- 최신 Windows 빌드 `20261117T143426821Z-build` 성공/종료 0/입력 변경 0. `20261117T143503376Z-ux-restart`의 저장 PID 8180과 복원 PID 7604 모두 정상 종료 0. 같은 슬롯을 Tick 17에서 저장한 뒤 두 번째 프로세스에서 Tick 20 이동 완료로 복원·진행했고 continuedStateHash가 일치했다. 배치 문제 0. 일반 창 PNG 두 장은 단색 실패이며 wrapper 종료 1로 유지한다.

### 상태별 정지 렌더와 같은 칸 가림 수정 (2026-11-17)

- V-A04: SettlementPlayerCheck가 실제 시뮬레이션으로 이동 Tick 1, 목재 10 운반 Tick 2, 침대 수면 Tick 2, 채집 Tick 6을 만들고 각 상태의 정지 전후 진단 이미지를 저장한다. 0.25초 실제 대기 동안 Tick/전체 저장 해시/활성 SpriteRenderer의 위치·회전·sprite가 유지되고, 정지 해제 후 0.1초 입력에서는 Tick이 증가했다. Pause 버튼 pointerEnter 직접 전달 후 색 변화도 확인했다. 이는 Selectable 핸들러 반응이며 실제 마우스 hit-test나 사람 조작 증거는 아니다.
- 첫 진단 `20261117T142016147Z-ux-capture`에서 같은 칸 나무가 채집 정착민을 가리고 수면 그림이 세로 침대를 가로질러 겹치는 문제를 발견했다. 시설의 정수 Y와 정착민의 칸 중심 Y가 혼용되어 동일 칸에서 정착민이 뒤로 정렬됐다.
- WorldMapView의 시설/물자/식물 정렬도 칸 중심 Y로 통일했다. 침대 수면은 앞 방향, 0.72칸 크기, 위로 0.04칸 표시하며 바닥 수면의 회전은 유지한다. 시뮬레이션 위치와 선택/이동 규칙은 변경하지 않는다. 수면 전용 원화를 추가한 것은 아니며 자세의 자연스러움과 실제 크기의 식별은 M4에서 확인해야 한다.
- Windows 빌드 `20261117T142508340Z-build`: 성공, 종료 0, ChangedInputs 없음. 수정 후 `20261117T142552118Z-ux-capture`: Player 종료 0/내부 passed true, 네 pose 보고서의 pauseStable/hoverChanged/resumeAdvanced 모두 true, layout-failures 0. 네 상태의 paused-offscreen PNG를 직접 검토해 채집 정착민 노출, 침대 세로 정렬, 운반물과 이동 방향 표시를 확인했다.
- 일반 창 PNG 23개는 여전히 단색 실패로 wrapper 종료 1이다. 진단 이미지 확인을 정상 창 렌더나 M1~M4 통과로 대체하지 않는다. 최신 전체 PlayMode 회귀 결과는 아래와 같다.

- 추가 회귀 `SameCellPawnRemainsVisibleAboveBedResourceAndTree`는 실제 침대 수면 상태에서 같은 칸 침대/목재/나무보다 정착민이 앞에 있는지, 북쪽 칸으로 이동하면 나무 뒤에 있는지, 표시 갱신이 저장 상태를 바꾸지 않는지 확인한다.
- 첫 PlayMode `20261117T142643785Z-d0aa174f`: 100/101. 새 가림 검사는 통과했지만 기존 UI 배율 저장 검사가 저장 후 다시 읽었을 때 150 대신 100이라 실패했다. 최초 실행에서 저장/읽기 원인 구분 자료는 부족하다.
- 배율 버튼 적용 직후 값·SettingsWarning, 직접 파일 읽기의 Success/ErrorCode/배율을 확인하는 진단 단언을 추가했다. 재실행 `20261117T142820936Z-5c1198ae`: 101/101, 종료 0, 필수 목록 일치/issues 0. 재현되지 않았으므로 간헐 설정 I/O 문제가 해결됐다고 판정하지 않는다. 관련 실패를 보존하고 후속 재현 시 어느 단계에서 실패했는지 확인한다.

### 불러오기 처리 중 중복 요청 (2026-11-17)

- V-S07: `NestedRequestsDuringLoadCannotSaveReplaceRestoredWorldOrExit`. A=Tick 2, B=Tick 5를 만들고 A를 복원한 상태에서 B LoadSlot을 호출한다. LoadSlot은 복원 후 최근 슬롯 기록까지 SaveBusy를 유지한다. 기존 UserSettingsStore 파일 서비스의 BeforeAtomicStage를 통해 Write/ReadBack/Replace 각 시점에 재진입 요청을 전달했다.
- 각 시점에서 B의 Session/활성 슬롯/Tick 5와 SaveBusy true를 먼저 확인한다. 추가 새 저장/기존 A 덮어쓰기/A 로드는 false, 새 정착지/Reset/10초 진행은 상태 변경 0, 보이는 전환 확인의 버리기·저장 후 계속 버튼과 Esc도 전환하지 않는다. B 전체 저장 bytes와 Session 참조가 유지된다.
- 정확히 세 단계가 호출되고 원래 로드가 성공한 뒤 SaveBusy false/최근 B/슬롯 두 개/종료 요청 false다. A/B 원본 파일 bytes가 동일하다. 메뉴 취소·재개 후 입력 차단 false, Map.World=B, 저장 상태 불변을 확인했다.
- 동기 LoadSlot의 최근 기록 갱신 단계에서 관측한 재진입 검사다. 파일 읽기 중 별도의 사용자 프레임이나 스레드 안전한 Unity 명령 호출을 입증하지 않는다. 실제 LoadSlot 소스상 같은 SaveBusy 범위가 파일 읽기·후보 적용·최근 기록 전체를 감싼다.
- `TestResults/20261117T141441813Z-692c297b`: PlayMode 100/100, 종료 0, 필수 목록 일치, issues 0. 제품 변경은 없으며 사람 확인용 빌드는 `20261117T140738043Z-build` 그대로다.

### 무효 경로의 파일 복원·물자 보존 (2026-11-17)

- V-S05: `InvalidWorkAndLifeRoutesCancelOnlyRestoredCandidatesAndDropCargoOnceThroughBothFiles`. 운반/건설 자재 반입/조리 식재료 반입/파종 이동/채집 이동/침대 이동/음식 이동 7종을 실제 시뮬레이션으로 만든다. 이동 중 저장 데이터의 다음 칸을 비연속 칸으로 바꾸거나 해당 칸을 차단한다. 각각 기존 파일과 새 이름 슬롯에 기록하고 새 repository로 읽어 총 28개 복원 조합을 검사한다.
- 각 복원본은 작업·생활 행동 종료, RestoreInvalid 이유, cargo 0, 이동 경로/진행 0이며 자원/예약/침대/저장 칸/설계도/시설/작물/채집 대상의 점유가 해제된다. 바닥/보관/운반/공정의 물자 총합은 원본과 같다. 정리된 복원본을 다시 캡처·복원한 전체 bytes가 같아 중복 낙하와 추가 ID 발급을 검사했다. 두 파일 형식 복원본을 소집 상태로 고정해 50Tick 동안 매Tick 상태와 최종 물자 총량을 비교했다. 이 후속 50Tick은 물자 재생성 방지 검사이며 자동 작업 완료 검사는 기존 유효 경로 왕복 검사가 담당한다.
- 입력 DTO bytes, 원본 World 전체 상태, 두 저장 파일 bytes가 검사 전후 동일하다. fixture 이동 대기는 기존 8×8 맵/단일 정착민/최대 조리대 완공과 반입을 포함한 1,000Tick 상한이며 실제 필요한 상태에 도달하면 즉시 중단한다. 성장/조리의 정지된 공정 자체는 이동 경로를 갖지 않으므로 기존 2399/2400·39/40 경계 검사를 유지한다.
- `TestResults/20261117T141133289Z-8d09aeff`: EditMode 185/185, 종료 0, 필수 검사 일치.
- 추가 V-S05: `InvalidPlayerRoutesRebuildOrWaitWithoutMutatingSavedFiles`는 소집 이동 Tick 2/부분 진행 상태를 사용한다. 비연속 경로는 복원 시 재계산/진행 0으로 시작해 (6,1)에 도착하고, x=4 전체 차단은 원래 목적지를 유지하며 Blocked 상태로 대기한다. 두 저장 형식 각각 50Tick 확인, 원본 위치/진행/Tick/파일 보존과 목재 20 유지를 검사했다.
- 최신 `TestResults/20261117T141242310Z-c076b9a3`: EditMode 186/186, 종료 0, 필수 목록 일치, issues 0. 제품 소스 변경은 없으며 M1용 빌드는 그대로다.

### 격리 저장을 사용하는 사람 확인용 시작 경로 (2026-11-17)

- 일반 DemoBootstrap 시작 경로에 `--storage-root <directory>`를 연결했다. 지정하지 않으면 기존 기본 경로를 사용한다. 지정했지만 값이 없거나 빈 값/다음 옵션이면 오류 종료하며 개인 저장으로 조용히 대체하지 않는다. 자동 UX/legacy smoke 분기는 유지한다.
- `InteractiveStartupUsesExplicitSaveRootAndRejectsMissingPath`: 실제 `StartInteractive`를 사용해 잘못된 인수 3종에서 repository/UI 생성이 없음을 확인하고, 공백/한글이 포함된 격리 경로에서 새 정착지 → 저장 콜백으로 `Saves/<id>.save.json`과 `Settings/user-settings.json` 생성, 최근 슬롯 일치, 새 repository에서 Tick 3 복원을 확인했다. 개인 저장 경로는 읽거나 쓰지 않았다.
- `TestResults/20261117T140503481Z-549ceec8`: PlayMode 99/99, 종료 0, 필수 목록 일치. `TestResults/20261117T140738043Z-build`: Windows build 성공/종료 0/ChangedInputs=[] . 시작 경로 변경이며 맵/HUD 배치는 변경하지 않았으므로 직전 12조합 UI 배치 결과의 해당 코드 범위는 유지된다.
- `Tools/Start-ManualCheck.ps1 -PrintOnly`: 실행 파일, 1280×720 창 인수, 절대 `TestResults/ManualAcceptance` 저장 루트, 로그 경로, WillLaunch=false를 확인했다. 이 호출은 게임 프로세스를 띄우지 않았다. 실제 사용자 실행과 한글 IME/창 표시가 검증됐다는 뜻이 아니다.
- [사람 확인 카드](2026-11-17-manual-acceptance.md)에 정확한 실행 파일/명령/저장 경로와 M1~M4의 조작·기대 결과·회신 예시를 제공한다. 먼저 M1 확인을 요청하고 독립 자동 검증은 이어간다.

### 최신 Windows 빌드·재실행·12조합 재검사 (2026-11-17)

- `TestResults/20261117T135735261Z-build`: Windows build 성공, 프로세스 종료 0, ChangedInputs=[] . 이 시점 제품/UI/아트를 기준으로 아래 Player를 실행했다.
- `TestResults/20261117T135810199Z-ux-restart`: save PID 27972와 load PID 33156가 각각 정상 종료 0. 같은 슬롯 `be4f2b5b7357422fabcfc0a3d11d4743`의 Tick 17 저장 후 새 프로세스에서 Tick 20까지 이동을 완료했고 양쪽 후속 상태 해시가 `63D9ECF3EB8A319AF2D6952CB94F53D497875CD3EFDC100B4AEF0FC538C8FF98`로 일치했다. 배치 이슈 0. saved/restored 일반 PNG는 단색이어서 UX 도구 종료는 1이다.
- `TestResults/20261117-latest-ui-matrix.json`: 1280×720, 1280×800, 1920×1080, 2560×1440 × UI100/125/150의 12개 실행. `20261117T135846773Z-ux-capture`부터 `20261117T140106930Z-ux-capture`까지 연결한다. 각 Player 종료 0, 각 도구 종료 1, 배치 이슈 0, 일반 이미지 실패 15개씩 총 180개다.
- 필수 default/sample-five/stove/work/building/save/confirmation/error의 96개 JSON을 다시 읽어 실제 화면명·해상도·배율 일치, checkedButtons/checkedTexts>0, issues.Count=0을 확인했다. 버튼/글자 영역과 글리프 검사이며 실제 입력이나 모든 그림의 가독성 판정은 아니다.
- 직접 연 진단 PNG: `20261117T135907749Z-ux-capture/capture/`의 default/work/save/confirmation/error/art-far 각 `-offscreen.png`, `20261117T135950498Z-ux-capture/capture/`의 stove/building `-offscreen.png`. 기본 HUD와 두 줄 소집 표기, 작은 화면 저장/확인/오류 문구, 먼 줌의 물자3종과 작물3단계, 조리대 선택 및 건설 불가 안내를 확인했다. 작업표와 조리대의 아래 항목은 스크롤 영역이며 이 이미지로 접근 조작까지 판정하지 않는다.
- 정적 검토는 위 8장에 한정된다. 정상 창 표시, 96장 전체의 내용 검토, 실제 한글 IME와 M1~M4는 미완료다. 다음 사람 확인 실행은 일반 사용자 저장을 건드리지 않도록 별도 저장 루트를 전달하는 경로가 필요하다. 현재 `--ux-storage`는 자동 검사 모드 전용이라 일반 실행에 적용된다고 안내하지 않는다.

### 반복 복원 UI 차단 실패의 재현과 검사 수정 (2026-11-17)

- 기존 `TenLoadAndResetCyclesRebindViewsAndButtonsOnlyToCurrentWorld`를 100회로 확장한 `20261117T135242881Z-66fe29ed`는 PlayMode 98/98 통과했다. 재현을 위해 1,000회로 늘리고 이름을 `RepeatedLoadAndResetCyclesRebindViewsAndButtonsOnlyToCurrentWorld`로 변경했다. 기존 10회 요구의 모든 상태/선택/도구/표시/리스너 관측을 그대로 유지한다.
- `TestResults/20261117T135408706Z-b6d866ad`: 전체 97/98, cycle 375에서 실패. 기록된 상태는 `SaveMessage=저장한 정착지를 불러왔습니다.`, `SettingsWarning=게임은 '반복 복원'에 저장되어 있지만 이어하기 기록을 갱신하지 못했어...`, warning button=true, menu=true였다. 따라서 이 실행의 UI 차단은 복원 실패나 오래된 메뉴가 아니라 S3/S4의 설정 실패 경고 확인 대기였다.
- 검사를 수정해 설정 경고가 있을 때 이름/메뉴/차단/활성 슬롯/종료 없음과 전체 복원 bytes를 검사하고 `warning.ok`를 누른다. 이후 기존의 UI 차단 해제와 현재 월드만 토글/Reset 검사를 계속한다. 경고 없이 UI가 막히거나 경고 확인 후에도 막히면 여전히 실패한다. 경고를 무시하거나 무조건 통과시키지 않는다.
- `TestResults/20261117T135535772Z-a7e9af82`: PlayMode 98/98, 종료 0, 필수 목록 일치, issues 0. 반복 테스트 29.319899초, 출력 `Completed 1000 Load/Reset cycles; acknowledged settings warnings=1`. 즉 경고 분기를 실제로 한 번 거쳐 상태 보존/확인 후 복귀까지 검사했다. 제품 코드는 변경하지 않았다.
- 이 증거는 반복 복원 검사에서 관측한 UI 차단을 설명한다. 설정 파일 갱신이 간헐적으로 실패하는 OS/파일 작업 원인이나 앞선 최근 슬롯 삭제 검사의 별도 실패 원인까지 확정한 것은 아니다. 당시 정상 저장 fixture에는 이후 설정 기록 전제조건을 추가해 두었다.

### legacy 저장과 화면 전환 분기 (2026-11-17)

- V-S09: `LegacyLoadThenSaveAndExitCreatesNamedSlotWithoutChangingOriginal`은 이전 Player의 `legacy-v1.json` fixture를 격리 저장 폴더에 복사하고 실제 목록·확인 콜백으로 로드한다. 활성 슬롯 null/최근 슬롯 legacy, 저장 화면에 legacy 덮어쓰기 버튼 없음/새 이름 기본값을 확인한다. 새 이름 저장 후 종료 요청 true, 새 정상 slot ID/최근 기록 갱신, 전체 현재 저장 bytes 불변이다. legacy 파일과 fixture 원본, 기존 다른 슬롯의 bytes가 모두 동일하며 새 repository에서 3개 항목과 새 이름/동일 Tick 복원을 확인했다.
- V-S09: `TransitionCancelAndOverwriteCancelPreserveProgressBeforeExplicitDiscard`는 new/main/exit의 세 전환에 대해 최초 확인 취소와 저장→덮어쓰기 확인 취소→저장 취소→재개를 각각 검사한다. 모든 취소는 Session 동일 참조/전체 저장 bytes/정착민 선택/활성 슬롯/기존 파일을 유지한다. 이후 명시적 버리기는 새 정착지 Tick 0/활성 슬롯 null, 메인 메뉴의 HasSettlement false/입력 차단, 종료 요청 true로 각각 연결된다. 파일은 그대로이고 슬롯은 1개다. 버튼 콜백 검사이며 실제 프로세스 종료는 별도다.
- `TestResults/20261117T134919430Z-5e38f0d3`: 새 검사 둘은 통과, 전체 97/98/종료 2. `TenLoadAndResetCyclesRebindViewsAndButtonsOnlyToCurrentWorld`에서 cycle 4의 `IsUiBlocked=false` 기대가 실패했다. 이전의 반복 복원 간헐 실패와 관련 가능성이 있지만 확정하지 않았다. 설정 경고 모달 가능성을 구분하기 위해 해당 assertion 메시지에 SettingsWarning/SaveMessage/warning.ok 존재/menu 상태를 추가했다.
- `TestResults/20261117T135113566Z-488ef383`: PlayMode 98/98, 종료 0, 필수 목록 일치, issues 0. 재실행 통과는 간헐 실패 원인 해결의 증거가 아니며 미해결 관찰로 유지한다. 제품 소스 변경은 없다.

### IME 이벤트 라우팅과 최근 저장 검사의 관찰 사항 (2026-11-17)

- `FocusedNameFieldBlocksKeyboardShortcutsAndMapCameraMovement`를 확장했다. 기존 R/Space/1/3/W 차단과 해제 후 양성 대조를 유지하고, `IMECompositionEvent`의 "한"/빈 문자열을 Keyboard에 전달했다. 조합 중 Esc/Enter의 실제 키 눌림과 composition callback 4회 수신을 확인한다. 각각 메뉴가 유지되고 같은 InputField가 선택되며, 저장 슬롯 0개/종료 false다.
- 이 검사는 Shell의 단축키 전달 차단 범위다. Unity 기본 InputField는 로컬 uGUI 소스상 BaseInput.compositionString과 Event.PopEvent의 문자 이벤트를 사용하므로, Input System 합성 이벤트가 Windows의 실제 문자 입력을 대체한다고 주장하지 않는다. 확정 텍스트 불변은 해당 합성 경로의 관측이며 실제 조합 취소/확정 결과는 M1에서 확인해야 한다.
- `TestResults/20261117T134454635Z-f7cd086f`: 95/96 통과, 종료 2. 새 IME 검사는 통과했지만 `MissingOrCorruptRecentSlotShowsListWithoutLoadingAnotherSave`가 삭제 후 이어하기에서 Session 동일 참조 기대를 충족하지 못했다(당시 소스 297행). 원인은 확정되지 않았다.
- 정상 저장 fixture `SaveDirect`에 설정 경고 empty/최근 슬롯=활성 슬롯 전제조건을 추가했다. 최근 파일 삭제 검사에는 삭제 후 최근 슬롯 ID 유지와 File.Exists=false를 추가했다. 이는 원인 구분을 위한 관측 보강이며 제품 수정이나 재현 원인 해결이 아니다.
- `TestResults/20261117T134618154Z-fccc9e16`, `TestResults/20261117T134656308Z-29f01db3`: 각 PlayMode 96/96, 종료 0, 필수 검사 일치, issues 0. 두 번 통과했다고 앞선 간헐 실패의 원인이 해소됐다고 판정하지 않는다.

### 두 구역의 제스처 수명과 캡처 거부 후 재시도 (2026-11-17)

- 기존 포인터 검사를 `PointerDragCreatesExactZonesAndCancelsAcrossUiAndWorldTransitions`로 확장·이름 변경하고 필수 검사 목록도 대응시켰다. 기존 저장 구역의 모든 관측 조건을 유지하면서 경작 구역도 같은 정/역 네 칸·한 칸·UI/화면 밖 취소 경로를 검사한다.
- V-U11: 저장/경작 × 메뉴/포커스 알림/Reset/Load의 8조합. 실제 마우스 누름 이벤트로 캡처를 시작하고, 해당 전환 직후 캡처 false를 확인한다. 여전히 버튼이 눌린 상태에서 도구를 다시 활성화해도 오래된 해제는 전체 저장 bytes 불변/구역 0개다. 이후 새 누름·해제는 (3,3) 한 칸/구역 1개를 만든다. Reset은 실제 Runner.ResetWorld와 Shell.LateUpdate, Load는 실제 저장 목록·확인 콜백을 사용한다. 포커스는 Unity OnApplicationFocus 알림 경로를 호출한 검사이며 OS의 실제 창 전환 증거가 아니다. 키 홀드의 모든 Shell 조합까지 포함하지 않는다.
- `TestResults/20261117T134115231Z-647521dd`: 확장한 구역 검사 포함 PlayMode 96/96, 종료 0, 필수 목록 일치.
- V-S06: `PendingCommandSaveFailurePreservesSlotAndDoesNotExit`에 복구 흐름을 추가했다. 캡처 거부 뒤 오류 확인 → 저장 화면 취소 → 메뉴 재개는 사용자 Pause true를 유지하고, 1초를 입력해도 Tick/대기 명령이 동일하다. 이후 Space 명령으로 사용자가 재개하고 0.1초를 진행하면 Tick +1/대기 명령 0이 된다. 다시 저장 후 종료를 요청하면 같은 슬롯이 Tick +1로 저장되고 종료 요청 true, 슬롯 1개다. 저장을 위해 자동으로 Tick을 진행시키지 않는다. Space는 shortcut dispatch 검사이며 물리 키 입력은 아니다.
- 최신 `TestResults/20261117T134219139Z-447ac227`: PlayMode 96/96, 종료 0, 필수 목록 일치, issues 0. 이번 변경은 테스트에 한정되며 전체 완료나 M1~M4 통과를 뜻하지 않는다.

### 저장 구역의 포인터 입력 경로 (2026-11-17)

- V-U08 보완: `PointerDragCreatesExactStorageRectangleAndRejectsUiOrOutsideRelease`는 합성 MouseState를 Input System에 전달하고 실제 InputSystemUIInputModule과 WorldMapInput.Update를 처리한다. Shell의 사각형 handler와 Runtime을 거쳐 저장 구역이 생성되므로 handler 직접 호출 검사가 아니다.
- (3,3)→(4,4), (4,4)→(3,3)의 결과는 정확히 (3,3)/(4,3)/(3,4)/(4,4) 네 칸이다. 누른 상태에서 이동하는 동안 전체 저장 bytes는 동일하고, 해제 후 구역 1개/도구 None이며 추가 해제로 중복 생성되지 않는다. (3,3) 누름/해제는 정확히 한 칸이다.
- 메뉴 버튼 중심과 화면 밖(-100,-100)으로 각각 드래그 후 해제하면 캡처 해제, 전체 저장 bytes 불변, 구역 0개다. 메뉴 버튼을 향한 드래그는 메뉴 클릭으로 처리되지 않는다. 실제 GraphicRaycaster가 시작/끝 맵 좌표를 UI 아님, 메뉴 중심을 UI로 판정하는 것도 검사했다.
- 정지 상태의 Tick 0, 대기 명령 0, 목재 20 유지. `TestResults/20261117T133900614Z-aaf163e0`에서 PlayMode 96/96, 종료 0, 필수 목록 일치, issues 0. 제품 소스 변경은 없다. 물리 마우스나 실제 Windows 화면 검증을 대신하지 않으며 경작 구역/제스처 수명 조합은 별도다.

### 남은 증거 보완의 우선순위

완료조건 전체의 V-ID 목록을 다시 대조했다. 아래는 이번에 확인한 누락 범주이며 전체 하위 사례 감사를 완료했다는 뜻은 아니다.

1. V-U02/U08/U11: IME 조합, 경작 구역의 이벤트 경로, 드래그/키 홀드 중 메뉴·Load/Reset·포커스 상실의 Shell 통합 조합.
2. V-S05/S06/S07/S09: 무효 경로 복원 하위 사례 대응, 캡처 거부 후 실제 재개/재시도, 불러오기 중 요청 차단, legacy 세션 저장 후 전환과 각 전환의 취소/버리기 범위.
3. V-A01/A03/A04: 기준 5종의 실제 이미지 접점, 운반·수면·작업/Pause 상태별 렌더와 가림·식별성. 현재 아트 교체 검사는 정착민/나무/침대의 렌더러 접점을 검사하며 이미지 자체의 발/밑면 판정은 별도다.
4. V-U09/P01: 최신 제품 상태의 12조합 배치·렌더와 실제 두 프로세스 저장/복원 재확인. 기존 진단 렌더와 정상 창의 검은 PNG 실패를 구분한다.
5. M1~M4: 실제 Windows IME, 탐색/스크롤, 작업/구역, 아트 식별의 사람 확인. 자동 입력과 진단 RenderTexture를 이 증거로 취급하지 않는다.

### 저장 처리 중 요청 차단과 단계별 실패 보존 (2026-11-17)

- `TestResults/20261117T133614383Z-852f84b7`: PlayMode 95/95, 프로세스 종료 0, 필수 검사 목록 일치, issues 0. 제품 소스 변경은 없다. 테스트에서는 repository의 기존 `SaveFileService.BeforeAtomicStage` 오류 주입 지점을 사용한다. Runtime API를 넓히지 않기 위해 테스트 한 곳에서 private 필드를 조회한다.
- V-S06: `AtomicStageFailuresPreserveLiveStateAndDoNotExitOrLeakSlots`. Tick 2 저장본과 현재 Tick 5/3배속/정착민 1 선택을 준비하고, 실행/정지 × Write/ReadBack/Replace의 6조합에서 IOException을 유도한다. 저장 후 종료의 UI 덮어쓰기와 Runtime 신규 저장을 각각 실패시킨다. 조합마다 주입 횟수 2, 기존 파일 bytes 불변, 전체 현재 저장 데이터 불변, Session/World 동일 참조, 활성 슬롯/선택 유지, SaveBusy 해제, 슬롯 1개, tmp 0개, 종료 요청 false를 확인했다.
- V-S07 보완: `NestedRequestsAtEverySaveStageCannotReplaceWorldSaveAgainOrExit`. 첫 신규 저장의 Write/ReadBack/Replace 각 지점에서 Runtime 저장·불러오기·Reset·새 정착지·10초 진행, UI의 저장하지 않고 종료·저장 후 계속·Esc를 시도한다. SaveBusy true에서 저장/로드는 false, 종료와 메뉴 전환은 발생하지 않고 전체 저장 상태/월드/선택이 유지된다. 단계 호출은 정확히 세 번이며 첫 저장 완료 후 기존 슬롯+새 슬롯 두 개만 존재하고 기존 파일은 그대로다. 새 슬롯은 Tick 5로 복원 가능하다. SaveBusy 해제 후 동일 종료 버튼은 요청을 발생시켜 차단의 양성 대조도 확인했다.
- 동기 파일 처리의 재진입 검사이며 중간 렌더 프레임이나 실제 동시 사용자 클릭을 재현했다는 주장이 아니다. 종료는 요청 기록이고 프로세스는 테스트용 억제 상태다. 불러오기 처리 도중의 요청 차단과 기타 V-S07 하위 사례까지 전체 통과 처리하지 않는다.

### 실제 파일 잠금과 저장 후 종료 (2026-11-17)

- V-S06/V-S09 보완: `LockedSlotPreservesLiveStateAndSelectionUntilSaveThenExitRetrySucceeds`는 Tick 2의 기존 슬롯을 만든 뒤 현재 진행을 Tick 5, 정지/3배속, 정착민 1 선택 상태로 설정한다. 저장 후 종료 → 덮어쓰기 확인에서 실제 파일을 `FileShare.Read`로 열어 Windows의 최종 교체를 차단한다.
- 실패 후 기존 파일 바이트와 전체 캡처 데이터가 동일하며 Session/World 참조, 활성 슬롯, 선택, 카메라 위치/줌이 유지된다. 메뉴는 열려 있고 종료 요청은 false, SaveBusy는 false, 임시 파일은 0개, 슬롯은 1개다.
- 잠금 해제 → 오류 확인 → 같은 슬롯 덮어쓰기 재시도에서는 종료 요청 true, 슬롯 1개, 새 repository가 읽은 Tick 5를 확인했다. 현재 게임 상태도 동일하다. `SuppressProcessExit`를 사용하므로 이는 종료 요청 증거이며 실제 프로세스 종료 증거가 아니다. 버튼 콜백 호출이므로 물리 입력 검증도 아니다.
- `TestResults/20261117T133410229Z-ad281cb4`: PlayMode 93/93, 실행 종료 0, 필수 목록 일치, issues 0. 이번에는 테스트와 필수 목록만 변경했으며 제품 코드는 변경하지 않았다. 단계별 실패 주입의 Runtime/UI 연결과 중복 요청 검사를 모두 완료했다는 뜻은 아니다.

### 후속 실행 (2026-11-17)

- 실제 Player의 URP `SingleCameraRequest`로 맵 RenderTexture를 얻고 동일 HUD를 camera canvas로 렌더하는 진단 경로를 추가했다. `*-offscreen.png`는 일반 창 backbuffer PNG와 별도다. 후자는 계속 단색 실패이며 도구 전체 종료1을 유지한다. 카메라/캔버스 상태와 임시 텍스처는 캡처 후 복원/해제한다.
- `20261117T122332034Z-ux-capture`의 진단 이미지에서 지면이 실제 맵보다 크게 겹친 문제를 발견했다. 원본512/128PPU의4칸 Sprite를 Tile.transform으로1칸에 정규화했고 카탈로그 교체 때 차단 지면도 갱신한다. `20261117T122544453Z-0a662404` PlayMode83/83에서64/128/256PPU 교체의1칸 크기와 게임Tick 불변을 검사했다.
- 뒤/옆 정착민 원화2종을 생성해 import/연결했다. 파일·출처·규격은 `Docs/Art/Implementation-2026-11-17.md` 후속 기록을 따른다. `20261117T122944437Z-ux-capture`에서 방향 연결과 수정 지면을 정적 확인했다.
- 이름을 화면16픽셀 기준으로 표시하고 겹친 이름을 화면 좌표에서 위아래로 배치했다. `20261117T123156667Z-4a7ad0d1` PlayMode84/84 통과. 같은 칸3명·3줌에서 크기 유지·이름 기준점 간격20픽셀 이상·Tick 불변을 검사했다. `20261117T123329775Z-ux-capture/pawn-directions-offscreen.png`를 열어 이름 겹침 해소를 확인했다. 일반 창 화면은 미검증이다.
- 작업표 제목/숫자 행의 가변 너비 차이를 진단 이미지에서 발견해 동일 열 너비로 변경했다. `20261117T123426892Z-29ede90a` PlayMode84/84 통과, `20261117T123508463Z-build` 입력 변경0/성공. `20261117T123550278Z-ux-capture`는 열 중심 정렬을 포함한 배치 issues0이며 진단 작업표 PNG를 확인했다. 이후 제목 Text도 열 가운데로 정렬했고 이 마지막 텍스트 정렬 변경의 재빌드/렌더는 아직 남았다.
- 1280×720/150% 진단 렌더에서 저장40자·작업표를 확인했다. 작업표 하단행과 정착민 추가 버튼은 스크롤 영역에 있어 실제 휠 접근은M2/M3에서 확인해야 한다. 전체12조합의 최신 렌더·최종 전체 아트·사람 확인은 미완료다.

- `20261117T121601001Z-build`, `20261117T121737052Z-build`는 입력 변경0/빌드 성공. 최신 정착민 겹침 선택과 하단 안내 여백 수정이 Player에 포함됐다.
- `20261117T121642592Z-ux-capture`의1280×720/150%에서 하단 안내 Text의 preferredHeight가 실제 높이를 초과했다.32높이 패널에서 위아래10여백으로 글자 영역이12가 된 원인이며, 수직 여백을4로 줄여24높이를 확보했다. `20261117T121818667Z-ux-capture` 재검사에서 배치 issues0을 확인했다. 이미지 출력은 계속 단색 실패다.
- `TestResults/20261117-layout-matrix.json`은1280×720,1280×800,1920×1080,2560×1440 ×100/125/150%의12개 실제 Player 실행을 연결한다. 실행은 `20261117T121837717Z-ux-capture`부터 `20261117T122016978Z-ux-capture`까지다. 각 실행의8개 필수 화면(default/sample-five/stove/work/building/save/confirmation/error) 총96개 `*-layout.json`을 다시 읽어 해상도/배율/화면명 일치·검사 버튼/텍스트 수>0·issues0을 확인했다. main 화면12개도 추가 기록했다.
- 배치 검사의 실제 범위는 버튼이 캔버스 또는 스크롤 content 안에 있는지, 스크롤 viewport보다 넓지 않은지, Text의 preferredHeight가 표시 높이를 넘지 않는지, 사용 문자의 font glyph가 존재하는지다. 실제 그림·겹침·색 대비·마우스 접근성·스크롤 감각은 이 검사로 판정하지 않는다. 따라서 V-U09 전체 완료는 아직 아니다.
- 검사 화면에40자 한글 저장명·덮어쓰기 확인·41자 거부 오류를 포함했다. 건설 미리보기는 임시 Unity Input System Mouse에 상태 이벤트를 넣어 실제 WorldMapInput의 hover가(5,5)에 도달했음을 검사한다. 물리 입력 증거는 아니다. 조리대 화면은 격리 시나리오에서 실제 자재 운반·시공 후 선택한다.
- 12개 실행 모두 배치 문제0이지만 PNG 단색으로 도구 종료1이다. 정상 렌더로 기록하지 않았다. 화면 표시 허용은 계속 응답 대기이며 새 UI의 실제 렌더와 M1~M4는 미완료다.

- `20261117T121042439Z-d952564b` PlayMode81/81 통과. 정착민 아래 겹친 물자를 명령·Tick 변경 없이 선택하는 경로와10회 Load/Reset의 새 월드 연결·선택/도구 초기화·표시 수·단일 소집 토글을 검사했다. 정착민 정보에 같은 칸 시설/물자 선택 버튼을 추가했고 대상이 없으면 비활성, 이동으로 겹침이 생기면 갱신한다.
- `20261117T121146012Z-55de2dbb`, `20261117T121255601Z-f8b0361a`는 신규 조리/작업표 검사1개 실패. 테스트가 Cook 기본 우선순위2를1로 가정해2회 클릭 후0에서 담당자 유지를 요구했다. 초기 우선순위를1로 명시해1→2→3 유지와3→0 해제를 각각 검사하도록 수정했다. 제품 규칙 변경은 없다.
- `20261117T121335662Z-5d82b353` PlayMode82/82 통과. 조리 진행10/40·투입2에서 숫자 변경은 담당자/행동ID/진행 유지, 끔은 즉시 담당자 해제와 투입·진행 보존, UI 다음 갱신에서 끔 표시, 켬 후 다음Tick 재배정, 조리 중지는 후속5Tick 진행 불변을 확인했다.
- 마지막 Windows 빌드 `20261117T120734645Z-build`는 입력 변경0으로 성공했다. 이후 겹친 대상 선택 버튼 변경의 Player 재빌드는 아직 남아 있다.

- `20261117T120436114Z-c33f228d` PlayMode77/77, `20261117T120550153Z-ae1779cf` PlayMode79/79 통과. 최근 기록 없음의 최신 후보/확인/취소, 최근 파일 삭제/손상 시 임의 대체 로드 방지, 정상 이어하기, 설정 실패 후 B 슬롯 보존과 경고 확인 전 종료 보류, UI 배율 재설정/게임 로드/설정 손상 경로를 추가했다. 두 실행 모두 필수 검사 목록과 일치한다.
- `20261117T120702756Z-1c9c7ad8` EditMode184/184 통과. `SaveRoundTripTests.Restore`에서 기존 파일 왕복에 이어 새 슬롯 파일 생성→새 repository 인스턴스 읽기→전체 상태/시간 잔여/Reset 횟수 비교→기존 원본 bytes 보존을 검사한다. 두 형식 복원본을50Tick 동안 매Tick 비교하고, 다시 복원한 새 슬롯을 기존 이동/수면/운반/시공/성장/조리의 완료 경계 검사에 사용했다. 무효 경로의 모든 하위 사례까지 충족했다는 판정은 아직 하지 않는다.

### 이번 보완의 요구사항 대응

| 요구/검사 | 실제 테스트 | 확인한 결과 |
|---|---|---|
| S4 / V-S08 | MissingRecentRecordOffersLatestSlotAndRequiresConfirmation | 빈 목록 이어하기 비활성, 최근 기록 없음은 최신 후보를 명시하고 확인 전 세션 유지, 취소 보존, 확인 후5Tick 복원 |
| S4 / V-S08 | MissingOrCorruptRecentSlotShowsListWithoutLoadingAnotherSave | 최근 파일 삭제/손상 시 기존 세션 유지와 목록 안내, 다른 저장 자동 진입0, 정상 파일 복구 후 이어하기 |
| S3/S4 / V-S07/V-S09 부분 | SettingsFailureAfterSaveKeepsNewSlotAndWaitsForAcknowledgementBeforeExit | 설정 파일 잠금에서 B 저장 성공, A bytes·최근 기록 유지, B 이름 경고, 확인 전 종료 요청0/확인 후1 |
| U5 / V-U10 | UiScalePersistsAcrossStorageReloadAndGameLoadAndReportsSettingsFailure |150% 설정 재읽기·게임 로드 보존, 잠금 시 화면125%/파일150%와 경고 표시, 손상 시100% 기본값·안내 |
| S2 / V-S05 유효 진행 | SaveRoundTripTests의 이동/식사·수면/운반/건설·철거/성장·수확/조리6개 테스트 | 두 파일 형식의 실제 왕복·50Tick 동등 진행, 새 슬롯 복원 후 각 완료 경계 결과 |
| U2 / V-U04 부분 | PawnInspectorCanSelectAnOverlappingResourceWithoutChangingOrders | 겹침 없음 비활성, 이동 후 물자 선택 가능, Tick/소집/행동ID 보존, 맵 클릭으로 정착민 재선택 |
| V-I03 수명 부분 | TenLoadAndResetCyclesRebindViewsAndButtonsOnlyToCurrentWorld |10회 각각 Load와Reset, 새 월드 참조·선택/도구 초기화·표시 수·초상1개·현재 월드만 소집 토글. 도형/임시/최종 아트 교체 부분은 대기 |
| U3 / V-U05 부분 | WorkTableChangesPriorityWithoutRestartingCookingAndOffReleasesWorker | 조리 중 숫자 우선순위 변경·끔/켬·조리 중지의 소유/투입/진행/표시 검사. 주문 삭제/재생성의 새 UI 경로는 별도 대기 |

이 표의 부분 증거로 중복 요청·모든 전환·전체 무효 경로까지 완료 처리하지 않는다. 창 표시 허용과 M1~M4 사람 확인은 별도로 대기한다.

### 이전 후속 실행

- `20261117T115653758Z-build`는 Unity 종료0/빌드 성공이지만 성능 테스트 자동 생성 파일4개가 바뀌어 입력 동결 판정 실패. `20261117T115735877Z-build` 재실행은 변경0/성공이다.
- `20261117T115903212Z-build`, `20261117T120126969Z-build`는 검사 코드 변경 뒤 입력 변경0/정상 종료0/빌드 성공이다.
- `20261117T115811307Z-ux-capture`, `20261117T115836487Z-ux-capture`, `20261117T115941031Z-ux-capture`는 단색 이미지 실패. 배치 모드 해제와 splash 종료 대기로 해결되지 않았다. 실제 PNG를 열어 검은 화면을 확인했다.
- `20261117T120020363Z-ux-restart`는 프로세스 A 저장/정상 종료0, 새 프로세스 B 이어하기/정상 종료0. 슬롯·17Tick·전체 상태 해시 일치. 렌더는 실패다.
- `20261117T120208434Z-ux-restart`는 V-P01의 진행 복원도 검사했다. 초기 (1,1)에서 (5,1)까지 네 칸 이동을 지정하고17Tick에 저장했다. 프로세스 A의 중단 없는20Tick 결과와 새 프로세스 B의17Tick 복원+3Tick 결과가 전체 상태 해시 `63D9ECF3EB8A319AF2D6952CB94F53D497875CD3EFDC100B4AEF0FC538C8FF98`로 일치하고 두 프로세스 모두 정상 종료0, `movementCompleted=true`다. 저장/복원은 격리된 검사 루트에서 수행했다. `summary.json`에 두 프로세스 결과, `render-failures.json`에 화면 실패를 남겼다. 검사 전체 종료는 렌더 실패로1이며 전체 통과로 취급하지 않는다.
- `Run-UXCheck.ps1`은 화면 실패가 있어도 저장/재실행 두 프로세스의 상태 검증을 끝내고 실패 목록을 기록한 뒤 실패를 반환한다. 화면 실패를 통과로 바꾸지 않는다.
- 보이는 게임 창에서의 캡처 허용을 사용자에게 확인 중이다. 숨김 실행의 단색 원인은 아직 확정하지 않았다. 남은 기능/하위 사례·12조합 화면·최종 아트·M1~M4는 미완료다.

| 실행 | 관측 | 판정과 한계 |
|---|---|---|
| Unity 최초 import 및 BuildArtAssets.Build | 컴파일 후 visual catalog 41항목 생성, 정상 종료0 | 파일 연결 생성 완료. 실제 화면/최종 아트 채택 아님 |
| TestResults/20261117T105303571Z-1d2083aa | EditMode182/182 통과, 입력 manifest 일치 | 새 저장 파일 서비스12개·아트5개와 기존 규칙 검사. 메뉴/렌더/사람 확인 대체 불가 |
| TestResults/20261117T105340175Z-104e64c3 | PlayMode67개 중 WorldMapViewTests 입력4개 실패 | 입력 이벤트 처리 순서를 명시해 보완, 통과로 취급하지 않음 |
| TestResults/20261117T105820563Z-35836636 | EditMode184개 중 신규 미리보기2개 실패 | 거부 명령이 정상적으로 LastCommandError를 바꾼 것을 전체 상태 비교가 오류로 판정. 오류 문구는 별도 비교하고 저장 게임 상태 비교에서 제외하도록 검사 보완 |
| TestResults/20261117T105910374Z-1d4a81cc | PlayMode75개 중 맵 마우스3개 실패, 새 Shell8개와 키홀드 통과 | 포커스 없는 배치 실행의 이벤트 전달 경로 추가 보완. Shell 키보드에도 실제 전달/차단 해제 양성 대조 추가 |
| TestResults/20261117T110209362Z-72a86f12 | EditMode184/184 통과, 입력 manifest 일치 | 모든 칸/시설과 대표 사각형에서 미리보기 상태 보존·실제 배치 판정 일치 확인. 시각 미리보기 가독성은 별도 |
| TestResults/20261117T110241315Z-ff7b8b49 | PlayMode75개 중 키보드 전달 선행조건1개 실패 | 맵7개 통과. 키보드를 테스트 포커스 설정 전에 생성한 순서 수정 |
| TestResults/20261117T110359387Z-7ae2f9a1 | PlayMode75/75 통과, 입력 manifest 일치 | Shell8개·맵7개·기존 통합60개. 실제 입력 전달과 차단 해제 양성 대조 포함. 사람 입력 증거 아님 |
| TestResults/20261117T112119381Z-prepare | 장면 준비 정상 종료0 | 새 Shell·카탈로그 연결. 변경 입력은 지정된 장면1개 |
| TestResults/20261117T112201402Z-build | Windows 빌드 성공, 입력 변경0 | 실제 Player 생성. 렌더 성공을 의미하지 않음 |
| TestResults/20261117T112436249Z-ux-capture | 숨김 D3D12 Player screenshot 파일 생성 실패 | 실패로 기록 |
| TestResults/20261117T113337968Z-ux-capture 및 20261117T113433447Z-ux-capture | D3D11/배치 캡처 파일 생성, 내용은 검은 화면 | 스크립트 결과는 true였으나 내용 검토에서 실패. 완료 근거에서 제외하고 Run-UXCheck에 빈 이미지 검출 추가 |
| TestResults/20261117T113549407Z-ux-capture | 배치 Player splash 종료 대기에서 timeout | 해당 검사 PID2332와 exe/검사 인수 확인 후 종료. 정상 종료·화면 증거 아님. 명시적 검사 모드에서만 splash 즉시 건너뛰기로 보완 |

## 현재 자동 검사 대응

| 요구/검사 | 실제 검사 | 확인 범위 / 남은 범위 |
|---|---|---|
| S1~S3 / V-S01~07 | SaveRepositoryTests 12개 | 이름·슬롯 독립·실제 legacy fixture·오류 파일·원자 교체 실패·설정 실패. 전체 UI 경로와 모든 진행 단계 추가 대응 필요 |
| S4 / V-S09 | Shell LoadConfirmationCancel / CorruptionAfterOpeningLoadConfirmation / ConfirmedLoad / NewSettlementFirstSave / PendingCommandSaveFailure | 취소·손상·성공·새 정착지·저장 거부 전환. 실제 재실행은 V-P01 별도 |
| U1 / V-U01~02 | MenusBlockPositiveTimeAndRestoreUserPauseAndSpeed / FocusedNameFieldBlocksKeyboardShortcutsAndMapCameraMovement | 양의 시간 차단과 재개 대조, 실제 키 이벤트/차단 해제 대조. IME 조합과 사람 확인은 미완료 |
| U2/U4 / V-U03~04/08/11 | WorldMapViewTests 7개 | 화면↔칸·카메라·실제 포인터 경로·UI 차단·모달 취소·키홀드. 모든 Shell 도구·포커스/로드 조합을 대신하지 않음 |
| U4 / V-U07~08 | PlacementPreviewTests 2개 | 시설4종×경계 밖 포함64칸, 정/역/한칸/점유/경계 사각형에서 읽기 전용·commit 일치. 실제 색/문구는 렌더 대기 |
| A1/A2 / V-A01~03 | VisualCatalogTests 5개 | 키·참조·다른 pivot/크기 접점·성장 경계. 실제 화면 투영1px/반복·가림/사람 식별성은 별도 |

이 대응은 부분 증거이며 위 V 전체 통과 판정이 아니다. 미실행 하위 사례를 후속 실행으로 채운다.

## 구현 중인 기능

- 이름 있는 다중 슬롯, 최근 슬롯/배율 설정, 실패 시 기존 파일 보존.
- 시작/게임 메뉴, 저장·덮어쓰기·불러오기 확인, 사용자 Pause와 메뉴 차단 분리.
- 월드 카메라와 칸 선택, 실제 배치와 같은 읽기 전용 규칙의 미리보기.
- 정착민×5개 작업 표, 선택 대상 정보, 원인+대상별 문제 안내와 위치 이동.
- 포함된 한글 폰트, 기준 원화4종과 임시 그림 카탈로그. 전체 최종 아트는 미완료.

SettlementPlayerCheck는 새 Shell의 실제 Windows Player 캡처와 프로세스 A 저장/B 복원용 검사 경로다. 기존 --smoke-output 경로는 옛 View이므로 새 UI 증거로 사용하지 않는다. 새 Player 빌드와 재실행 상태 검증은 위 후속 실행에서 통과했고 캡처는 실패했다.

검사 이름·하위 사례 대응은 후속 실행 결과와 함께 확장한다. V/M 전체 통과는 아직 주장하지 않으며 실제 IME/마우스·키보드 사람 확인은 별도 대기다.
# 원화5종 추가 반입 검사 (2026-11-17)

- 조리대/채집 식물/목재/식재료/식사 PNG5종: 1254×1254 RGBA, alpha0~255. 원본 보존·무편집 복사.
- BuildArtAssets: exit0, Catalog41항목. 로그 `TestResults/20261117-art-five-import.log`.
- EditMode184/184, 누락0, timeout없음: `TestResults/20261117T124534723Z-3e81f734`.
- 새 원화의 실제 Windows 렌더·3줌 식별은 미검증. 원화 IsFinalArt=false 유지. 이 결과로 V-A03 전체나 A2 완료를 판정하지 않는다.

# 작물3단계 원화 연결 검사 (2026-11-17)

- 원본3개는1254×1254 RGBA/alpha0~255. alpha128이상 bbox: young(470,737,785,1079), growing(296,337,949,1051), mature(227,142,992,1085).
- 바닥 anchor Y는 각각0.1396/0.1619/0.1348로 설정. 원본 편집은 하지 않았다. 이 수치는 원본 파일 측정이며 실제 화면 접점≤1px 충족 증거는 아니다.
- Unity 반입 exit0: `TestResults/20261117-art-crops-import.log`.
- EditMode184/184: `TestResults/20261117T124911687Z-4331edd0`. 기존 CropDisplayBoundariesDoNotInventSimulationStages에서 Growing0/1199/1200/2399, Mature2400 키 경계와 BuiltCatalogConnectsEveryCurrentVisualWithoutMissingSprites에서 필수 연결을 검사했다.
- 실제 Player 렌더, 최소/기본/최대 줌, 사람 식별은 미검증. A2/V-A03 완료 아님.

# 신규 아트 3줌 진단 렌더 (2026-11-17)

- Windows build `TestResults/20261117T125049763Z-build`: exit0, 입력 변경0.
- Player `TestResults/20261117T125132567Z-ux-capture`: 1920×1080/UI100. art-default/near/far-offscreen.png 정적 검토. 물자3종은 통나무 묶음/상자/그릇, 작물은 작은잎/큰잎/이삭으로 구분됨. 먼 줌 어린잎은 작아 작은 해상도 추가 확인 필요. 가까운 줌의 나무 위쪽은 viewport 밖으로 나감.
- fixture는 Growing0/1200,Mature2400을 저장 DTO에 작성한 뒤 TryApplySave 검증 경로로 복원했다. 실제 파종/성장 진행 증거가 아닌 표시 비교용이다. 캡처/줌 전후 전체 게임 상태 hash 일치 검사 통과.
- 배치 수치 이슈0. 일반 ScreenCapture PNG13개는 단색이라 runner exit1. offscreen은 같은 맵 카메라와 HUD를 RenderTexture에 합성한 진단 출력이며 정상 OS 창 표시나 사람 입력 증거가 아니다.
- 벽16조합, 작물1199/2399 경계 실제 표시, 상태별 연속 렌더, 작은 화면, 사람 확인은 이 실행으로 충족되지 않는다.

# 작은 화면·벽16연결 검증 (2026-11-17)

- `20261117T125237474Z-ux-capture`:1280×720/UI150 art-far-offscreen 정적 검토에서 어린 작물이 매우 작고 이름/수량이 주변 그림에 가까움. 가독성 완료 불가. 정상 ScreenCapture는 단색 실패.
- `20261117T125322343Z-build`:Windows build exit0, 입력 변경0.
- `20261117T125402108Z-ux-capture`:1920×1080/UI100, walls-sixteen-offscreen 검토. 위에서 아래/왼쪽에서 오른쪽으로 mask0~15. 중심 벽의 WorldMapView.WallMask를 기대값과 각각 비교하는 Player 검사 통과. 실제 복원 검증을 거친 authored fixture이며 건설 진행 검증은 아님.
- 직선/모서리/T자/십자 외형과 타일 연결을 진단 렌더에서 확인. 모서리 내부의 어두운 선은 추가 아트 정리 대상. 선택 가림/사람 확인까지 증명하지 않음. 일반창 PNG는 실패이므로 runner exit1 유지.

# 전체 화면 지도와 필요 패널만 표시 (2026-11-17)

- 변경:Map viewport(0,0,1,1), 선택 없음이면 Inspector 숨김, 열린 탭 없음이면 Command panel 숨김. 빈 피드백 숨김, 패널을 모두 닫았을 때 피드백을 하단으로 이동.
- 신규 OverlayPanelsPreserveCameraAndOnlyVisiblePanelsBlockPointer:패널 전환 전후 카메라 위치/줌/칸 좌표 유지, 활성 패널 GraphicRaycaster 적중/숨긴 패널 적중 없음,Tick0 확인. 사람 입력 증거 아님.
- PlayMode85/85:`TestResults/20261117T125723777Z-4d48846d`.
- 피드백 마지막 수정 전 Windows build:`20261117T125615450Z-build` exit0/입력변경0.1280×720/UI150 capture:`20261117T125653873Z-ux-capture` 배치이슈0,Player내부exit0,일반캡처 단색으로 runner exit1.
- art-far-offscreen 정적 비교:동일 화면 조건에서 지면 폭 약246→600px,어린잎/성장잎/이삭과 물자3종 구분 개선. 피드백이 중간에 남아 가리는 문제를 발견해 마지막 소스 수정함. 마지막 안내줄 수정의 새 빌드/렌더는 남음.
- 전체 HUD 재배치/건축가 카테고리/작은 선택 카드 등 UI 인계안 전체 완료가 아니다. 실제 창/사람 확인 및 새 배치12조합 재검증 필요.

# 전체 화면 배치 12조합 재검증 (2026-11-17)

추가 반복 `20261117T130417028Z-2a287a25` PlayMode85/85 통과. 동일 실패는 두 후속 실행에서 재현되지 않았으나 원인을 확정하거나 제품 결함 수정으로 판정하지 않았다.

- build `20261117T125832993Z-build` 성공/입력변경0. matrix 목록 `TestResults/20261117-fullviewport-matrix.json`에12조합 run경로를 기록했다. 필수8화면×12의 실제 보고서96개 해상도/배율/문제목록을 대조해 배치수치 이슈0. 각 실행 일반PNG14개는 단색 실패. 진단PNG가 있더라도 전체 runner 통과로 바꾸지 않았다.
-1280×720/UI150의 art-far/work/stove/save-offscreen을 정적 검토했다. 안내줄은 빈 패널을 닫을 때 하단으로 이동한다. 작업표3행/조리대 하단 명령은 ScrollRect에 있어 실제 스크롤 접근 확인이 필요하다. 정상 창·사람 검증 아님.
- 기준 대조에서 월드 교체 후 이전 탭 유지와 정착민 버튼의 인물 그림 미연결을 발견했다. 월드 교체 때 열린 탭 초기화, 맵과 같은 pawn.front·PawnColor의 버튼 그림을 추가했다. 이는 위 matrix 이후 변경이므로 새 렌더 검사가 필요하다.
- PlayMode `20261117T130210190Z-3604f52b`:85개 중 반복Load/Reset 검사1개에서 첫 소집 토글 결과false 실패. 제품 원인 확정 못함. UI차단/로드 직후 소집 상태 사전assert 추가 후 `20261117T130322646Z-25144c70`85/85통과. 비재현1회로 해결 판정하지 않으며 추가 반복 확인 중.

# 새 UI 기존 명령 연결 검증 추가 (2026-11-17)

인물 버튼 후속:build `20261117T130826274Z-build`와capture `20261117T130905047Z-ux-capture`에서 인물 그림은 연결됐지만 소집 문구 잘림9건(3화면×3명)을 발견했다. 이름/상태2줄,글자16,버튼56높이로 수정 후build `20261117T130956868Z-build`성공. capture `20261117T131036474Z-ux-capture`1280×720/UI150의 배치문제0, pawn-directions-offscreen에서3명 소집문구 표시를 확인했다. 일반 캡처14개는 단색 실패. 전체12조합과 배고픔/피곤함 동시 문구는 최종 수정 이후 다시 검토해야 한다.

- PlayMode87/87:`20261117T130734618Z-b3cd7b49`.
- ZoneAndGatherCommandsUpdateImmediatelyWithoutAdvancingPausedWorld(V-U07/U08 부분):채집 지정/취소,역방향2×2저장/경작 구역 생성과 도구 종료,자원3종 필터 토글·다음프레임 문구,파종 토글·문구,구역삭제와 목재20보존,전체Tick0. UI callback/handler 연결 검사이며 물리 드래그 증거 아님.
- ConstructionButtonsCancelDeliveredMaterialsAndToggleDemolitionWithoutTick(V-U07 부분):반입목재2의 벽 설계 취소 후 전체목재20보존·점유해제. 다른 빈칸에 실제 벽 완공 후 철거 지정/해제·문구와Tick보존. 첫 실행`20261117T130615917Z-5692b5a9`는 반환자재가 놓인 동일칸을 재건설하려 해 실패. 검사 fixture를 빈칸으로 고쳤으며 제품 배치규칙은 변경하지 않았다.
- WorkTableChangesPriorityWithoutRestartingCookingAndOffReleasesWorker(V-U05 추가):진행중 중지→주문 삭제로 투입2반환/전체식재료보존/진행0/점유해제,다음 UI에 주문재생성 표시,새ID·중지·입력0·진행0 주문 재생성,목표12→13→12와Tick보존을 추가했다.
- 실제 입력 이벤트→handler 전체 조합,자원/작물이 진행 중인 모든 삭제 분기,대표 진단별 UI값,사람 조작은 이 증거만으로 완료하지 않는다.

# 실제 작업표 휠·클릭 입력 경로 (2026-11-17)

- `WheelScrollReachesLastWorkRowWithoutZoomAndPointerClickChangesOnlyThatPriority`(V-U03/U09 부분):실제 SettlementShell 작업표를150논리단위 높이로 제한해 overflow를 먼저 확인. InputSystem 합성 Mouse→InputSystemUIInputModule.Process→GraphicRaycaster→ScrollRect로 마지막행까지 스크롤. 마지막행 버튼중심이viewport안인지 검사한 뒤 press/release로 정착민3 Cook우선순위가 한 단계만 변경되고 정착민1값은 유지됨.
- UI위 휠8회 동안 카메라 위치/줌/Tick0유지. 패널을 닫고 실제 맵칸에서 동일 휠이 줌을 변경하는 양성 대조 포함. 테스트 입력·초점 설정은 finally에서 복원.
- `20261117T131234762Z-0f94d72f`, `20261117T131332245Z-173b9b9e`는새 UI생성 직후 입력 적중실패. 실제 좌표/활성패널/EventSystem을 확인하고 생성후2프레임 기다리도록 fixture수정. 제품변경없음.
- `20261117T131413789Z-d84f78fd`88/88통과. 맵휠 양성대조 추가 후 `20261117T131510635Z-b3ee7f6e`88/88통과.
- 배치테스트 GameView640×480에서 패널높이를 제한한 검사다. 실제1280×720/UI150의 사람 휠·스크롤확인을 대체하지 않는다. 전체 V-U03/U09나 M2 완료가 아니다.

# 아트 교체 상태·기준점 검사 (2026-11-17)

작물 경계 진단:build `20261117T131929408Z-build`성공/입력변경0. `20261117T132012860Z-ux-capture`1280×720/UI150의crop-boundaries-offscreen을검토했다. 왼쪽부터Growing0/1199/1200/2399/Mature2400의작성fixture이며작은잎2/큰잎2/금색이삭1로일치한다. 정상복원검증경로를통과하지만실제성장진행증거는아니다. 배치이슈0,일반PNG15개는단색실패이므로runner exit1. 이전장면의도구안내문이남는문제는별도정리대상이다.

- `CatalogReplacementPreservesSaveStateRestorationAndProjectedGroundAnchors`(V-A01/V-I03 부분):실제 진행중 이동을 포함한WorkScenario의 저장 bytes전체를 기준으로 기본도형→다른sprite/pivot/1.6배WorldSize의검사catalog→반입catalog→검사catalog→반입catalog를 교체했다. 모든 교체에서 저장bytes동일/표시개수동일,복원본bytes동일,복원후10Tick결과동일.
- 정착민1/나무301/침대201의실제SpriteRenderer변환을 통해 작성된ground anchor를screen좌표로투영해 기대점오차≤1px,해당화면좌표의칸선택일치를 확인. 단순Definition계산뿐아니라렌더러scale/pivot/localPosition까지 검사했다.
- `20261117T131818040Z-eb79b754`PlayMode89/89통과. 반입catalog의최종아트채택은별도이며이테스트로IsFinalArt=true처리하지않음. 실제발/시설밑면에기준점이맞는지와사람식별은남음.

# 월드 교체 안내 정리와 문제 대상 표시 (2026-11-17)

후속build `20261117T132419784Z-build`성공/입력변경0. capture `20261117T132458411Z-ux-capture`1280×720/UI150 배치이슈0,art-default-offscreen에서오래된도구안내없음·전체수량표시·목재/식재료/식사이름과칸표시를확인했다. 일반PNG15개단색실패로runner exit1유지.

- 새 World를 연결할 때 이전 Feedback을 비운다. 성공한 불러오기 메시지는 새 월드 Refresh 뒤 설정해 유지하고,실패는 기존 월드와 실패 안내를 보존한다. 기존 새정착지/성공로드/실패로드/10회Reset검사에 각각안내조건을 추가했다. PlayMode89/89:`20261117T132146024Z-7b530b50`.
- 문제 알림은 내부ID 대신 시설/물자/작물 이름과 칸을 표시한다. 자원 요약 앞에 전체 범위를 명시한다.
- `WorkNoticeNamesTargetDeduplicatesAndClearsWhenCauseIsResolved`(V-U06부분):건설담당없음→동일안내5프레임중복0→클릭시벽설계선택과행동ID유지→건설우선순위1로원인해소→다음UI에서기존안내제거,Tick0/전체목재20표시확인.
- PlayMode90/90:`20261117T132330901Z-8ef9a1d3`. 다른진단원인전부와운반/공정수량분기의완료증거로확대하지않는다.

# 작업 진단 4종과 운반·공정 수량 표시 (2026-11-17)

- ConstructionNoticeTracksMissingMaterialsAndBlockedAccess(V-U06):자재없는벽설계의재료부족안내→세로장벽추가후접근불가→장벽해제후재료부족. 이전원인의안내제거,현재문구일치,알림클릭시대상선택과Tick유지를확인.
- CookingNoticeDistinguishesFullOutputFromNormalTargetAndCountsInputsOnce(V-U06):실제벽대신조리대완공후식재료120으로식사50개생산. 목표99의출력포화는위험표시,목표50은정상·조리대,목표51은다시포화. 출력49로한칸비우고후속반입에서포화안내제거.
- 식재료바닥18+운반2일때전체20,바닥18+공정2일때전체20을각각UI에서관측. Fixture가운반단계를실제로거쳤는지별도assert.
- `20261117T132642779Z-ccdfe5db`92/92,운반수량추가후 `20261117T132742055Z-b34576a6`92/92통과. 알려진담당없음검사와합쳐필수진단5종의대표상태를커버하나모든시설/진단조합을완료로확대하지않는다. 물리사람입력아님.

