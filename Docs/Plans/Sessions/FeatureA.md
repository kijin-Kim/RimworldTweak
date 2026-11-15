# SA 기능A 세션 작업 지시서

상태: **건설 B1~B3 통합 자동검증 완료, 사람의 직접 조작 확인 대기.** 후속 승인으로 조리 C1~C2를 작성해 통합 인계 중이다. 후속 사용자 승인으로 저장/불러오기 S1~S2 전용 DTO·검증·파일서비스·패널·검사를 작성해 S0 통합 인계 중이다. 순서는 **건설 → 조리 → 저장/불러오기**다. 기준 HEAD `d0069af` 위 미커밋 변경이며 최신 입력·실행 결과는 [건설·재배 검증 기록](../../Verification/2026-11-15-construction-farming.md)을 따른다.

## 읽을 문서와 경계

[현재 상태](../../Status.md), [3세션 계획/파일 소유권](../SettlementFiveFeatures.md), [현재 공통 계약](../../Contracts/SettlementFiveFeatures.md), [통합 지시서](Integration.md)를 읽는다. 작업계획은 개인 우선순위/끄기/자동 배정이며 SB 소유다. 재배도 SB 소유다.

수정 허용 파일은 계획 표의 ConstructionState/Rules/Panel/Tests/IntegrationTests, CookingState/Rules/Panel/Tests/IntegrationTests, SaveData/Validation/FileService/LoadPanel/Tests/IntegrationTests 및 각 .meta, 본 문서다. 실제 이름은 표의 정확한 경로를 따른다. 아직 존재하지 않는 예정 파일이며 S0의 배정 없이 다른 파일을 추가하지 않는다. WorldState, SimulationRunner, 기존 SettlementView, 공유 설정/시나리오/검사 manifest는 직접 수정하지 않는다.

## 1차 건설: B1 → B2 → B3

선행: G0-C 검증된 접점/수치/조작표 수신. 산출물은 전용 규칙·패널·검사와 공통 연결 요청이다.

| 단위 | 작업 지시 | 완료를 증명할 검사 |
|---|---|---|
| B1 지정 | 벽/문/침대/조리대 설계도 지정·취소, 비용/대기 이유 표시 | 정지 상태에서 지정/취소 및 UI 즉시 반영, 겹침/맵 밖/부적합 타일 거부, 자재 임의 생성 없음 |
| B2 반입 | 기존 스택 독점/운반 경로로 필요한 수량만 현장에 반입 | 두 Pawn 경쟁, 비용 초과 반입 없음, 이동 중 R/생활 중단·반입 후 취소의 총량 보존 |
| B3 완공/철거 | 실제 반입·진행 확인 후 시설 생성, 침대/조리대 조회 제공, 벽/문 통행 연결 요청 | 경계 Tick 전/후 비용 한 번 소비·시설 한 개 생성, 취소는 반입량만 반환, 철거 한 번 반환, 새 침대 사용·경로 변경 |

현장 진행은 담당자의 소집/생활 중단 뒤 유지한다. 점유 중 철거/벽 완공 예외는 S0의 G0 정책을 사용한다. 통행/BedState를 전용 모듈에서 직접 바꾸지 않고 적용 요청을 보낸다. B1/B2/B3을 각각 인계하고 G1 통과 뒤 조리 단계로 넘어간다.

## 2차 조리: C1 → C2

선행: G1/G2 실제 조리대/수확물 연결, 작업대 조회/출력/생산계수 계약. 시험용 조리대 검사만으로 G3를 완료 처리하지 않는다.

| 단위 | 작업 지시 | 완료를 증명할 검사 |
|---|---|---|
| C1 한 회 | 식재료 반입→작업대 진행→식사 출력, 재료/진행/점유/중단 이유 표시 | 식재료2→식사1 한 번, 두 Pawn 독점, R/생활 중단 후 투입·진행 유지, 담당 교체 후 중복 소비 없음 |
| C2 반복 | 목표12 및 변경/끄기, 현재+진행 생산을 반영한 회차 시작, 출력 포화 보류 | 두 작업대 목표 경쟁, 목표 하향/끄기 즉시 상태 표시, 재료 부족/출력 포화 해소 후 재개, 실제 수확물의 식사 생산·섭취 |

작업계획을 직접 구현하지 않는다. SB에 조리 후보/불가 이유/중단 가능 상태를 제공하고 S0에게 정책 연결을 요청한다. 조리 재료와 출력의 수량 집계/저장 필드 목록도 인계한다. G3/G4 통과 뒤 저장으로 넘어간다.

## 3차 저장: S1 → S2

선행: G4, S0/SB와 필수 저장 목록/경로/포맷버전/복원 정책 확정. 기존 저장 구역 기능 개발과 혼동하지 않는다. 한 슬롯 수동 저장/불러오기에 한정한다.

| 단위 | 작업 지시 | 완료를 증명할 검사 |
|---|---|---|
| S1 왕복 | 순수 저장 데이터/검증, Runtime 파일 I/O, 저장/불러오기 버튼과 오류 안내 | 정상 왕복에서 Tick/욕구/ID/설정/물자 일치, 잘못된 버전/손상/쓰기 실패에서 기존 파일/월드 보존 |
| S2 진행 복원 | 현장/작업대/작물·운반·정책까지 복원, 경로/점유 검증 요청, 실제 월드 교체는 S0 | 반입·건설·성장·조리·운반 각 단계 저장 후 정상 재개, 정지/소집 보존, 수량/산출 한 번, 다음ID 충돌 없음 |

Simulation에 파일 접근이나 Unity 참조를 넣지 않는다. 읽기 실패 중 기존 월드를 조금씩 수정하지 않는다. 복원 중 권한을 모두 버리고 진행/재료를 잃게 하는 방식은 금지다. SB의 연결 검사가 재현한 SA 소유 결함은 SA가 수정한다. 저장 구현과 건설/조리 수정이 동시에 필요하면 S0과 순서를 정한다.

## 인계·검사 요청

단위마다 변경 파일, 실제 기준/계약 버전, 공개 읽기 상태, 공유 연결 요청, 저장 필드, 검사 이름/입력/기대 결과, 미검증과 다음 행동을 제출한다. 검사는 작성하되 실제 Unity 실행은 S0에 요청한다. 검사 중 대상 폴더를 수정하지 않는다. 직접 검증하지 않은 결과를 통과로 적지 않는다. SB 파일의 오류는 재현 정보로 요청하고 임의 수정하지 않는다.

현재 기록: B1~B3은 전용 상태·규칙·패널과 S0의 World/화면 연결을 통합해 자동검증했다. 실제 목재로 벽·문·침대·조리대를 건설하고 건설 취소/철거·생활/R 중단·점유·정지 UI를 검사했다. 사람의 직접 플레이는 확인 대기다. 후속 승인된 C1~C2는 전용 구현/검사 작성 후 S0 통합 인계 중이고 S1~S2는 미착수다. 다음 행동은 S0 통합 실행에서 확인한 SA 소유 결함 수정과 조리 검증 결과 인계다. 저장은 별도 착수 지시를 따른다. 인계는 [작업 카드](../SettlementFiveFeatures.md)를 사용하며 커밋은 하지 않는다.

## G0-D 인계 명세

계약 `settlement-five-v2`, 제품 기준 `d0069af`. [준비 확정서](../G0-Preparation.md)의 실제 추가 서명·수치·시설/출력 위치·조작·저장 정책을 따른다. 이 문서는 API 코드가 존재한다는 증거가 아니다. G0-C가 실제 컴파일/회귀를 통과하고 S0이 인계하기 전에는 제품 파일을 생성하지 않는다. 현재 폴더/계약 문서 기준은 S0에 있고 기능별worktree는 아직 없다.

## G0-C 수신 기준

[G0-C 검증·인계](../../Verification/2026-11-15-g0-c.md)의 구현 범위와 입력 manifest를 먼저 대조한다. 계약 v2의 미래 기능 API는 아직 호출할 수 없다. 새 종류의 후보/진행/데이터 연결은 전용 규칙과 검사 결과를 S0에 제출해 확장하며 기존 World 파일을 직접 수정하지 않는다. 이번 요청은 기능 착수 승인이 아니다.

## SA 건설 1차 인계 (2026-11-15)

사용자의 건설/재배 병렬 착수 지시에 따라 B1/B2/B3 전용 상태·규칙·패널·검사를 작성했다. 조리·저장 기능은 착수하지 않았다.

- 실제 작업 경로: `C:\Users\kijin\.codex\worktrees\rimworld-feature-a\RimworldTweak`
- 기준 HEAD: `d0069af439e0bccfd7d99f56bdd464965991ab12`, G0-C 제품 입력156개 집계 SHA-256 `4fdc276a370d7f0a565feb37f1fcf330a9816e0e166621d4734199aa7de39932`, 계약 `settlement-five-v2`, 전달 기준 `G0-C-1`.
- 전달받은 제품/문서212파일은 S0이 파일별 동등성을 확인했다. 최신 AGENTS의 보고 규칙도 전달받았다.
- 허용/작성 파일: Simulation ConstructionState.cs, ConstructionRules.cs; Presentation ConstructionPanel.cs; EditMode ConstructionTests.cs; PlayMode ConstructionIntegrationTests.cs와 각.meta, 본 지시서.
- 공유 변경은 하지 않았다. World의 지정/취소·자재 반입·완공/철거·침대 등록·통행·실제 진행/점유와 View의 배치도구·보드·상세 선택은 S0 연결 대상이다.
- 상태/규칙: BlueprintState/BuildingState는 외부 읽기 전용이며 비용/기간은 G0 시험값. ConstructionRules는 후보 조회와 진행 결과만 반환하고 World를 변경하지 않는다. 조리대 건설은 시설만 제공하고 조리 기능은 아직 없다.
- 패널: Initialize(parent,font,beginPlacement,cancelBlueprint,toggleDemolition), Refresh(blueprint,building,error). 종류별 버튼, 취소/철거 버튼, 재료·진행·담당·보류이유 표시. 배치 성공 도구 해제와 정지 갱신은 S0 View에 연결한다.
- 검사 작성: EditMode16개, PlayMode4개. 네 시설의 실제 비용,10개 운반한도/경쟁, 자재 부족, 운반 중 취소, 시공 중 R, N Tick 경계, Pawn 위 벽 완공 보류,30Tick 철거/한 번 반환, 새 침대 수면과 철거 시 수면 해제, 일시정지 지정/취소 및 UI, 우클릭/Esc, Reset.
- 실행 상태: SA는 Unity/빌드/테스트를 실행하지 않았다. 작성된 검사와 실제 통합 검증을 구분한다. S0이 전체 입력 동결 후 통합 실행 결과를 최종 검증 문서에 기록한다.
- 커밋/원격/push/Computer Use 없음. 다음 행동은 S0 연결 검사에서 발견한 SA 소유 결함 수정 및 인계다.

추가 검사: 출발 스택 접근 중 R과 오래된 취소 무시, 반입 완료 후 설계도 취소의 실제 반입분 반환, 허기 임계치에서 시공 중단·식사 뒤 진행 재개를 작성했다.

S0 첫 통합 검사에서 목재0 시나리오의 기존 양수 자원 검증 위반을 확인했다. 기대 동작은 유지하고 자원 없는 배열로 fixture를 수정했다. 완료 진행 스냅샷 재평가가 소비/반환을 다시 만들지 않도록 순수 규칙 거부와 경계 검사를 추가했다. 재실행은 S0 담당이다.

## SA 건설 통합 결과 (2026-11-15)

S0 실행 결과를 수신했다. 건설16개를 포함한 EditMode112/112, PlayMode43/43과 Windows 빌드·Player를 통과했다. 실제4시설 건설에 목재31을 소비했고 재배와 연결한 Player에서 수확12/저장12를 확인했다. 렌더 이미지도 S0이 확인했다. SA는 Unity를 직접 실행하지 않았다.

마지막 지도 작물 라벨 수정은 S0 소유 변경이며 건설 전용 제품 변경은 없다. 최종 동일 입력 재검사·실행 ID·입력 manifest는 [검증 기록](../../Verification/2026-11-15-construction-farming.md)을 최종 기준으로 삼는다. 이 문서의 앞선 미실행 기록은 SA 최초 인계 당시 이력이다. 사람의 실제 마우스/키보드 확인은 자동 검사와 별도로 대기한다. 조리/파일 저장은 구현하지 않았고 커밋·원격 연결·push도 하지 않았다.

최종 재고 표시 검토: 미완공 현장에 반입한 목재2가 UI의 `현장 2` 및 초기 물자 보존 총량 `총량 20`에 포함되는 PlayMode 검사를 추가했다. 건설 PlayMode는 총4개다. 전체 재검사 결과는 [검증 기록](../../Verification/2026-11-15-construction-farming.md)을 따르며 실행 중인 전체44개를 통과로 기록하지 않는다. 이번 후속 인계는 문서만 변경했다.

## SA 조리 C1/C2 인계 (2026-11-15)

사용자가 조작 수정 정상 확인 후 조리/작업계획 병렬 구현을 승인했다는 S0 인계를 받고 조리를 작성했다. 기준은 `player-control-construction-ui-1`이며 작업 공간의 제품184개를 기준 manifest와 다시 대조해 불일치0을 확인했다. 기존 SA 경로와 소유권을 유지한다.

- 작성 파일: Simulation CookingState.cs/CookingRules.cs, Presentation CookingPanel.cs, EditMode CookingTests.cs, PlayMode CookingIntegrationTests.cs와 각.meta, 본 지시서.
- 상태: 주문은 조리대와 같은 작업 권한을 공유하고 출력은 별도 스택 권한을 공유한다. 초기 정지/목표12, 입력2, 출력50, 한 회40Tick에 식재료2→식사1이다. 규칙은 읽기 후보/진행 결과만 계산한다.
- S0 연결 요청: World/Runner SetCookingOrder(stoveId,enabled,targetMeals), DeleteCookingOrder(stoveId), World.CookingOrders/CookingOutputs, CookingConsumedIngredients/CookingProducedMeals. 실제 완공 조리대에 주문/출력 생성, 입력 반입/진행/완료/삭제/철거/목표 재평가와 기존 자원·음식·운반 연결은 S0 담당이다.
- 삭제된 주문은 SetCookingOrder로 새로 만들 수 있다. UI의 다시 만들기는 false/12를 요청한다. 목표는 현재 모든 식사와 활성 회차를 ID순 평가한다. 출력 스택은 Resources에 한 번 등록하고 StorageSlotId=null로 일반 운반이 가능하게 유지한다.
- 패널: Initialize(parent,font,setOrder,deleteOrder,createOrder), Refresh(order,output,mealTotal,error). 정지 중 가동/목표±1/삭제/재생성과 현재 투입·진행·출력·담당·보류 이유를 표시한다. S0은 View.CookingPanel과 선택한 조리대 ID를 연결한다.
- 검사 작성: EditMode16개, PlayMode4개. 실제 건설한 조리대,40Tick 경계, R/가동끄기/목표0/삭제/운반취소, 두 조리대 목표 경쟁, 출력50포화와 점유, 철거 반환, 생성식사 실제 소비, 실제 수확물 조리·식사 운반, 순수 규칙 중복 완료 거부, 정지 UI·Reset을 포함한다.
- SA는 Unity/import/검사/빌드를 실행하지 않았다. S0 통합 실행 전이므로 자동검증 완료로 표시하지 않는다. 저장 기능·공유 파일 변경·커밋·원격·Computer Use는 없다.

조리 연결 검토 후 추가 회귀: 낮은 조리대 주문의 삭제/재생성 뒤에도 시설ID순 회차 할당/유지, 빈 출력 스택 재사용 시 생산 다음Tick부터 운반, 접근 불가능한 낮은ID 조리대가 목표 회차를 독점하지 않고 접근 가능한 조리대가 생산하는 검사를 추가했다. 조리 EditMode는16개다. S0 검증 전에는 이 추가 검사의 통과를 주장하지 않는다.

후속 통합 검사에 따라 단일 식재료 스택 독점이 풀릴 때까지 두 활성 회차 전제를 기다리도록 순서 검사를 정정했다. 완료 식사0인 상태에서 두 회차 활성 확인은 유지한다. 선택한 조리대의 물류 보기로 닫은 패널이 반복 Refresh 후 다시 열리지 않는 PlayMode 회귀를 추가했다. 조리 전용 PlayMode는4개다.


2026-11-15 S0 최종 인계: 조리/정책 연결 후 전체 Edit144/Play55 및 Windows Player/PNG 자동 검증 완료. 최종 제품208개는 [manifest](../../Verification/2026-11-15-cooking-work-policy-inputs.json)와 [검증](../../Verification/2026-11-15-cooking-work-policy.md)을 따른다. 전용 작성 단계의 미검증 표시는 당시 기록이며 현재 전체 자동 결과는 이 인계가 우선한다. 사람 조작 확인·파일 저장·커밋은 완료하지 않았다.

## SA 저장 S1/S2 인계 (2026-11-16)

조리/작업계획까지 검증된 제품208개 기준을 유지하고 사용자의 후속 저장 구현 승인을 받았다. 전용 작업 공간은 기존 SA worktree다. 공통 캡처·복원 및 실제 Unity 실행은 S0 소유다.

- 작성 소유 파일: Simulation SaveData.cs/SaveValidation.cs, Runtime SaveFileService.cs, Presentation SaveLoadPanel.cs, EditMode SaveTests.cs 및 각.meta. 본 지시서 외 전역 문서는 변경하지 않았다.
- Newtonsoft는 manifest/lock/기존 코드에서 확인되지 않았다. S0 합의에 따라 새 패키지 없이 BCL DataContractJsonSerializer를 Runtime에서 사용한다. Simulation은 Unity/파일 I/O에 의존하지 않는다.
- DTO는 명시적인 DataContract/DataMember 필드다. 헤더는 camelCase, 내부 필드는 기존 PascalCase로 고정한다. enum은 정의된 문자열만 허용하고 long은 정수, PendingSeconds는 invariant decimal 문자열이다. 누락 필수 필드는 역직렬화에서 거부한다. 기존 readonly GridPosition 대신 X/Y를 갖는 PositionSaveData를 사용한다.
- NextEntityId는 실제 다음 할당ID이며 소진/삭제 기록을 포함한 모든 ID보다 커야 한다. constructed Bed와 Building의 동일ID만 허용한다. 원래 Reset 시나리오와 현재 월드/세션 상태를 따로 보존한다.
- SaveValidation은 후보 월드를 생성하지 않고 헤더/범위/중복ID/enum/참조/점유의 소유자·ActionId/공유 슬롯·스택/조리대·주문 권한/정책 목록을 검증한다. 복합 실행 상태·경로·점유 재구성과 원자적 Runner 교체는 S0이 담당한다.
- 파일 서비스는 instance Save/Load, 동시에 하나의 요청만 허용하는 process 공통 gate를 사용한다. 16MiB 제한, 엄격 UTF-8, 명시 DTO만 직렬화하며 타입 메타데이터는 출력하지 않는다. `.tmp` 작성·Flush(true)·닫기·재파싱/검증 후 File.Replace 또는 최초 File.Move를 사용한다. Replace 실패에 최종 파일 삭제/이동 대체를 하지 않는다. 파일 원문 예외는 UI에 전달하지 않는다.
- 저장 패널은 외부 배치 가능한 Root208×36과 저장 덮어쓰기/불러오기 버튼, 확인 시에만 보이는700×200 확인창이다. Build/Refresh 및 SaveButton/LoadButton/ConfirmButton/CancelButton/InfoText를 S0에 제공한다. 확인창 내부에 전체 저장 경로와 현재 진행 교체 안내를 표시하고 일반 결과는 S0 하단 feedback에 연결한다.
- EditMode SaveTests11개 작성: 정확한 long/decimal/string enum 왕복, 교체, 실패 시 기존 파일 보존, 임시 파일 무시, 손상·필수 필드 누락·파일 크기·버전 거부, ID/참조/설정 검증, 공유 권한 불일치, 일시정지 이동 큐/잔여 시간 및 동일 후속 이동 복원. TestResults/SaveFileTests 아래 고유 폴더만 사용하며 실제 사용자 슬롯은 건드리지 않는다.
- SA는 Unity/import/빌드/검사 실행을 하지 않았다. 이 기록은 작성/인계 상태이며 검증 완료가 아니다. 커밋·원격 연결·push·Computer Use 없음.

저장 후속 검토 반영: DTO 검증에도 종류별 물자 장부 보존식을 추가했다. 파일 저장 전과 임시 파일 재읽기 후에는 순수 DTO 검사와 별도로 detached 후보 세션 복원까지 확인해 실행 불가능한 스냅샷이 기존 슬롯을 교체하지 못한다. 세션 복원은 FileService를 호출하지 않으므로 검증 순환은 없다. BCL JSON 읽기가 최상위 객체 뒤의 내용을 무시할 수 있어 문자열 escape를 고려한 단일 루트 경계 검사로 trailing JSON을 선행 거부한다. 파일 오류 검사 기대치는 약화하지 않았다. SaveTests의 기존 덮어쓰기 방지 사례에 양수 수량 변조 및 장부는 맞지만 점유가 없는 운반 상태를 추가했다. 패널은 Runner에서 이미 번역한 결과 문장을 그대로 표시한다. 최신 실행 결과는 S0 통합 검증 기록을 따른다.


2026-11-16 S0 최종 인계: 저장 S1/S2를 통합하여 실제 JSON 단계별 왕복·손상 거부·전체 Edit165/Play60·Windows 파일 복원/PNG를 검증했다. [최종 결과](../../Verification/2026-11-16-save-load.md)와 [인계 해시](../../Verification/2026-11-16-save-load-inputs.json)를 따른다. 이전 작성 단계의 미검증 표시는 당시 기록이다. 현재 사람 직접 확인은 대기이며 커밋/원격 작업은 하지 않았다.
