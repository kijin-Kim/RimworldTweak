# 전투 독립 규칙 검증

2026-11-17. [실행 계획/완료 기준](../Plans/Combat-Rules-Foundation.md), [교차 검토](../Reviews/2026-11-17-combat-rules-review.md).

## 실제 변화

화면·조작 변화 없음. 새 Simulation 규칙은 사선/직선거리/표적 인접 엄폐 정보를 계산하고, 무기·방어구 설정을 검증하며 명중을 가정한 원본 피해에서 고정 방어력을 한 번 뺀다. 기존 World/정착민/저장/UI 제품 코드는 변경하지 않았다. 별도 통합 worktree에서 검증하며 원래 UI 작업 폴더는 수정하지 않았다.

제품: `Assets/Game/Simulation/SightRules.cs`, `EquipmentRules.cs`. 검사: `SightRulesTests`, `EquipmentRulesTests`, `CombatRulesIntegrationTests`. 새 검사 24개를 기존 필수 목록에 추가했다.

## 완료 기준별 증거

| 기준 | 실제 관측/테스트 | 판정 |
|---|---|---|
| F1 | RangeIncludesExactEuclideanBoundaryAndRejectsBeyond: (3,4)/range5 가능, range4 거부. InvalidArgumentsAndCoverValuesAreRejected, ExtremeCoordinatesRejectBeforeOverflowOrTraversal, RejectedGeometryDoesNotQueryTheMap 통과 | 통과 |
| F2 | AxisAndDiagonalWallsBlockInBothDirections, CornerTouchingSideCellsBlockEvenAdjacentDiagonalShots, TargetFrontCoverProtectsButSideAndDistantCoverDoNot, MultipleCoverCellsUseMaximumWithoutAddingAndBlockClearsCover, EndpointCellsAreNeverQueriedOrUsedAsCover 통과. TraversalMatchesIndependentSegmentRectangleIntersection은 [-4,4]의 80방향에서 독립 선분/사각형 판정과 조회 칸 집합 및 역방향을 대조 | 통과 |
| F3 | EquipmentRulesTests 10개 모두 통과: 원거리2/근접1, 잘못된 입력 거부, 피해20/미착용=20, 방어5=15, 방어20 이상=0. int.MaxValue 피해의 흡수+잔여 합 보존, 불변 자료, 반복 계산 동일 | 통과 |
| F4 | BlockedAndOutOfRangeAttacksNeverProduceDamage: 결과 없음. FrontCoverChangesSightInformationButDoesNotReduceDamageTwice: 정면 cover50/측면0, 양쪽 피해20-방어6=14. RepeatedRuleQueriesPreserveLiveWorldAndSerializedSave:100회 질의 전후 보유 Session JSON 동일, 복원과 원본의 후속5Tick 동일 | 통과 |
| F5 | EditMode 전체212/212, PlayMode 전체103/103. 두 검사 모두 종료0/timeout 없음/입력 변경0 | 통과 |
| F6 | Windows build Succeeded, 종료0/timeout 없음/입력 변경0. 관련 문서 링크와 완료 기준 대조 완료 | 통과 |

F4의 호출 조합은 테스트의 ResolveAssumedHit이며 제품 공격 API가 아니다. 맵은 명시적인 callback 시험 자료다. Session 불변 검사는 실제 맵 어댑터·피해 적용·건강 저장을 증명하지 않는다.

## 실행 기록

- Unity 6000.3.28f1, 통합 worktree: `C:/Users/kijin/.codex/worktrees/rimworld-combat-integration/RimworldTweak`.
- 기준 소스는 [전달 hash](2026-11-17-combat-source.json)의385개 제품/검사 입력. A/B 전용 파일은 복사 후 SHA256 일치를 확인했다.
- EditMode: `20261117T144642524Z-eaa317a6`, 전체212/212, 종료0, timeout 없음, 입력 변경0. [결과](../../TestResults/20261117T144642524Z-eaa317a6/summary.json), [XML](../../TestResults/20261117T144642524Z-eaa317a6/results.xml).
- 원래 UI 작업의 검사와 충돌하지 않도록 해당 runner.lock을 OS 배타 잠금으로 확보한 동안 별도 통합 공간의 검사를 직렬 실행했다. 첫 잠금 시도는 진행 중인 원래 검사 때문에 실행하지 못했으며, 대기 후 확보했다. 열린 프로세스 강제 종료/잠금 삭제 없음.

## 한계와 인계

규칙 계산은 게임 공격의 기초다. 실제 발사 명령/조준 진행/명중 확률/부상/정착민 장착/저장 버전/UI 표시는 미구현이며 승인 범위 밖이다. 렌더·사람 플레이는 실행하지 않았고 이번 순수 규칙의 완료 기준이 아니다. 원래 UI 작업 공간으로의 반입은 아직 하지 않았다. 후속 통합은 신규 소스/테스트/meta와 검사 manifest의 신규24개만 전달하고, 변화한 UI/저장 파일이나 전체 옛 snapshot을 덮어쓰지 않는다.

커밋/push 없음. 전투 전체의 G1~G3 통과나 현재 UI·세이브 작업 완료로 보고하지 않는다.

- PlayMode: `20261117T144908924Z-7532f00f`, 전체103/103. [결과](../../TestResults/20261117T144908924Z-7532f00f/summary.json), [XML](../../TestResults/20261117T144908924Z-7532f00f/results.xml).
- Windows build: `20261117T145010125Z-build`, 성공. [결과](../../TestResults/20261117T145010125Z-build/summary.json), [빌드 요약](../../Builds/Windows/build-summary.json). 실행 파일은 이 worktree의 `Builds/Windows/RimworldTweak.exe`이며 이번에는 실행하지 않았다.
- [선별 인계 manifest](2026-11-17-combat-handoff.json): 신규 코드/테스트/meta10개와 추가할 테스트명24개. 원본 UI 작업의 더 최신 상태는 이 검증 범위에 포함하지 않는다.

## 최종 완료 판단
F1~F6을 이 통합 snapshot에서 충족했다. 승인된 독립 규칙 구현은 완료다. 실제 게임 연결과 원래 UI 폴더 반입은 후속 작업이며 전투 전체가 완료된 것은 아니다. 새 규칙+전용 테스트+관련 문서를 기능 단위로 커밋할 수 있으나, 이 worktree에는 전달받은 기존 미커밋 변경도 있으므로 전체 add를 하지 않고 범위를 선별해야 한다. 권장 메시지: Add deterministic sight and equipment combat rules. 커밋 실행은 별도 승인 후에만 한다.

최종 [완료 감사](2026-11-17-combat-completion-audit.json): 세 실행 각각395개 입력이 현재 파일과 일치, 원본385개 중 변경은 검사 manifest1개뿐, 인계10파일 hash 일치, 문서9개 링크 누락0. Windows build 오류0/경고0.
