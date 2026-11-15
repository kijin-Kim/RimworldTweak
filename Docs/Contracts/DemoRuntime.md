# Windows 초기 정착지 데모 계약 v1

범위: 0-C/D/E2/F. 이동·생활·생산·전투 제외. 기존 초기 데이터 계약 유지.

## 시간·수명 API

namespace RimworldTweak.Simulation:
- WorldState.Tick: long getter, 초기 0. Step(long count): 음수 ArgumentOutOfRangeException, checked overflow로 상태 변경 전 실패. 기존 개체 데이터는 변경하지 않음.
- SimulationSession(ScenarioDefinition scenario): World getter, ResetCount int getter(초기 0), PendingSeconds decimal getter. AdvanceTime(decimal seconds): 음수 거부, 0.1초당 1 Tick, 잔여 시간 보존. Step(long count)는 World.Step 전달. Reset()은 새 월드 생성, pending=0, ResetCount 증가. 이벤트 없음.
namespace RimworldTweak.Runtime:
- SimulationRunner : MonoBehaviour: Session getter, IsPaused getter, Initialize(ScenarioDefinition scenario), SetPaused(bool), ResetWorld(), StepOnce(). Awake에서 세션 없으면 SandboxScenario로 초기화. Update가 unscaledDeltaTime을 decimal로 전달(자동 진행 10Tick/sec); IsPaused면 진행 안함. Initialize는 세션 교체 및 paused=false; ResetWorld는 pause 유지. StepOnce는 paused일 때만 1Tick. 이벤트 구독을 사용하지 않아 중복 구독 원천 제거.

## 표시 API

namespace RimworldTweak.Presentation:
- SettlementView : MonoBehaviour: Bind(SimulationRunner), RefreshNow(), Runner getter. LateUpdate에서 읽기 상태를 조회. Bind 반복 시 UI/표시를 중복 생성하지 않음. 다른 Runner/World로 바뀌면 실제 데이터로 재표시.
- ViewKind enum Tile, Colonist, Resource. WorldMarker : MonoBehaviour: Kind, EntityId, Position, Amount, IsBlocked getter, UI Image와 필요한 Text를 실제 객체에 둔다. readonly IReadOnlyList<WorldMarker> Markers로 노출.
- SettlementView.TickText, ResetButton, PauseButton, StepButton 공개 getter (UnityEngine.UI). Reset 버튼 → Runner.ResetWorld, Pause → SetPaused 토글, Step → StepOnce. 버튼 리스너는 생성 시 1회 연결, 재Bind 시 현재 Runner를 참조. 생성 UI root는 View 소유, 파괴 시 정리.
- ScreenSpaceOverlay Canvas와 1280×800 reference scale UI로 8×8 board를 렌더. 타일은 어두운 장애물, 정착민 triangle sprite, 자원 square+실제 Amount 텍스트. 맵 크기·좌표·수량은 World에서 읽는다. 사람 가독성은 별도 미검증.
- 공개 marker 데이터뿐 아니라 테스트는 실제 Image/Text/RectTransform과 총 개수를 검증한다.

## 소유권 (작성만 병렬, 실행은 root)

- demo_runtime: Simulation/WorldState.cs 수정, Simulation/SimulationSession.cs 신규, Runtime/SimulationRunner.cs 신규 및 각각 meta. 다른 기존 파일 수정 금지.
- demo_view: Presentation/*.cs 및 각각 meta. Canvas/UI는 코드 생성. Scene/asmdef 수정 금지.
- demo_tests: Tests/EditMode/Simulation/TickTests.cs, Tests/PlayMode/Integration/*.cs 및 meta; Tools/expected-editmode-tests.json에 새 테스트 추가, expected-playmode-tests.json 생성. 기존 테스트 변경 금지.
- root: 공통 asmdef·폴더 meta, Scene/Editor 빌드 스크립트, Player 스모크, CLI 도구 수정, 문서, 단일 Unity 실행.

스모크는 실제 빌드에서 진행·정확 Tick·반복 Reset·표시 데이터 확인 후 JSON 결과와 실제 화면 PNG를 저장하고 정상 종료한다. 자동화 플래그에서만 활성화하고 일반 실행은 인터랙티브 데모다. 사람 화면·조작은 체크리스트 제공, 미검증 표시. 코드·설정·도구 동결 후 검증하며 커밋·원격 작업·Computer Use 금지.

## 작업 1 확장

이 문서는 작업 0의 API 기준이다. 현재 이동 상태·시간·화면 입력 API는 [MovementTime.md](MovementTime.md)가 확장한다. 정착민 seed는 불변 초기값이며 실행 위치는 ColonistState가 소유한다.
