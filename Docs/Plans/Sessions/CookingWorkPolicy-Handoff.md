# 조리·작업계획 병렬 실행 카드

2026-11-15. 사용자 두 수정 정상 확인 및 다음 단계 구현 승인.
기준 player-control-construction-ui-1 / HEAD d0069af, 제품184개 SHA-256을 S0/SA/SB 모두 대조했다.

- S0: E:/Dev/RimworldTweak, 공통 계약/World/Runtime/View/manifest 및 Unity 직렬 검증.
- SA: C:/Users/kijin/.codex/worktrees/rimworld-feature-a/RimworldTweak, CookingState/Rules/Panel/Tests/IntegrationTests와.meta, FeatureA 기록.
- SB: C:/Users/kijin/.codex/worktrees/rimworld-feature-b/RimworldTweak, WorkPolicy/Diagnostics/Panel/Tests/IntegrationTests와.meta, FeatureB 기록.

기존 SettlementFiveFeatures 및 G0-Preparation의 수치/규칙을 유지한다. 공유 파일은 S0만 수정한다. S0 신규 WorldState.Cooking.cs 및 SettlementView.Planning.cs와.meta를 공유 연결용 소유 파일로 등록한다. 기능 담당은 공통 접점 제안을 먼저 인계하고 순수규칙/상태/패널/검사를 작성한다. 실제 상태 변경과 Unity 실행은 S0에 한정한다.

범위: 실제 건설된 조리대에 수확 식재료 반입→조리→식사 출력/섭취, 목표/출력제약, 개인 작업 종류별0~3설정과 모든후보배정 및 진단. 저장파일 구현은 제외한다. 커밋/원격/push/ComputerUse 없음.

검증: 기존114 EditMode/47 PlayMode를 유지하고 조리 보존·독점·목표경쟁·포화복구 및 개인정책/즉시취소/현재행동유지 검사를 추가한다. Windows 빌드/Player/렌더 및 사람조작은 구분한다. 결과는 추후 실제 실행 후 기록한다.

## 연결 API와 검토 반영

- `World/Runner.SetCookingOrder(stoveId, enabled, targetMeals)`, `DeleteCookingOrder(stoveId)`, `SetWorkPriority(pawnId, category, priority)`는 Tick 없이 즉시 설정/취소를 적용한다.
- World는 CookingOrders/CookingOutputs/WorkPolicies 읽기 목록과 조리 투입/소비/생산 장부를 공개한다. WorkReadContext.Policy는 복사본이며 Seal 이후 변경 불가다.
- S0 `GetWorkDiagnostic(category, targetId?)`는 경로·점유·정책을 읽어 진단한다. 화면 갱신은 경로 검색 계수·Tick·점유를 변경하지 않는다.
- 주문 재생성 후에도 회차 할당/유지는 stove ID 순서다. 구조상 접근 불가 또는 해당 작업을 켠 Pawn이 없는 시설은 새 회차를 선점하지 않는다. 진행/입력은 보존한다.
- 같은 출력 스택을 재사용해도 산출 Tick을 갱신하여 일반 운반은 다음 Tick부터 허용한다. 스택 단위 점유와 일치하도록 기존 잔량도 그 Tick의 새 운반에서는 함께 제외한다.
- 조리대 선택 시 생산 패널은 처음만 자동으로 열고, 사용자가 물류 보기로 바꾸면 같은 선택에서 이를 유지한다.

초기 통합 검사: EditMode140/PlayMode53 통과. 교차 검토 보완과 확장 검사는 진행 중이며 최종 결과는 별도 검증 기록으로 남긴다.

## 최종 인계

조리16 EditMode/4 PlayMode, 작업계획14/4를 기존114/47에 추가해144/55 통과. 최종 Windows 빌드/Player/PNG 및 입력 동결을 확인했다. [검증 기록](../../Verification/2026-11-15-cooking-work-policy.md), [파일 해시 manifest](../../Verification/2026-11-15-cooking-work-policy-inputs.json), [교차 검토](../../Reviews/2026-11-15-cooking-work-policy-review.md)를 기준으로 이어간다. 다음은 사용자 직접 조작 확인이며 저장/불러오기 착수와 커밋은 별도 승인 범위다.
