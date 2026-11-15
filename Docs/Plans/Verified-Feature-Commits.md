# 검증된 기능의 커밋 구성 제안

2026-11-15. 사용자가 이 문서의3개 기능별 커밋 실행을 승인했다. 분리 시작 기준 HEAD는 `7d20688`이다. 검토 시작 시 변경55파일(기존24/신규31), 스테이징 없음. 이후 사용자 정상 확인과 이 제안 문서만 갱신했다. 제품 소스·패키지·에디터 버전 변경 없음.

## 권장 순서

| 순서 | 메시지 | 기능과 포함 범위 |
|---|---|---|
| 1 | `Add gathering, hauling and storage workflows` | 채집 지정·생산, 스택/저장 칸 점유, 운반·입고·필터, 생활 우선순위와 R 취소/낙하, 기본 작업 UI, 물류 시나리오·회귀 검사·계약·계획·데모 안내 |
| 2 | `Improve settlement readability and pawn inspection UI` | 정착민 고유색/상시 이름, 종류별 모양·이름, 저장 바닥/외곽선, 하단 행동/운반/목적지/점유 정보, 한글·선택 연결·UI 검사·계약·화면 기록 |
| 3 | `Apply paused commands immediately and reset the storage tool` | 설정/취소의 Tick 대기 제거, 무효 점유 해제와 물자 중복 방지, 다음 Tick 자동 재선택, 구역 생성 성공 후 도구 해제, 정지 UI 검사·최종 문서·사용자 확인 |

각 커밋에 해당 기능의 코드·검사·문서를 함께 넣는다. 문서를 전부 마지막 커밋으로 미루지 않는다. 장점은 기능별 리뷰·문제 추적이 쉽다는 점이며, 비용은 뒤섞인 파일을 변경 단위로 분리하고 중간 상태를 다시 검증해야 한다는 점이다. 뒤 커밋은 앞 커밋에 의존하므로 독립 cherry-pick이나 앞 커밋만의 revert를 보장하지 않는다.

## 파일 배분 기준

- 1: `WorkEntities.cs`, `WorkScenario.cs`, `WorldState.Work.cs`, `WorkTests.cs`, `WorkIntegrationTests.cs`와 각각의 `.meta`; ColonistState/LivingEntities/ScenarioDefinition/WorldState/WorldState.Living/SimulationRunner의 노동 연계; SettlementView.Work와 기본 도구 입력; DemoBootstrap의 물류 검증; Run-Demo의 실패 원인 보고. GatherHaulStorage 계약, Task3 계획, WorkDemo, work 검증/PNG 포함.
- 2: `MapSymbolGraphic.cs`, `SettlementView.Inspection.cs`, `ReadableUiTests.cs`와 `.meta`; SettlementView/SettlementView.Work의 렌더·배치·한글 변경, 기존 PlayMode UI 기대와 Demo 표시 검사. ReadableSettlementUI 계약, ReadableUiDemo, readable-ui 검증/PNG 포함.
- 3: WorldState.Work의 즉시 지정/구역/필터/삭제·집기 전 취소·낙하 자원 선택 시점, WorldState.Living의 즉시 취소, WorldState의 Tick 실행 여부 구분, SettlementView.Work의 저장 도구 종료/즉시 적용 표시, PausedCommandTests와 `.meta`, 관련 기존 검사 수정·Player 무시간 단언. ImmediatePlayerCommands 계약, immediate-commands 검증, 이전 계약/안내의 적용 시점 수정과 최종 상태 포함.
- 공용 문서 README/Architecture/Decisions/ParallelWork/Roadmap/Settlement/Status/Testing 및 검사 manifest는 해당 커밋까지 구현된 내용만 순차 반영한다. 사용자 확인 및 현재 제안은 마지막 상태에 반영한다.
- Assets 신규 파일과 `.meta`는 같은 커밋에 포함한다. Builds/Library/Temp/TestResults와 자동 생성 로그/실행 파일은 제외한다. Docs의 선별된 검증 PNG 2개는 포함한다.

`WorldState.Work.cs`와 `SettlementView.Work.cs` 자체가 신규 파일이면서 후속 수정도 포함하므로 단순 파일 스테이징이나 기존 파일의 hunk 선택만으로 분리가 끝나지 않는다. 커밋1용 내용과 이후 변경분을 구성해야 한다. 현재 최종 파일을 보존하고 분리 결과의 최종 트리가 검증된 현재 상태와 일치하는지 대조한다. 이번 제안 단계에서는 실제 분리·스테이징을 하지 않았다.

## 검증 상태와 이후 절차

현재 최종 상태: EditMode80/PlayMode36, Windows 빌드 오류0·경고0, 실제 Player/PNG 통과. 네 실행의 입력150개와 빌드 출력183개가 현재 해시와 일치한다. 사용자 전체 정상 확인 완료. 기록은 [최종 검증](../Verification/2026-11-15-immediate-commands.md)을 따른다. 이번에는 파일 대조와 diff 검사만 했으며 Unity를 다시 실행하지 않았다.

승인 후 분리할 커밋1·2의 정확한 트리는 아직 미검증이다. 과거106/110검사 통과를 새로 구성한 중간 트리의 통과로 간주하지 않는다. 각 단계에서 그 상태의 manifest와 EditMode/PlayMode, Windows 빌드·Player를 직렬 검증하고 통과한 단계만 커밋한다. 최종 단계에서는 원래 검증 입력과 소스 일치 및 전체116검사/Windows 결과를 확인한다. 에이전트가 CLI로 수행 가능하다. 새 동작 변경이 없다면 사용자에게 동일 조작 확인을 다시 요청하지 않는다.

분리 과정에서 중간 상태 재구성이 과도하게 커지면 통합1커밋(`Add verified settlement work, readable UI and paused controls`)이 대안이다. 최종 검증 상태를 그대로 보존하기 쉽지만 기능별 리뷰와 추적은 불리하다. 사용자 승인 범위가3커밋이면 임의로 통합1커밋으로 바꾸지 않는다.

기존 Player 종료 ComputeBuffer/D3D12 4096B 경고는 미해결이다. 장시간 GPU 프로파일링, 다른 OS/해상도 실제 플레이는 미검증이다. 이번 기능들의 정상 확인과 구분한다. 원격 연결·push·Computer Use 없음.

## 실행 승인과 결과

사용자는 각 중간 상태 검증 후3커밋 저장을 승인했다. 원격 연결·push는 승인되지 않았다. 제안 당시 미검증/미실행 문장은 당시 기록이며 실제 수행 결과는 [커밋 검증](../Verification/2026-11-15-feature-commits.md)을 따른다. 이번3커밋 이후 추가 커밋은 다시 승인받는다.
