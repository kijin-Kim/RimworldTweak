# 건설·재배 병렬 실행 카드

2026-11-15. 사용자 승인: 검증된 G0-C 인계 후 건설 B1~B3와 재배 F1~F2 구현/통합/검증. 조리/작업계획/파일 저장은 다음 단계이며 이번에 시작하지 않는다. 커밋/원격/push 금지.

| 담당 | 실제 작업 공간 | 소유권 |
|---|---|---|
| S0 통합 | E:\Dev\RimworldTweak | 기존 공통 파일/문서/Unity 실행. 추가 WorldState.Production.cs 및 .meta, SettlementView.Production.cs 및 .meta를 S0 소유로 등록 |
| SA 건설 | C:\Users\kijin\.codex\worktrees\rimworld-feature-a\RimworldTweak | 기존 소유권 표의 ConstructionState/Rules/Panel/Tests/IntegrationTests와 .meta, FeatureA 지시서 |
| SB 재배 | C:\Users\kijin\.codex\worktrees\rimworld-feature-b\RimworldTweak | 기존 소유권 표의 FarmingState/Rules/Panel/Tests/IntegrationTests와 .meta, FeatureB 지시서 |

기준 HEAD d0069af439e0bccfd7d99f56bdd464965991ab12, 계약 settlement-five-v2, G0-C-1 제품156개 집계4fdc276a370d7f0a565feb37f1fcf330a9816e0e166621d4734199aa7de39932. 두 detached worktree에 제품/문서/manifest212파일 복사 후 바이트 일치 확인. AGENTS.md는 이후 사용자 수정이 있어 과거 문서 해시 대신 현재본을 양쪽에 동기화했으며 제품 해시는 불변이다. 기준문서 manifest는 과거 G0-C 증거로 보존한다.

이번 요청의 세 세션은 통합 채팅 내 S0+SA/SB 하위 에이전트로 운영한다. 사이드바의 새 사용자 채팅은 생성하지 않는다. 기능별 전용 파일만 통합 폴더에 복사하고 공유 파일 수정은 S0이 수행한다. Unity는 S0 통합 폴더에서만 직렬 실행한다. 기능 작성 폴더의 독립 파일 수정과 통합 폴더 검사 동결을 구분한다.

진행: 상태/순수 규칙/패널 계약 확인 → SA 건설/SB 재배 병렬 작성 → S0 건설 연결 → 재배 연결 → 전체 검사/Windows/Player/렌더 → 결과 기록. 기능 세션은 Unity나 빌드를 실행하지 않는다.

## 합의한 확장과 인계

S0은 WorkReadContext.Options(후보/이유), ActiveWork(불변 Pawn/ActionId/대상/단계/진행/시간/필요량)와 FeatureWorkResult(진행·완료·소비/산출)를 추가했다. SA/SB GetCandidates/EvaluateStep은 읽기 전용 계산이며 실제 점유·진행·소비·산출은 S0이 적용한다. 새 개인 정책은 만들지 않고 기존 운반 우선 뒤 채집/건설/재배를 거리→요청순→ID로 선택한다. 건설 자재 반입은 일반 운반과 구분하며 저장된 목재도 출발원으로 사용한다.

건설 상태: BlueprintState/BuildingState, 실제 자재 DeliveredWood, 현장 Progress, 권한/이유, 철거 진행. 완공 시설은 새 ID이며 건설된 침대의 BedState는 시설 ID와 동일하다. 재배 상태: GrowingZoneState와 상시 ID CropState(빈 칸 포함), Empty/Growing/Mature, 파종/성장/수확 진행, PlantedTick/RequestOrder/권한/이유. 수확 뒤 새 요청순으로 재파종한다. 기능별 데이터·저장 대상은 각 지시서에 기재했으며 저장 구현은 하지 않았다.

화면 연결: 오른쪽 '건설 / 재배' 버튼으로 전용 패널 표시, '물류 보기'로 기존 패널 복귀. 건설 종류 버튼→한 칸 성공 시 도구 해제. 재배 두 모서리 성공 시 해제, 실패 시 첫 모서리 초기화. 우클릭/Esc는 도구만 취소한다. 실제 지도 표시는 S0, 버튼/상태 패널은 SA/SB 소유다.

SA 제출: 건설 상태/규칙/패널 + EditMode16 + PlayMode4, 각.meta 및 FeatureA 기록. SB 제출: 재배 상태/규칙/패널 + EditMode10 + PlayMode4, 각.meta 및 FeatureB 기록. 지정한 파일만 복사했으며 feature worktree 전체 diff는 복사하지 않았다. 공유 접점 변경은 S0만 수행했다.

첫 통합 검사 20261115T135335900Z-3d8daebe: 110개 중109통과. 실패는 목재 없음 시험에 수량0 스택을 넣어 기존 시나리오 입력 검사가 거부한 사례이며 SA가 빈 배열로 시험조건을 고쳤다. 제품의 수량 유효성 검사는 유지했다. 추가 순수규칙 검사 후 다시 동결/검증한다.

마지막 통합 표시 수정: 지도작물 성장률 간결화, 현장목재를 재고총량에 포함. SA가 실제반입 뒤 현장2/총량20 UI검사1개를 추가해 인계했다.

## 최종 통합 기준 재인계

검증기준 construction-farming-1, EditMode112/PlayMode44, Windows/Player/렌더 통과. [최종 manifest](../../Verification/2026-11-15-construction-farming-inputs.json)를 따라 제품 및 최신문서를 양쪽 작업공간에 복사하고 바이트/해시를 대조한다. 신규기능전용파일은 각소유자제출본이며 공유파일은S0본이다. 다음 기능은 착수하지 않고 검증된미커밋상태로 유지한다. 실제실행은통합폴더기준이며 feature worktree에서Unity를 별도로 실행한 것으로 보고하지 않는다.

재인계 실행 결과: 두 worktree 각각 제품180개와 문서/manifest를 포함한239파일을 동기화했고 모든 바이트 및180개제품SHA-256 일치 확인. `TestResults/construction-farming-handoff.json`에 복사결과를 남겼다. HEAD는세폴더모두d0069af이고 새커밋은없다.
