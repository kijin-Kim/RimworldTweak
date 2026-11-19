# 현재 범위: 다섯 기능의 3세션 개발 계획

2026-11-15. 제품 기준 `d0069af`. 현재 범위는 **건설·재배·조리·작업계획·저장/불러오기**다. 작업계획은 정착민별 작업 우선순위·끄기·자동 배정을 뜻하며 일정표/시간표는 아니다. 저장은 기존 저장 구역 기능이 아니라 게임 상태의 파일 저장과 복원이다.

이 문서는 현재 범위의 실행 기준이며 [전체 로드맵](MultiSessionRoadmap.md)보다 우선한다. 현재 요청은 G0-C 인계 후 건설 B1~B3와 재배 F1~F2 병렬 구현/통합/검증이다. [G0 준비 확정서](G0-Preparation.md)를 함께 따른다. 세 역할과 기능별 작업 공간을 사용한다. 조리/작업계획/저장과 스테이징·커밋은 이번 범위에 포함하지 않는다. 기존 미커밋 계획 문서는 유지한다.

## 범위와 끝나는 지점

현재 사용 가능한 이동·생활·소집·단일 점유·채집·운반·저장 구역·식별 UI·정지 중 즉시 반영은 재사용하고 회귀 검사한다. 신규 다섯 기능의 전용 UI·오류 이유·검사도 범위에 포함한다.

마지막 확인 장면은 '자재로 조리대/침대를 건설 → 작물 재배/수확 → 우선순위에 따라 운반·조리·섭취 → 도중 저장/불러오기 후 계속 진행'이다. 시설 철거와 취소 반환은 기존 건설 기획의 최소 범위에 포함한다.

전투·치료·Fog of War·습격·최종 데모, 연구/전력/온도/부패/계절, 일정표, 자동저장/클라우드/여러 슬롯 관리, 장기 성능 최적화는 이번 범위가 아니다. 작업8의 '초기 식사 소진 뒤 두 재배 주기 자급' 전체 승인은 별도 단계로 남긴다. 이번에도 한 생산 순환·중단 복구·저장 후 재개는 반드시 검증하되 전체 자급/밸런스 완성을 주장하지 않는다.

## 정확히 세 세션의 역할

| 세션 | 1차 | 2차 | 3차 | 계속 맡는 책임 |
|---|---|---|---|---|
| S0 통합 | 공통 접점·건설/재배 연결 | 조리/작업계획 연결 | 저장 연결·최종 검증 | 공유 파일·계약·실제 Unity 실행·전체 상태·소유권 변경 |
| SA 기능A | 건설 | 조리 | 저장/불러오기 | 담당 기능의 규칙·UI·단위/통합 검사·인계 |
| SB 기능B | 재배 | 작업계획 | 다섯 기능 연결 검사 | 담당 기능의 규칙·UI·단위/통합 검사·인계 |

[통합 지시서](Sessions/Integration.md), [기능A 지시서](Sessions/FeatureA.md), [기능B 지시서](Sessions/FeatureB.md)를 사용한다. BUILD/FARM 등은 더 이상 별도 동시 세션이 아니라 이 세션 내부의 기능 이름이다. QA도 네 번째 세션을 만들지 않고 3차 SB가 맡는다. 역할이 바뀌어도 기존 기능 파일의 소유권은 자동 이전되지 않는다.

## 준비와 병렬 순서

| 단계 | 선행 | SA | SB | S0의 통합 순서와 통과 지점 |
|---|---|---|---|---|
| G0-D 명세 준비 | 현재 요청 범위 | 건설 인계 명세 | 재배 인계 명세 | [준비 확정서](G0-Preparation.md)에 공간/서명/수치/조작/저장 규칙 확정. 문서 완료, 코드 없음 |
| G0-C 연결 검증 | 별도 구현 요청 | 계약/허용 파일 읽기 | 계약/허용 파일 읽기 | S0 최소 공통 연결 구현→실제 회귀 검사→[인계 기준](../Verification/2026-11-15-g0-c.md) |
| 1차 | G0-C 통과 | B1 지정 → B2 자재 → B3 완공/철거 | F1 구역/파종 → F2 성장/수확/재파종 | B1/B2/B3 순으로 건설을 연결(G1), F1/F2를 연결(G2). 독립 재배 검사는 건설 완료 전 가능 |
| 2차 | G1/G2 실제 연결과 갱신 기준 인계 | C1 조리 한 회 → C2 목표/출력 제어 | J1 개인 설정 → J2 자동 배정/진단 | C1/J1 개별 검사는 병렬. C2를 실제 수확/조리대에 연결(G3) → J2로 모든 작업 배정(G4) |
| 3차 | G4, 저장 항목표 확정 | S1 저장 왕복 → S2 진행 복원 | I1 실제 연결 → I2 중단/복원 회귀 | S1 뒤 S2 연결 → I1/I2 최종 통합(G5) |

이 문서의 G0~G5는 현재 다섯 기능용 통과 지점이며 전체 로드맵의 G4~G12와 번호 의미가 다르다. 현재 작업에서는 이 문서의 번호만 사용한다.

G1은 건설, G2는 재배, G3은 조리, G4는 작업계획, G5는 저장을 포함한 현재 전체 완료 지점이다. 각 지점에서 검사·실행·사용자 확인 상태를 따로 기록한다. '코드 작성 완료'는 이 지점의 통과가 아니다.

다른 담당을 기다릴 때에는 다음 기능의 입력/조회 요구·검사 사례·저장 항목표를 준비할 수 있다. 추가 세션을 열거나 미정 접점을 추측해 구현하지 않는다. 시험용 조리대/자재로 작성한 개별 검사와 실제 건설/수확 연결 검사는 구분한다. 빨리 끝난 세션이 다른 소유 파일을 임의 수정하지 않는다.

## 공통 파일 소유권

경로는 저장소 기준이다. 건설/재배 파일은 이번에 구현하며 나머지 기능 파일은 예정 상태다. 기존 파일을 다른 세션이 맡으려면 S0이 대상·기간·변경 이유를 인계 기록에 먼저 명시해야 한다.

| 소유자 | 파일 범위 |
|---|---|
| S0 | 기존 `Assets/Game/Simulation/WorldState.cs`, `WorldState.Living.cs`, `WorldState.Work.cs`, `ColonistState.cs`, `InitialEntities.cs`, `LivingEntities.cs`, `WorkEntities.cs`, `ScenarioDefinition.cs`, `SimulationSession.cs`, GridPosition/오류 형식/기존 시나리오 |
| S0 | 기존 `Assets/Game/Runtime/SimulationRunner.cs`, `Assets/Game/Presentation/SettlementView.cs`, `.Work.cs`, `.Inspection.cs`, `BoardPointerHandler.cs`, `WorldMarker.cs`, `MapSymbolGraphic.cs` |
| S0 | `Assets/Game/Demo/`, `Editor/`, `Scenes/`, asmdef와 공유/부모폴더 .meta, Packages/ProjectSettings, `Tools/` 및 검사 manifest, 기존 공통 회귀 검사, 아래에 배정하지 않은 모든 기존 파일 |
| S0 | `Assets/Game/Simulation/FeatureWorkContracts.cs`, `Assets/Game/Simulation/WorldState.Features.cs` 및 .meta (G0-C 소유) |
| S0 | README, Status/Decisions/Roadmap/ParallelWork/Architecture/Testing/Workflow, 본 계획·공통 계약·통합 지시서·통합 검증 보고서 |
| SA | 아래 건설/조리/저장 전용 파일과 자신의 지시서·인계 기록 |
| SB | 아래 재배/작업계획/연결 검사 전용 파일과 자신의 지시서·인계 기록 |

다음 표에서 Simulation/Presentation/Runtime은 `Assets/Game/` 아래, EditMode는 `Assets/Game/Tests/EditMode/Simulation/`, PlayMode는 `Assets/Game/Tests/PlayMode/Integration/` 아래다. 원본과 파일 .meta는 같은 소유자다. 새 폴더 .meta는 S0이 배정하며 GUID를 재생성하지 않는다.

| 기능/소유자 | 예정 제품 파일 | 예정 검사 파일 | 기능 규칙/진행 기록 |
|---|---|---|---|
| 건설 SA | Simulation의 `ConstructionState.cs`, `ConstructionRules.cs`; Presentation의 `ConstructionPanel.cs` | EditMode `ConstructionTests.cs`; PlayMode `ConstructionIntegrationTests.cs` | [기능A 지시서](Sessions/FeatureA.md)의 건설 부분 |
| 재배 SB | Simulation의 `FarmingState.cs`, `FarmingRules.cs`; Presentation의 `FarmingPanel.cs` | EditMode `FarmingTests.cs`; PlayMode `FarmingIntegrationTests.cs` | [기능B 지시서](Sessions/FeatureB.md)의 재배 부분 |
| 조리 SA | Simulation의 `CookingState.cs`, `CookingRules.cs`; Presentation의 `CookingPanel.cs` | EditMode `CookingTests.cs`; PlayMode `CookingIntegrationTests.cs` | 기능A 지시서의 조리 부분 |
| 작업계획 SB | Simulation의 `WorkPolicy.cs`, `WorkDiagnostics.cs`; Presentation의 `WorkPolicyPanel.cs` | EditMode `WorkPolicyTests.cs`; PlayMode `WorkPolicyIntegrationTests.cs` | 기능B 지시서의 작업계획 부분 |
| 저장 SA | Simulation의 `SaveData.cs`, `SaveValidation.cs`; Runtime의 `SaveFileService.cs`; Presentation의 `SaveLoadPanel.cs` | EditMode `SaveTests.cs`; PlayMode `SaveIntegrationTests.cs` | 기능A 지시서의 저장 부분 |
| 연결 검사 SB | 제품 파일 추가 없음 | EditMode `FiveFeatureFlowTests.cs`; PlayMode `FiveFeatureFlowIntegrationTests.cs` | 기능B 지시서의 연결 검사 부분 |

파일 분리는 기능 모듈 간 상태 변경 권한까지 분리해야 의미가 있다. 공유 World 내부 필드를 다른 partial 파일에서 직접 변경하는 방식으로 소유권을 우회하지 않는다. 신규 접점 파일이 꼭 필요하면 S0이 본 표에 경로를 등록한 뒤 추가한다. 전용 UI는 패널을 제공하고 S0이 기존 화면에 연결한다.

## 검증·인계·합치는 절차

1. S0이 작업 카드에 실제 기준 커밋/제품 입력 해시·계약 버전·작업 경로·허용 파일·결과 단위를 적는다. 초기 공통 기준은 `d0069af`와 현재 미커밋 계획 문서이며, 계획 문서 자체를 제품 커밋에 포함된 것으로 착각하지 않는다.
2. 기능 담당은 한 결과 단위의 코드·UI·검사·변경 목록과 필요한 공유 연결을 제출한다. 시작/중단/진행도/실패 이유/저장할 상태가 드러나야 한다.
3. S0이 연결과 계약을 검토하고 실제 검사 대상의 쓰기를 동결한다. Unity import/컴파일/EditMode/PlayMode/빌드/Player는 S0만 직렬 실행한다. 사용 중 에디터를 강제 종료하거나 잠금을 삭제하지 않는다.
4. 단위별 관련 검사, G 경계별 전체 회귀·Windows 빌드·Player·렌더를 수행한다. 기존 이동/생활/R/점유/물류/정지 UI/도구 종료 검사를 유지한다. 실행하지 않은 항목은 미검증으로 적는다.
5. 성공하면 최신 통합 기준을 두 기능 세션에 인계한다. 기능 브랜치의 테스트 성공만으로 합친 상태 성공을 선언하지 않는다. 실패한 변경은 다음 기준에 섞지 않고 소유자에게 되돌려 수정한다.
6. 다른 소유자의 결함은 위치·재현·예상 결과를 전달한다. 특히 SB의 연결 검사에서 건설/조리/저장 결함을 찾아도 SA가 고치고 S0이 통합한다. 검증 담당이 검사 기대를 바꿔 실패를 감추지 않는다.
7. 검증된 결과 단위의 코드·검사·문서를 함께 커밋 제안한다. 이번 요청은 커밋 승인이 아니며 임의 커밋/merge/cherry-pick은 하지 않는다. 원격 연결·push도 금지다.

작업 공간 운영은 S0 기존 폴더 유지+SA/SB 별도managed worktree로 확정했다. 실제 생성은 기능 착수 때 수행하며 G0-D에서는 기존 폴더만 사용했다. 생성 시 기준/최신 미커밋 문서를 인계하는 방법과 동결·캐시 규칙은 [G0 준비 확정서](G0-Preparation.md)를 따른다. 새로운 폴더를 만들었다고 미커밋 계약까지 복사됐다고 가정하지 않는다.

## 현재 전체 완료 기준 G5

- 실제 건설 자재로 완공한 침대 사용 및 조리대 이용. 수확한 식재료가 운반·조리·섭취로 이어진다. 초기 시험용 시설/재료만으로 대체하지 않는다.
- 작업 끄기와 1~3 우선순위가 실제 작업 선택과 정지 중 UI에 반영된다. 소집은 모든 자동 작업에 우선하고, 유효한 작업을 가까운 후보 출현만으로 반복 중단하지 않는다.
- 건설 취소/철거, 재배 중지, 조리 보류, 생활 중단, R, 통로 차단, 출력 포화 후 점유·물자 보존과 원인 해소 후 재개를 확인한다.
- 반입/건설/성장/조리/운반 도중 저장하고 불러온 뒤 수량·진행·설정·소집 상태를 보존하며 정상 재개한다. 잘못된 파일은 현재 월드를 훼손하지 않는다.
- 초기 식사가 남아 있어도 검사는 가능하지만 최소 한 번은 생산한 식사의 소비를 추적해 실제 연결을 증명한다. 전체 자급2주기/64×64 성능 검증은 이번 완료 조건으로 확대하지 않는다.
- 자동검사와 빌드·Player·PNG는 에이전트가 실행 가능하다. 실제 마우스/키보드 조작감과 가독성은 사용자 확인으로 분리한다. 미확인 상태를 완료로 꾸미지 않는다.

## 작업 카드

각 지시서 끝의 진행란을 이어 쓴다. 전체 Status는 S0만 갱신한다. 세션 대화가 자동 전달된다고 가정하지 않는다.

```text
세션/단계/인계 단위:
실제 작업 폴더/기준 커밋/제품 입력 해시/계약 버전:
허용 파일/공유 연결 요청:
완료 기준/제외 범위:
상태: 계획 | 작성 중 | 인계 대기 | 개별 검증 | 통합 검증 | 사용자 확인
변경 목록/완료 내용/다음 행동:
검사 명령·실행 폴더·결과·미검증:
막힌 접점/영향 세션/소유권 인계:
커밋 허용 범위: 없음(별도 승인 시 갱신)
```

건설/재배와 소집 전 이동·건설 표시 수정은 사용자 정상 확인을 받았다. 조리/작업계획의 통합 자동 검증은 [최신 검증 기록](../Verification/2026-11-15-cooking-work-policy.md)을 따른다. 저장/불러오기도 후속 승인으로 통합 자동 검증을 마쳤으며 [저장 검증](../Verification/2026-11-16-save-load.md)을 따른다. 다섯 기능의 자동 연결 검증과 사람 직접 확인은 구분하며 저장의 사람 확인은 대기다.

G0-C 추가 S0 소유: `Assets/Game/Tests/EditMode/Simulation/FeatureWorkContractTests.cs`와 .meta, `Tools/expected-editmode-tests.json`.

이번 병렬 실행의 실제 경로/소유권 확장/인계 기록은 [실행 카드](Sessions/ConstructionFarming-Handoff.md)를 따른다. S0의 WorldState.Production.cs 및 SettlementView.Production.cs와 .meta를 소유권에 추가한다.
