# 전투 독립 규칙 구현과 완료 기준

2026-11-17 사용자 승인: UI·세이브 작업과 병렬로 사선·엄폐 및 무기·방어구 규칙을 구현하고 완료 기준까지 검증한다. 실제 사격 입력, 공격 진행, 건강·부상, 장비 저장과 UI 연결은 후속 범위다. 기존 전투 전체 계획의 G1 완료를 뜻하지 않는다.

## 1. 핵심 설계

WorldState나 ColonistState를 변경하지 않는 순수 규칙을 만든다. UI·세이브가 현재 상태·월드 교체를 계속 소유하도록 하기 위함이다. 기존 월드에 직접 연결하면 즉시 플레이할 수 있지만 저장/취소 수정이 겹치므로 이번에는 명시적 입력 자료를 사용한다. 실제 맵 어댑터와 공격 연결은 후속에 필요하다.

시험 수치는 밸런스 확정이 아니다. 직선거리 제곱으로 사거리 경계를 포함한다. 사선은 칸 중심 사이의 supercover로 모서리 접촉 칸도 검사하여 벽 모서리 관통을 막는다. 시작/끝 칸은 장애물 검사에서 제외한다. 중간 완전 차단은 공격 불가다. 부분 엄폐는 표적의 인접 8칸 중 실제 선이 통과하는 칸의 가장 큰 CoverPercent만 적용한다. 중간의 먼 부분 엄폐와 사수 쪽 엄폐는 적용하지 않는다. 엄폐는 명중 관련 정보이며 피해 감소로 사용하지 않는다. 같은 위치 공격은 거부한다.

공격 확률/난수/사수 능력은 이번 규칙 범위가 아니다. 이후 명중 판정이 이번 사선 결과를 소비한다. 방어구는 max(0, 기본 피해 - 고정 방어력)이며 원본 피해와 결과 자료형을 나눠 결과를 다시 입력으로 넣는 실수를 막는다. 원거리 2종과 근접 1종의 초기 시험 설정을 제공한다.

## 2. 공통 계약과 흐름

- A 소유: `SightRules.cs`, `SightRulesTests.cs` 및 각각 meta. namespace Simulation / Tests.EditMode.
- A API: `SightCell(bool blocksShot, int coverPercent)` (0..100), `SightRules.Evaluate(GridPosition source, GridPosition target, int maxRange, Func<GridPosition,SightCell> cellAt)` → `SightResult` (`CanAttack`, `Reason`, `CoverPercent`). `SightFailure`: None, SamePosition, OutOfRange, Blocked. 잘못된 range와 null callback은 예외.
- B 소유: `EquipmentRules.cs`, `EquipmentRulesTests.cs` 및 각각 meta.
- B API: `WeaponKind` Ranged/Melee; immutable `WeaponDefinition(string id, WeaponKind kind, int range, int damage, int aimTicks, int recoveryTicks)`; `ArmorDefinition(string id,int reduction)`; `DamageInput(int amount)`; `ArmorRules.Resolve(DamageInput input, ArmorDefinition armor)` → `DamageResult` (`OriginalDamage`, `AbsorbedDamage`, `RemainingDamage`). armor null은 미착용. `PrototypeEquipment.Rifle/Carbine/Knife/LightArmor`.
- B 검증: 빈 ID, enum 외 값, range<=0, damage<0, aimTicks<0, recoveryTicks<=0, reduction<0 거부. 피해 0 허용. 자료는 외부에서 변경 불가.
- S0 소유: 본 계획, `CombatRulesIntegrationTests.cs`와 meta, 검사 manifest, 최종 검증/상태 문서. 공통 기존 제품 파일·Scene·설정은 변경하지 않는다.
- 호출 흐름: 무기 사거리 → 사선 결과 → 공격 불가면 피해 계산 생략 → 가능하면 무기의 기본 피해로 방어구 계산. 엄폐가 곧 명중 성공이라는 의미는 아니다. 이는 명중을 가정한 규칙 연결 시험이다.

## 3. 작업 공간과 통합

기준 소스: rimworld-feature-b의 미커밋/미추적 소스 포함. 세 worktree는 HEAD d0069af에서 만들고 Assets/Packages/ProjectSettings/Tools/Docs와 루트 지침을 전달했다. 제품·검사 입력 385개 SHA256 일치 확인: [manifest](../Verification/2026-11-17-combat-source.json).

- 통합: C:/Users/kijin/.codex/worktrees/rimworld-combat-integration/RimworldTweak
- A: C:/Users/kijin/.codex/worktrees/rimworld-combat-sight/RimworldTweak
- B: C:/Users/kijin/.codex/worktrees/rimworld-combat-equipment/RimworldTweak

A/B는 전용 파일만 작성하고 Unity를 실행하지 않는다. 인계 파일·hash 확인 후 S0에 복사한다. 같은 프로젝트의 Unity는 경로와 무관하게 단일 실행 큐를 유지한다. 기존 UI 검사가 끝난 것을 프로세스로 확인하고 실행한다. 통합 입력은 검사 중 동결한다. 기존 사용 중 에디터 강제 종료/잠금 삭제, 커밋/push는 하지 않는다.

## 4. 완료 기준과 검사

| 기준 | 증거 |
|---|---|
| F1 사거리 안/밖/정확한 경계, 축/대각선, 동일 위치 및 잘못된 입력 | EditMode 규칙 테스트 |
| F2 벽/모서리 완전 차단, 역방향 동일 차단, 표적 앞 부분 엄폐/측면 노출/먼 엄폐 제외 | EditMode 규칙 테스트 |
| F3 무기 3종, 잘못된 설정 거부, 미착용/부분/완전 방어, 0피해, 큰 값에서 overflow 없음 | EditMode 규칙 테스트 |
| F4 실제 두 모듈 연결, 차단 시 피해 없음, 엄폐를 피해 감소로 중복 적용하지 않음, 동일 입력 반복 결과 | 통합 EditMode 테스트 |
| F5 기존 이동/생활/작업/저장/UI 동작 보존 | 기존 전체 EditMode 및 PlayMode, 입력 hash 불변 |
| F6 배포 코드 컴파일, 설명/검증/상태 문서 일치 | Windows build, 링크/파일/완료 기준 대조 |

순수 규칙이므로 화면·조작 변화는 없으며 이번 완료에 사람 플레이를 요구하지 않는다. 실제 사격·명중·부상·저장 연결의 성공으로 보고하지 않는다. 실패 검사/미실행 기준이 남으면 미완료로 기록한다.

## 최종 요약

공통 자료 고정 → A/B 독립 작성 → 상호 검토 → S0 규칙 연결 → 직렬 자동검사/빌드 → 완료 증거 기록. 승인된 독립 규칙 구현 완료. EditMode212/212, PlayMode103/103, Windows build 성공/입력 변경0. [완료 기준별 증거](../Verification/2026-11-17-combat-rules.md)를 따른다. 실제 게임 연결은 후속 범위다.

## 구체 시험 설정과 호출자 조건
Rifle: range12/damage20/aim20/recovery30, Carbine: 8/12/10/15, Knife: 1/8/0/10, LightArmor: reduction5. 설정값은 최종 밸런스가 아니다. cellAt은 질의 중 동일한 읽기 상태를 제공하고 맵 경계 처리는 호출자가 정한다. 사선 길이에 비례한 조회를 사용하며 이번 최대 시험 사거리는12다. 신규 명중 확률·실제 지도 연결은 없다. [교차 검토](../Reviews/2026-11-17-combat-rules-review.md).
