# G0 준비 확정서

2026-11-15. 제품 기준 `d0069af439e0bccfd7d99f56bdd464965991ab12`. 계약 식별자 `settlement-five-v2`. G0-D는 명세 확정 이력이다. 후속 요청으로 G0-C 공통 연결 구현과 회귀 검사를 진행하며 개별 기능 구현과 커밋은 하지 않는다. 최신 실행 상태는 [G0-C 검증·인계](../Verification/2026-11-15-g0-c.md)를 따른다.

기존 G0를 **G0-D 명세/운영 준비**와 **G0-C 공통 연결 구현/검증**으로 구분한다. G0-D에서는 명세만 완료했다. 아래 서명 중 G0-C에서 연결한 최소 API와 아직 예정인 기능 API는 검증·인계 문서에서 구분한다. G0-C는 구현 요청 후 S0이 작성하고 회귀 검증해야 하며, 그 전에는 SA/SB의 제품 구현을 개시하지 않는다.

## 1. 작업 공간과 문서 기준

| 역할 | G0-D 현재 사용 공간 | 실제 기능 작성 시 구성 |
|---|---|---|
| S0 | `E:\Dev\RimworldTweak`, main, 위 기준 HEAD | 기존 폴더를 통합용으로 계속 사용 |
| SA | 아직 세션/작업 폴더 없음 | 별도 managed worktree 이름 `rimworld-feature-a` 사용, 건설→조리→저장 |
| SB | 아직 세션/작업 폴더 없음 | 별도 managed worktree 이름 `rimworld-feature-b` 사용, 재배→작업계획→연결 검사 |

기능 작성 공간은 **S0 기존 폴더 유지 + SA/SB 별도worktree** 방식으로 확정한다. G0-D에서 실제 폴더/브랜치는 만들지 않는다. 기능 착수 때 관리 도구가 반환한 절대 경로를 작업 카드에 기록하고 경로를 추측하지 않는다. 현재 `git worktree list`에는 통합 폴더 하나뿐이고 연결된 작업 첨부는 없다. 이는 공간 구성 명세와 실제 생성 상태를 구분한 것이다.

첫 기능 worktree는 G0-C가 검증된 상태를 받아야 한다. 기본 제품 HEAD만 checkout하면 현재 미커밋 계약/계획 문서가 빠지므로 그대로 작업을 시작하지 않는다. 커밋 승인이 있으면 검증된 연결 기준 커밋에서 만들고, 없으면 기준 HEAD에서 만든 뒤 S0이 명시한 연결 파일과 최신 문서만 복사/패치하여 목록·SHA-256이 같은지 검사한다. 임의 커밋을 전제로 하지 않는다. 미완성 다른 기능 변경은 함께 넘기지 않는다.

모든 인계 카드에 `baseCommit`, `contractVersion`, 제품입력 manifest 해시, 이번에 전달한 문서별 SHA-256, 절대 작업 경로, 허용 파일을 기록한다. 현재 문서의 권위는 S0 작업 폴더이며 커밋에 저장됐다고 가정하지 않는다. SA/SB는 자기 지시서/전용 파일만 수정하고 문서 동기화도 S0이 담당한다. 한 지시서의 변경을 다른 세션이 덮어쓰지 않는다.

각 worktree의 Library/Temp/Builds/TestResults는 따로 유지한다. 최초 import는 기능 착수 후 S0 실행 큐에서 처리하며 이번에는 하지 않는다. 같은 PC에서 Unity는 경로가 달라도 한 번에 하나만 실행한다. 검사 대상 폴더의 제품/검사/시나리오/도구 입력은 동결하고 다른 분리 폴더의 독립 작성만 계속한다. 사용자 에디터 강제 종료와 잠금 삭제는 금지한다. 원격 연결/push/세션 추가 없음. G0-D 이후 커밋도 별도 승인 대상이다.

## 2. 현재 코드에서 확인한 접점

| 현재 파일/접점 | 확인 사실 | 확장 책임 |
|---|---|---|
| WorldState.Step | 생활 판단 뒤 ProgressWork/DecideWork, 이후 이동/도착/Tick 증가 | S0이 성장/새 공정을 연결하고 기존 순서 보존 |
| WorldState.Work.DecideWork | 현재 운반 우선→채집, 가용성 변화 또는10Tick 재탐색 | G0-C에서는 이 동작 유지, J2에서 개인 정책으로 명시 교체 |
| WorldState.FindPath | 동·북·서·남 BFS, 호출 시 PathSearchCount 증가 | S0이 경로 비용을 계산해 읽기 문맥으로 전달. 기능 규칙은 직접 호출하지 않음 |
| EndLife/ClearWork | ActionId 권한 해제, 운반 낙하, 이동/대기 명령 정리 | 새 대상 취소도 S0의 동일 경로에 연결 |
| WorldState의 Resources/Beds | 초기 목록 및 내부 ID 사전이 존재. 자원은 동적 목록 사용 | 새 시설 등록/삭제와 침대 조회도 S0만 관리 |
| SimulationRunner | 명령 전달, pause/speed, 초기화/Reset | 새 명령과 원자적 복원/화면 재연결 담당 |
| SimulationSession | PendingSeconds, ResetCount, 초기 시나리오 보유. 복원 API 없음 | S0이 저장용 캡처/복원 접점 추가 예정 |

현재 없는 공통 타입은 S0 소유 예정 파일 `Assets/Game/Simulation/FeatureWorkContracts.cs`, World 연결은 `Assets/Game/Simulation/WorldState.Features.cs`에 둔다. 소유권 표에 등록했고 후속 G0-C에서 두 파일을 생성한다. 기존 WorkActivity/ColonistState/ScenarioDefinition 변경도 S0만 한다. SA/SB의 상태 클래스는 실행 상태를 담되 외부 읽기 전용으로 제공한다. 기능 Rules는 검사/후보/진행 결과를 계산하고 실제 상태 변경은 S0 적용 경로가 수행한다. 범용 플러그인/DI/새 asmdef는 도입하지 않는다.

## 3. 공통 API 명세

아래 서명과 타입은 v2 구현 목표다. 타입 이름이 바뀌면 S0이 계약 버전을 바꾸고 양쪽에 인계한다. bool 명령은 실패 시 기존 상태 보존 및 LastCommandError 설정, 성공 시 오류 초기화다. ID는 World 단조 증가 ID 공간을 공유한다. 대상 지정 실패를 처리하려고 Tick을 진행하지 않는다.

### World 명령 / 동일 이름 Runtime 전달

| 예정 서명 | 의미 |
|---|---|
| `bool PlaceBlueprint(BuildingKind kind, GridPosition position)` | 한 칸 설계도 생성. 생성 ID는 Blueprints 조회로 확인 |
| `bool CancelBlueprint(int blueprintId)` | 담당 취소·반입분 반환·설계도 제거, 오래된 ID는 InvalidBlueprint |
| `bool SetDemolition(int buildingId, bool enabled)` | 철거 지정/해제. 지정 자체는 자재를 반환하지 않음 |
| `bool CreateGrowingZone(GridPosition first, GridPosition second)` | 두 모서리 사각형 전체 유효성 검사 후 한 번 생성 |
| `bool SetSowingEnabled(int zoneId, bool enabled)` | 새 파종 시작 제한 및 진행 파종 취소, 기존 작물 성장/수확 유지 |
| `bool DeleteGrowingZone(int zoneId)` | 담당 취소 후 구역과 미수확 작물 제거, 산출 없음, 기존 바닥 물자는 유지 |
| `bool SetCookingOrder(int stoveId, bool enabled, int targetMeals)` | 목표0~99/가동 설정을 한 명령으로 적용 |
| `bool DeleteCookingOrder(int stoveId)` | 미완료 재료 반환, 진행 폐기, 기존 완성품 유지 |
| `bool SetWorkPriority(int pawnId, WorkCategory category, int priority)` | 0=끄기, 1~3. 끄기는 현재 해당 작업 안전 취소 |

`BuildingKind={Wall,Door,Bed,Stove}`, `WorkCategory={Gather,Haul,Build,Farm,Cook}`. `Blueprints`, `Buildings`, `GrowingZones`, `Crops`, `CookingOrders`, `WorkPolicies`는 읽기 전용 목록. 조리대는 완공 직후 가동=false/목표12인 주문을 하나 가진다. Reset은 기존 시나리오 초기값으로 새 월드를 만들고, 저장 불러오기와 혼동하지 않는다.

### 작업 후보 / 실행

- `WorkCandidate`: Category, Step, TargetId, optional SourceId, WorkCell, RequestOrder, PathLength, Reason. Step은 Gather/HaulToStorage/DeliverBuild/Construct/Demolish/Sow/Harvest/DeliverCook/Cook이다. TargetId는 현장/작물/시설 등 실제 작업 대상이며 SourceId는 반입 스택이다.
- `WorkReasonCode`: None, Disabled, Drafted, NeedsPriority, NoDesignation, MissingMaterials, NoWorker, Unreachable, TargetReserved, OutputFull, TargetReached, AwaitingClearance, InvalidTarget. 후보 실패와 전체 '담당자 없음'을 구분해 WorkDiagnostics가 집계한다.
- `WorkReadContext`: Tick·읽기 Pawn/대상 상태·S0이 계산한 경로 비용·가용량·정책을 제공하는 좁은 문맥. World 변경 메서드나 변경 가능한 사전/배열은 노출하지 않는다. 조회를 위해 World 전체를 매 프레임 복사하지 않는다.
- `ConstructionRules.GetCandidates(WorkReadContext context, int pawnId)`, `FarmingRules.GetCandidates(...)`, `CookingRules.GetCandidates(...)`는 `IReadOnlyList<WorkCandidate>`를 반환한다. 물자/점유/진행을 변경하지 않는다. 경로 검색 카운터 증가는 S0 경로 계산 단계에서만 발생한다.
- `WorkPolicy.Select(WorkReadContext context, int pawnId, IReadOnlyList<WorkCandidate> candidates)`는 후보 또는 null을 반환한다. 시작 권한을 확보한 것으로 취급하지 않는다.
- S0 내부 `bool TryStartFeatureWork(int pawnId, WorkCandidate candidate)`는 현 상태를 재검사하고 ActionId를 결정한 뒤 필요한 권한을 일괄 확보한다. 실패 시 이번 시도에서 얻은 권한만 해제하고 물자는 그대로 둔다.
- S0 내부 `void ProgressFeatureWork()` / `void HandleFeatureArrivals()`는 기존 진행/도착 위치에서 호출한다. `void CancelFeatureWork(int pawnId, long actionId, WorkCancelReason reason)`는 EndLife/ClearWork 경로에 합쳐 호출한다. WorkCancelReason은 Draft/Life/Disabled/Explicit/TargetRemoved/Unreachable/RestoreInvalid이다. 공개 취소 UI는 기존 명령 경로를 사용한다.
- `ConstructionRules.EvaluateStep(WorkReadContext context, int pawnId)` 및 Farming/Cooking의 같은 서명은 `FeatureWorkResult`를 반환한다. 이 결과는 진행 전/후 값, 완료 여부, 필요한 소비/산출, 보류 이유만 담는다. S0이 ID/ActionId/진행값을 재검사해 한 번 적용한다. 저장 후 재적용할 이벤트 큐로 쓰지 않는다.

### 반입과 점유

반입 작업은 Build 또는 Cook에 속한다. 일반 운반 정책을 꺼도 건설/조리 담당자가 자재를 가져올 수 있다. 반입은 계획 비용에서 현장/작업대 투입분과 운반 중 확정 반입분을 뺀 부족량까지만 허용한다.

반입 접근 단계에서는 출발 스택만 점유한다. 후보의 목적지 ID는 반입 의도이며 아직 목적지 점유가 아니다. `PendingDeliveryTargetId`를 Pawn 상태에 보존하고 UI에는 목적지와 실제 점유 대상을 구분한다. 집기 순간 목적지의 권한/부족량을 다시 검사해 대상 권한 확보→물자 이전→출발 권한 해제를 한 호출에서 처리한다. 목적지를 확보하지 못하면 집지 않고 출발 점유와 행동을 정리한다. 이것은 기존 운반의 교환 규칙을 확장한 것으로 안정 상태에서 권한 하나라는 규칙을 유지한다.

출발 도착 다음1Tick에 집기, 물자는 스택→운반으로 이전하고 출발 권한 해제. 목적지 도착 다음1Tick에 반입, 운반→내부 자재 이전 및 대상 권한 해제. 최대 운반10을 유지한다. 대상이 없어지면 운반물은 Pawn 현재 위치에 한 번 낙하한다. 현장/조리대 대상의 권한을 반입/시공/조리/철거가 공유하므로 같은 대상의 실행자가 겹치지 않는다. 스택이 저장 슬롯 안에 있으면 기존 슬롯/스택의 공유 권한을 사용한다. 반입용 원본은 바닥 또는 저장 구역에 있는 해당 자원이며 다른 현장/작업대 내부 자재를 빼앗지 않는다.

## 4. 시험 수치와 Tick 경계

모두 최초 시험값이며 최종 밸런스가 아니다. 기존 생활 수치/기본 시나리오는 바꾸지 않는다. 장기 자급2주기나64×64 성능 완료를 뜻하지 않는다.

| 항목 | 확정 시험값 |
|---|---|
| 시간 | 1Tick=0.1게임초, 하루1200Tick, 1/3배속·정지 유지 |
| 이동/물류 | 평상시5Tick/칸, 기아 시 기존10Tick/칸, 운반10/회, 집기1·놓기1Tick, 저장 칸50 |
| 건설 비용/시공 | 벽 목재2/40Tick, 문4/60Tick, 침대10/100Tick, 조리대15/120Tick |
| 철거 | 종류 공통30Tick, 완료 시 기존 건설 비용100% 반환 |
| 파종/성장/수확 | 파종20Tick, 성장2400Tick, 수확30Tick, 수확량 식재료6, 씨앗 자원 없음 |
| 조리 | 식재료2→식사1, 40Tick/회, 초기 목표12, UI 범위0~99 |
| 작업대 내부/출력 | 입력 최대2식재료(1회분), 출력 타일 식사 전용 슬롯50, 한 회차만 진행 |
| 재평가 | 관련 가용성 변화 또는 최대10Tick 간격, 후보 매 프레임 재탐색 금지 |
| 기본 개인 설정 | Gather2, Haul1, Build2, Farm2, Cook2. 모두 enabled |

완료까지 N Tick인 행동은 도착/시작 Tick에0이며 다음 Tick부터1씩 증가한다. N-1 뒤 미완료, N 뒤 한 번 완료. 자재가 부족하거나 출력 포화면 진행/소비를 멈춘다. 완공/수확/조리 산출은 기존 CreatedTick 기준으로 다음 Tick부터 일반 운반 대상이다.

파종 완료 Tick에는 성장0, 이후2400번째 성장 Tick에 성숙한다. 성장 단계는 기존 생활/노동 결정 전에 갱신하되 그 Tick에 새로 파종된 작물은 제외한다. 성숙 시 수확 후보가 될 수 있지만 새 행동 자체는 다음 Tick부터 진행한다. 생장에는 Pawn 점유가 필요 없다.

G0-C는 기존 운반→채집 우선 규칙을 유지한다. J2에서만 개인 설정이 실제 선택을 대체한다. 정렬 순서는 수행 가능→개인 우선순위→접근 경로 길이→요청순→대상ID→Step 순이다. Pawn 배정은 ID순. 기존 운반 스택의 요청순은0으로 두어 ID 동률 규칙을 유지한다. 재파종은 새 요청순을 부여한다. 모든 기능 상태는 자체 월드에 속하고 Reset 때 새로 생성한다.

## 5. 시설·영역·출력 위치

시설은 모두1×1, 회전 없음. 벽/조리대는 완공 후 이동 불가, 문은 이동 가능(열림 애니메이션/시간 비용 없음), 침대는 기존처럼 해당 칸에 들어가 수면한다. 시공/철거 접근은 동→북→서→남 중 도달 가능한 최소 이동 비용, 동률은 이 순서다. 완공 전 설계도는 이동 가능하다.

2026-11-19 변경. 아래 최초 완공 대기 규칙 중 폰 점유는 네 건물 본체의 착공·진행·완공 모두에 적용한다. 바닥 물자 대기와 붕괴벽 수리의 예약 칸 검사는 [승인된 규칙](Diagonal-Movement-Construction.md)을 따른다.

조리대 사용 위치와 출력 위치는 본체 남쪽 `(x,y-1)` 한 칸을 함께 사용한다. 사용자는 이 칸에 서고 출력은 그 칸의 식사 슬롯에 있다. 겹침 방지를 위해 설치 때 본체와 서비스칸 둘 다 유효성을 검사하고 서비스칸을 다른 건물/구역/조리대가 사용할 수 없게 등록한다. 맵 밖이면 설치 거부. 본체/서비스칸의 기존 바닥 자원·나무·침대·시설·작물·설계도·저장/재배 구역과 겹치면 거부한다. 일반 Pawn은 설계도 위를 통과할 수 있으나 막는 시설 완공 때 Pawn/바닥 물자가 남아 있으면 AwaitingClearance로 보류한다. 강제 이동/물자 삭제는 하지 않는다.

일반 시설·저장·재배 구역도 서로 중복 배치 불가다. 재배/저장 사각형은 일부만 생성하지 않으며 전체 유효 시 생성한다. 재배칸의 생장 중 일반 통행은 가능하다. 새로 생긴 바닥 자원은 작물 위에 존재할 수 있고 수확 산출을 위해 삭제하지 않는다.

조리 출력 슬롯은 일반 저장 구역이 아니지만 기존 슬롯/스택 공유 권한 원칙을 재사용한다. 음식 섭취와 출력 입고가 같은 권한을 쓰며, 완성 식사는 먹거나 일반 저장 구역으로 운반할 수 있다. 출력 소유 스택이 차거나 다른 Pawn이 점유했으면 완료 소비/산출을 보류한다. 일반 운반이 임의 물자를 이 슬롯에 입고하지 않는다. 음식 소비/운반으로 공간이 나면 재개한다.

철거 지정은 즉시 새 사용/반입을 막고 현 사용자의 행동을 취소한다(수면도 종료). 철거 자체는 Build 작업으로30Tick 필요하다. 철거 취소는 아직 자재 반환 없이 원래 시설 사용을 다시 허용한다. 철거 완료 시 조리 미완료 투입분과 완성 출력은 바닥에 한 번 놓고 원래 목재 비용을 한 번 반환한다. 내부 자재와 건설 비용 반환은 서로 다른 장부다. 침대 삭제·통행 revision·시설 조회는 같은 적용에서 갱신한다.

## 6. 조작 확정

| 조작 | 확정 동작 |
|---|---|
| 건설 버튼→종류→좌클릭 | 설계도1개 생성 성공 시 선택 도구로 복귀. 오류면 도구 유지·이유 표시 |
| 재배 구역 버튼→두 모서리 | 구역1개 생성 성공 시 도구 해제, 오류 시 첫 모서리 초기화·도구 유지 |
| 우클릭/Esc(도구 중) | 도구만 취소, 이동 명령은 보내지 않음. 도구 밖은 기존 동작 유지 |
| 설계도 취소 | 즉시 담당 정리·실제 반입분 반환·표시 제거 |
| 철거 토글 | 지정/해제 즉시 표시, 실제 철거/반환은 작업 완료 때 |
| 파종 끄기 | 진행 중 파종 취소(산출 없음), 새 파종 금지. 기존 작물 성장/수확 유지 |
| 재배 구역 삭제 | UI에 '미수확 작물 제거·수확물 없음' 안내. 삭제 시 해당 작업 취소/작물 제거, 기존 바닥 자원 유지 |
| 조리 가동 끄기/목표 하향 | 새 회차 금지, 진행 회차가 현재 목표에 불필요하면 즉시 담당 취소·내부 재료/진행 보존. 필요해지면 재개 |
| 조리 목표0 | 가동 여부와 무관하게 새 생산/진행 보류. 입력/진행은 유지 |
| 조리 주문 삭제 | 진행 폐기·입력 반환·완료 출력 유지. 이후 기본 정지 주문을 새로 만들 수 있음 |
| 개인 작업 끄기 | 해당 Pawn의 해당 종류 행동 즉시 안전 취소, 진행은 현장/작업대에 유지 |
| 개인 우선순위1~3 변경 | 값/UI 즉시 반영, 현재 유효 행동 유지, 다음 선택부터 적용 |
| 저장/불러오기 | 별도 버튼 두 개, 한 슬롯. 저장 덮어쓰기 버튼 문구 명시. 불러오기 확인 창에서 현재 진행 교체를 알림 |

R/Space/1·3/한Tick/Reset은 기존 의미를 유지하며 새 단축키를 추가하지 않는다. 입력창에 포커스가 있으면 기존 게임 단축키를 차단한다. 선택 정보는 각 기능의 실제 상태/담당/진행·자재·보류 이유를 보여준다. 명령 직후 또는 다음 RefreshNow에서 갱신하고 정지 중에도 다음 Tick을 기다리지 않는다.

생산 목표의 수량 Q는 현재 남은 모든 식사(바닥/보관/운반/출력, 중복 제거)다. 소비되지 않은 점유 식사도 포함한다. 가동 중인 회차는1 생산 예정으로 세고 새 시작 때 Q+활성회차가 목표 미만이어야 한다. 목표 하향 시 stove ID 오름차순으로 필요한 회차만 유지하고 나머지는 보류한다. 보류 중 입력은 물자 보존에 포함하지만 생산 예정에는 포함하지 않는다. 여러 조리대는 하나의 전역 목표12를 공유하지 않고 각 주문의 targetMeals를 월드 식사 Q에 대해 평가한다. 시작/유지 판정은 ID순으로 순차 적용해 동시에 초과 확보하지 않는다.

## 7. 저장·복원 확정

### 경로와 형식

한 슬롯 UTF-8 JSON, `schemaVersion=1`, `scenarioId`, `scenarioVersion=1`, `contractVersion=settlement-five-v2`, `rulesVersion=1`을 기록한다. enum은 문자열, long은 정확한 정수, decimal 잔여 시간은 invariant 문자열로 저장한다. C# 타입 메타데이터나 Unity 객체 참조는 넣지 않는다. 미지원 schema/rules 버전은 이 단계에서 마이그레이션 없이 거부한다.

제품 경로는 `Path.Combine(Application.persistentDataPath, "Saves", "settlement-v1.json")`이다. 현재 companyName/productName은 모두 RimworldTweak이므로 Windows 예상 경로는 `C:\Users\kijin\AppData\LocalLow\RimworldTweak\RimworldTweak\Saves\settlement-v1.json`이다. 실제 실행에서는 persistentDataPath 반환값을 로그/UI로 확인하며 고정 사용자 경로를 코드에 넣지 않는다. 자동 검사는 TestResults의 해당 실행 폴더 아래 임시 경로를 주입해 사용자 저장을 건드리지 않는다.

같은 디렉터리 `.tmp`에 쓰기→스트림 flush/close→다시 읽어검증→기존 파일이 있으면 replace, 없으면 move한다. 실패하면 기존 최종 파일을 유지하고 오류를 표시한다. 중간 파일은 저장 슬롯으로 읽지 않는다. 실제 원자 교체·권한/디스크 부족·손상 검사는 G0-C가 아니라 저장 구현 단계 S1에서 한다. 동시 저장/읽기는1개만 허용하고 추가 요청은 SaveBusy다.

### API와 저장 경계

- S0 `SaveCaptureResult CaptureSaveData()` (SimulationSession): 명령 적용과 Tick 사이 안정 상태의 순수 데이터 캡처. 결과는 Success/Data/ErrorCode다. 토폴로지/테스트용 생활 큐가 남으면 SavePendingCommands로 거부한다. 수동 이동 큐는 저장 목록에 포함해 정지 중 내린 이동 명령을 보존한다.
- SA `SaveValidation.Validate(SaveData data)` → `SaveValidationResult`(IsValid, ErrorCode, FieldPath). 검사 실패는 데이터/실행 월드를 바꾸지 않는다.
- SA Runtime `SaveFileService.Save(string path, SaveData data)` → `SaveFileResult`; `Load(string path)` → `SaveLoadResult`(Success, Data, ErrorCode). Simulation에 파일 I/O 의존을 추가하지 않는다.
- S0 `bool SimulationRunner.TryApplySave(SaveData data)`는 후보 월드/세션을 구성하고 검증한 뒤 한 번 교체한다. 예외/실패면 현재 World 참조·pause/speed·화면을 그대로 유지한다. 성공 시 저장된 pause/speed/PendingSeconds를 적용하며 추가 Tick을 진행하지 않는다.

Reset의 초기 시나리오 데이터도 저장한다. 저장 상태를 새 초기 상태로 바꿔 Reset 의미를 바꾸지 않는다. 파일 읽기/확인 창 동안 게임 시간 전달을 일시 중단하고 취소/실패 시 이전 pause/speed로 돌아간다. 저장 성공/실패도 시작 전 시간 설정을 유지한다. 불러오기 성공 시 선택/미리보기/도구는 해제하고 새 월드로 즉시 다시 그린다.

### 필수 필드와 복원 순서

1. 헤더/범위/중복ID/수량/enum/참조/진행 범위/파일크기 상한16MiB 검사. 맵은 이번 데이터 모델의 범위1~64칸 축을 지원하되 성능 통과를 뜻하지 않는다. 다음ID가 살아있는/소진 기록 ID보다 커야 한다.
2. 월드 크기·통행·Tick·시나리오 원본·ResetCount·PendingSeconds, 다음ID/요청순/가용성revision을 복원한다.
3. 자원 스택(수량/존재/종류/CreatedTick/생성·소비·외부변경 집계), 저장 구역/슬롯/필터, 채집 노드/진행, 설계도/현장 자재/시설/비용장부, 재배 구역/작물/성장·요청순, 조리 주문/입력/진행/출력 슬롯, 정책을 만든다. ID 참조를 연결하고 읽기 목록과 내부 사전을 일치시킨다.
4. Pawn 위치/욕구/소집/수면 목적/ActionId, 생활·노동 상태/대상/진행/재개 가능Tick/PendingDeliveryTargetId, Cargo, 이동 목적/다음 경유칸/칸 진행/칸당Tick, 보류 이유/재시도Tick/진단 카운터를 복원한다. 수동 이동 큐의 명령 순서도 복원한다.
5. 저장된 권한을 PawnID순으로 재검증·재구성한다. 같은 대상 이중 소유, 없는 소유자, 잘못된 ActionId, 중복 물자 위치는 손상 파일로 거부한다. 저장 슬롯/스택 권한은 같은 객체로 연결한다. 반입 중 PendingDeliveryTargetId와 실제 점유 대상이 단계에 맞는지 확인한다.
6. 경로는 좌표/통행을 재검사한다. 저장된 다음칸이 유효하고 재계산 경로의 다음칸과 같으면 칸 이동 진행을 유지한다. 무효 시 진단과 함께 공통 보류/취소로 정리한다. 경로 재검증 자체가 기록된 게임 진단 카운터를 부풀리지 않도록 복원 전용 검색을 쓴다. 유효한 저장 파일에서는 작업/물자/ActionId를 임의 초기화하지 않는다.
7. 완료/소비/산출된 상태는 다시 완료하지 않는다. 완료한 진행은 이미 반영된 장부로 읽는다. 후보 월드의 보존/독점/순서 불변 조건을 확인한 뒤 Runner 교체와 UI 갱신을 수행한다.

검증할 대표 오류: InvalidBlueprint, InvalidBuilding, InvalidGrowingArea, InvalidGrowingZone, InvalidWorkPriority, InvalidCookingOrder, OccupiedServiceCell, SaveBusy, SavePendingCommands, SaveNotFound, SaveUnsupportedVersion, SaveInvalidData, SaveIoError. 메시지는 UI에서 한국어로 매핑하고 파일 원문/전체 예외를 사용자 화면에 그대로 노출하지 않는다.

## 8. 구현 후 실행할 계약 검사

| 검사 | 관측 기준 | 지금 상태 |
|---|---|---|
| 읽기 후보 | 규칙 호출 전후 권한/수량/진행/ActionId 불변, 동일 입력 동일 후보 | 명세만 |
| 시작 경쟁 | 두 Pawn이 같은 대상 시작, 한 명만 성공. 실패 시 새 권한/물자 잔류0 | 명세만 |
| 반입 취소 | 원본 접근/집기/운반/반입 각 단계 R, 총량 보존·해당 ActionId 권한 전부 해제 | 명세만 |
| N Tick 경계 | 시작0, N-1미완료, N완료1회. 정지 명령은 시간/욕구 불변 | 명세만 |
| 공간/완공 | 서비스칸 충돌 거부, 벽 완공 보류/해소, 출력50포화·재개 | 명세만 |
| 정책 | 기본 운반 우선 보존, J2동률/끄기/수치변경, 여러 조리대 목표 감소 | 명세만 |
| 저장 왕복 | pending move/칸 진행/생산 진행/권한/자재 유지, 잘못된 파일 원본 보존 | 명세만 |

G0-C에서 S0이 공통 DTO/읽기 문맥과 기존 Gather/Haul을 이용한 최소 시작/취소 연결 검사를 작성하고 실제 EditMode/PlayMode 회귀를 통과한 뒤 SA/SB에 기준을 전달한다. 아직 없는 ConstructionRules/FarmingRules/CookingRules를 참조해 컴파일 불가능한 기준을 배포하지 않는다. 새 공개 기능 명령의 연결은 담당 결과 단위가 들어올 때 S0이 추가한다. 아직 기능이 없는 항목을 가짜 스텁으로 통과시켜 G1~G5로 보고하지 않는다. 기능 완성 후 해당 G 경계에서 전체 회귀·Windows 빌드/Player·렌더를 실행하고 사람의 실제 조작 확인을 별도 남긴다.

이번 G0-D의 검증은 소스 읽기와 문서/파일 검사에 한정한다. 구현된 API나 실행된 계약 검사라는 주장은 하지 않는다.

## G0-D 확인 결과

제품 입력150개는 준비 전후 파일 목록·SHA-256 동일, 문서53개 로컬 링크 누락0건, diff 공백 검사 통과. 현재 저장 칸50·기존 도착 다음1Tick 집기/놓기·0.1게임초/Tick을 소스와 대조했다. 실제 생성된 작업 공간은 S0 한 곳이며 신규 기능worktree/브랜치/세션·제품 코드·테스트 실행·커밋은 없다. G0-C와 새 기능의 실행 가능성은 미검증이다.

## G0-C 착수 범위와 소유권 (2026-11-15)

최신 구현 요청으로 G0-C를 시작한다. S0은 FeatureWorkContracts.cs, WorldState.Features.cs 및 .meta, 기존 WorldState.Work/Living.cs, 신규 Tests/EditMode/Simulation/FeatureWorkContractTests.cs 및 .meta, Tools/expected-editmode-tests.json과 관련 문서를 소유한다. 기존 파일은 공통 선택/시작/취소 연결만 변경한다. 기능별 Rules/상태/패널과 저장 I/O는 작성하지 않는다. 후보 계산은 기존 자동 재탐색 시점에만 수행하며, G0-C 문맥에는 기존 Gather/Haul의 값 스냅샷만 넣는다. 신규 기능 데이터는 해당 통합 시 계약에 추가한다.
