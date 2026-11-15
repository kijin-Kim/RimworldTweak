# 작업 1 공통 계약과 파일 소유권

2026-11-15 사용자 승인. [상세 계획](../Plans/Task1-Movement-Time.md)의 추천 조작·규칙을 이번 구현 기준으로 확정한다. 이동·시간 조작만 구현하며 생활·생산·전투는 제외한다. 기존 데모 계약을 아래 내용으로 확장한다.

## Simulation 공개 API

namespace RimworldTweak.Simulation. Unity 참조 없음.

- ColonistSeed는 불변 초기 데이터 유지. WorldState.Colonists는 IReadOnlyList<ColonistState>로 확장. ColonistState 공개 getter: Id(int), Position(GridPosition), Destination(GridPosition?), Path(IReadOnlyList<GridPosition>, 현재 이후 남은 경로), MovementProgress(int 0~4), Status(MovementStatus), HasPendingCommand(bool), PendingDestination(GridPosition?). 외부 setter 없음. MovementStatus: Idle, Moving, Arrived, Blocked. Pending은 활성 이동 상태를 훼손하지 않는 별도 표시다. PendingDestination null+HasPendingCommand true는 Stop 예약.
- WorldState 기존 Width/Height/Tick/Resources 유지. BlockedTiles는 readonly, 통행 변경 후 반영. MapRevision(long), PathSearchCount(long), PendingCommandCount(int), LastCommandError(string, 정상은 빈 문자열) 추가.
- QueueMove(int colonistId, GridPosition destination): bool 접수 성공. ID/범위/장애물은 즉시 검사, 실패 시 기존 유효 명령 보존, LastCommandError 설정. 경로 검사는 적용 Tick에 수행. QueueStop(int colonistId): bool. 오류 코드는 InvalidColonist, OutOfBounds, BlockedDestination, Unreachable, OccupiedTile. 유효 접수/명령 실행 성공은 이전 오류 해제.
- QueueSetBlocked(GridPosition position, bool blocked): bool. 테스트용 통행 변경 명령. 범위/현재 정착민 점유 검사. 실제 변화는 다음 Tick 시작, 이동 명령보다 먼저 적용하며 적용 시도 점유 재검사. 변화가 있을 때만 MapRevision 증가. no-op은 경로 진행을 지연하지 않음.
- FindPath(GridPosition start, GridPosition destination): IReadOnlyList<GridPosition> 또는 null. 출발 제외·도착 포함; 제자리는 빈 목록; 범위/차단/도달 불가 null. 동→북→서→남 BFS. 검색 횟수 PathSearchCount 증가. 반환 경로 외부 변경 불가.
- Step(long count): 음수/최종 Tick overflow 사전 거부. Tick마다 통행 변경→활성 경로 revision 재평가→수신 순서 이동/중지 명령→ID순 이동 갱신. 명령들 사이 위치 이동 없음. 마지막 유효 명령 유지, 실패는 직전 유효 명령 보존. 동일 활성 목적지 명령은 경로·진행 보존. 다른 목적지는 현재 타일부터 진행0. 5번째 Tick에 한 칸 이동. Stop을 적용한 Tick에는 이동하지 않음. 도착 후 Path 빈 값·진행0. Blocked는 목표 유지, revision 또는 새 명령에서 재탐색. Step(0)은 명령 미소비.
- World/colonist/path/command/blocked 상태는 월드별 독립. Reset은 초기 seed에서 모두 새로 생성. overflow 테스트는 Reflection으로 테스트 내부에서 Tick backing field를 경계값으로 설정하고 실제 긴 Tick 반복을 하지 않는다. 제품에 테스트용 시간 조작 API를 추가하지 않는다.

## Runtime 공개 API

SimulationRunner 기존 Session/IsPaused/Initialize/SetPaused/ResetWorld/StepOnce 유지.
- SpeedMultiplier int getter 기본1. SetSpeed(int multiplier)는 1 또는3만 허용, 선택 후 재개. SetPaused(false)는 마지막 배속 유지.
- AdvanceTime(decimal realSeconds): 음수 거부, 정지 중 시간 무시, 실행 중 realSeconds*SpeedMultiplier를 Session.AdvanceTime에 전달. Update가 이 경로를 사용한다. Session.PendingSeconds는 게임 시간 잔여다.
- MoveColonist(int id, GridPosition target), StopColonist(int id): bool, World.Queue 명령 전달.
- Initialize는 speed1/pausedfalse, ResetWorld는 pause/speed 유지. StepOnce는 정지 중 정확1 Tick, 잔여·배속 유지.

## Presentation 공개 API

SettlementView 기존 API/marker 계약 유지. 같은 World의 위치/통행 변화도 RefreshNow에서 반영, 객체 재사용.
- SelectedColonistId: int? getter. SelectColonist(int id), ClearSelection(), MoveSelected(GridPosition): bool, StopSelected(): bool.
- HandleMapClick(GridPosition position, bool rightButton): bool. 범위 밖 거부. 왼쪽 클릭은 해당 타일 최소 ID 선택 또는 해제, 오른쪽은 선택 대상 이동. 버튼 UI 클릭은 이 경로로 전달되지 않는다.
- TryScreenToGrid(Vector2 screenPosition, out GridPosition position): bool. 실제 Canvas/Board RectTransform 기준 변환, max 경계 밖 거부. BoardRect: RectTransform getter.
- Speed1Button, Speed3Button, StopButton: Button getter. SelectionText: Text getter. CommandFeedbackText: Text getter(명령 거부 사용자 문장). ColonistButtons: IReadOnlyList<Button> getter(ID순). 클릭 가능한 정착민 목록 제공.
- ProcessShortcut(Key key): bool (UnityEngine.InputSystem.Key), Space/ Digit1/ Digit3/Period/Escape/X. 실제 Update는 wasPressedThisFrame만 전달하며 텍스트 입력 포커스에서는 차단. UI와 단축키가 같은 Runner 메서드 호출. OS 입력 주입은 하지 않는다.
- 맵은 EventSystem의 board pointer handler로 클릭을 받는다. 맵 외부·UI는 경로를 호출하지 않는다. 목록·버튼·맵의 실제 raycast 대상/좌표를 PlayMode에서 검사 가능하게 유지.
- 선택 강조·목적지·남은 경로는 WorldMarker 목록과 분리(기존 marker count 유지). Pending은 예약 목적지/중지 표시, 적용 전 활성 경로와 구분. Stop과 재지정 피드백 표시.
- World 교체/Reset/다른 Runner Bind 시 선택과 표시 피드백 제거. 기존 버튼 리스너1회, marker 재사용 유지. 화면은 영어 임시 UI 유지. Step는 정지 중만, Stop은 선택 시만 활성.

## 파일 소유권 — 계약 확정 후 작성만 병렬

| 담당 | 단독 쓰기 범위 |
|---|---|
| movement_sim | Assets/Game/Simulation/WorldState.cs, 새 ColonistState.cs 및 필요 Simulation/*.cs와 meta. SimulationSession/기존 seed 파일/asmdef는 수정하지 않음 |
| movement_view | Assets/Game/Presentation/*.cs 및 meta. asmdef·Scene·Runtime·Tests 수정 금지 |
| movement_tests | Assets/Game/Tests/**/*.cs 및 신규 meta, Tools/expected-*-tests.json. 기존 검증 의미 보존하며 Tick overflow/readonly type 변경 적응 |
| root | Runtime/SimulationRunner.cs, Demo/Bootstrap smoke, Editor/scene, 모든 asmdef, 도구·문서·최종 통합 수정 및 모든 Unity 실행 |

공동 파일 변경은 root에게 요청한다. 작성 종료 후 동결 보고, root가 테스트·빌드·Player 실행을 직렬 수행한다. 검증 중 소스 쓰기 금지. Computer Use·커밋·원격 연결·push 금지. 공유 폴더 그대로 사용.
