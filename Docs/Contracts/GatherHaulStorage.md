# 채집·운반·저장 공통 계약

2026-11-15 사용자 추천안 구현 승인에 따라 확정. [기능 계획](../Plans/Task3-Gather-Haul-Storage.md)의 완료 기준을 따른다. 구현 완료 증거는 별도 검증 기록에 둔다.

## 행동과 물자

- World는 채집 대상 목록, 저장 구역/칸, 동적 자원 목록과 명령 큐를 소유한다. ScenarioDefinition은 선택적 GatherSeed 목록을 복사한다. 기존 시나리오 생성자의 인수와 의미는 유지한다.
- Pawn의 WorkActivity는 Idle/GoingToGather/Gathering/GoingToPickup/PickingUp/GoingToStore/Storing이다. LifeActivity와 동시에 활성화하지 않는다. CargoKind/CargoAmount와 WorkTargetId는 읽기 공개, World만 변경한다.
- 모든 생활·노동 취소는 같은 정리 경로를 지나며 `(Pawn ID, Action ID)`가 일치하는 권한만 해제한다. R은 즉시 정리한다. QueueCancelLife의 기존 API도 공통 취소 경로를 사용하며 오래된 세대는 무효다.
- 한 안정 상태의 상호작용 권한은 하나다. 출발 스택 접근 후 집기 시 도착 칸 권한을 확보하고 출발 수량을 운반물로 옮겨 출발 권한을 해제한다. 실패하면 집지 않는다.
- 저장 칸의 보관 스택은 칸과 동일한 권한 객체를 사용한다. 식사와 입고가 독립 권한을 만들지 않는다. 비어 있는 칸도 동일한 권한을 가지며, 저장 스택 생성 시 그 권한을 연결한다.
- 운반물은 점유 대상이 아니다. 취소하면 현재 논리 타일에 독립 미보관 스택을 생성하고 운반량을 0으로 만든다. 이미 점유된 스택에는 합치지 않는다.
- 생성/소비/외부 추가/외부 제거와 운반을 구분한다. 운반은 총량을 변경하지 않는다. 미보관+보관+운반을 자원 종류별로 조회한다.

## 명령과 조회 API

- `QueueDesignateGather(id, designated)`는 요청을 변경한다. 취소는 현재 담당 행동도 정리한다. R은 이 전역 요청을 제거하지 않는다.
- `QueueCreateStorage(first, second)`, `QueueStorageFilter(zoneId, kind, allowed)`, `QueueDeleteStorage(zoneId)`는 다음 Tick에 호출 순서대로 적용한다. 유효하지 않은 요청은 오류 코드로 표시하며 부분 생성하지 않는다.
- `Gatherables`, `StorageZones`는 읽기 목록이다. 대상의 진행도·소진·지정·권한, 칸의 위치·보관 스택·권한을 공개한다.
- `PendingCommandCount`에 작업 설정 명령도 포함한다. 취소는 Action ID 검사, 생성 ID는 월드 내 단조 증가/재사용 금지. Reset은 새 World와 빈 명령 큐다.
- 새 일반 작업은 운반 우선, 다음 채집이다. 거리/요청순서/대상 ID 동률 처리, Pawn ID 순 배정을 유지한다. 재탐색은 관련 revision 변경 또는 10Tick 후다.

## 생활·시간 연결

외부 명령/무효 처리 → 욕구 갱신 → 노동의 생활 중단 판정 → 기존 생활 행동 완료 → 생활 선택 → 기존 노동 진행·완료 → 노동 선택 → 이동/도착 순이다. 기존 생활 회복·소비 Tick은 바꾸지 않는다. 도착/시작 당일에는 진행하지 않는다. 노동 중 수면 목적과 해결 가능한 식사를 우선하며, 음식이 없다고 노동을 반복 취소하지 않는다. 자신이 점유하거나 운반 중인 먹을 수 있는 음식도 중단 가능성 판정에 포함한다.

식사·침대 선택은 기존 우선순위다. 일반 수동 이동 도중 노동 배정을 보류하고, 노동/생활 중 우클릭은 R 안내와 함께 거부한다. 소집 중 자동 행동은 없다.

## 파일 소유권과 검증 순서

이번 요청에는 병렬 에이전트 실행 지시가 없으므로 root가 아래 파일을 순차 소유한다. 기존 휴면 에이전트에는 파일을 배정하지 않는다.

| 기능 경계 | root 소유 파일 |
|---|---|
| Simulation 공통 상태·작업 | `Assets/Game/Simulation/WorldState.cs`, `WorldState.Living.cs`, 신규 `WorldState.Work.cs`, `WorkEntities.cs`, `ColonistState.cs`, `LivingEntities.cs`, `ScenarioDefinition.cs`, 시나리오 파일 |
| Runtime·표시 | `Assets/Game/Runtime/SimulationRunner.cs`, `Assets/Game/Presentation/SettlementView.cs`, 필요 시 전용 partial/마커/입력 파일 |
| 검사·실행 | `Assets/Game/Tests` 아래 신규 및 기존 관련 검사, `Assets/Game/Demo/DemoBootstrap.cs`, `Assets/Game/Editor/DemoBuild.cs`, 필요한 Tools 검증기 |
| 공유·문서 | 관련 Docs/README, 신규 파일 .meta. 엔진/패키지 버전은 유지 |

구현 뒤 입력 동결 → EditMode 회귀/신규 → PlayMode → Windows build/smoke → 실제 렌더 이미지 확인 순서로 root가 직렬 실행한다. 사용자 에디터를 강제 종료하거나 잠금을 삭제하지 않는다. 실제 마우스·키보드 사용자의 확인은 별도 미검증 항목으로 남긴다. 커밋은 별도 승인 없이는 실행하지 않는다.

생활/입고 경쟁 보완: 같은 Tick의 식사 선택을 입고용 칸 점유보다 먼저 처리한다. 이를 위해 노동 진행은 생활 선택 뒤에 둔다. 접근/도착 다음 Tick 진행 및 기존 생활 완료 시점은 유지한다.
