# 작업 2 생활 기능 공통 계약과 소유권

> 현재 변경 기준: [상호작용 단일 점유·R 소집 계획](../Plans/Interaction-Occupancy-Draft.md). 소집·독점·취소·수면 재선택의 현재 API는 [점유 계약](InteractionOccupancy.md)을 따른다. 아래 초기 생활 계약과 충돌하면 점유 계약이 우선한다.
2026-11-15 구현 목표 재개에 따른 공통 계약. [상세 계획](../Plans/Task2-Needs-Food-Sleep.md)의 추천 수치·행동·예약 규칙을 이번 구현 기준으로 적용한다. 사용자 목표 재개에 따라 검토·구현·검증을 진행한다. Computer Use·커밋·원격 작업 금지. 공유 폴더 유지.

## 데이터·시나리오 API (RimworldTweak.Simulation)

- ColonistSeed(int id, GridPosition position, int nutrition=10000, int rest=10000). Nutrition/Rest int getter,0~10000 정수(0.01단위). seed는 불변.
- BedSeed(int id, GridPosition position), Id/Position getter.
- ScenarioDefinition 기존5개 인자 뒤 optional IEnumerable<BedSeed> beds=null, bool needsEnabled=false. Beds readonly복사, NeedsEnabled bool. 기존 이동 fixture는 생활 없이 독립 검증하되 일반 데모는 아래 LivingScenario를 사용. null beds는 empty. 복사에서 욕구값 누락 금지.
- SandboxScenario.Create는 기존 이동 시나리오 유지. LivingScenario.Create 신규: 기존8×8, 정착민3/목재40/식사18/장애물(3,3), 포만도3500/휴식2500, 침대201(5,5)·202(6,5), needsEnabled=true.
- World 생성에서 욕구범위 InvalidNeeds, 침대 null/ID중복/범위/장애물 위치 등 기존 유효성 규칙 확장. ID는 모든 종류에서 고유. 다른 World/seed와 가변 데이터 공유 금지.
- World.Resources를 IReadOnlyList<ResourceState>로 확장. ResourceState getter Id/Kind/Position/Amount, ReservedAmount, AvailableAmount, Exists(bool), ConsumedAmount/AddedAmount/RemovedAmount(int), Reservations(IReadOnlyDictionary<int,int>, colonistId→수량, 최대1명). 초기 positive수량 유지, 실행 수량0허용, 외부setter없음. 초기+Added-Consumed-Removed=Amount. 예약은소비아님.
- World.Beds: IReadOnlyList<BedState>. BedState getter Id/Position/Exists/OwnerId(int?)/IsOccupied(bool). 예약 이동 중Owner만, 수면중Occupied true. 제거시해제. 원소는 외부수정불가.

## 생활 상태 API

- ColonistState 기존이동 getter 유지. Nutrition/Rest(int), LifeActivity(enum), LifeTargetId(int?), ActionProgress(int), ActionDuration(int), LifeReason(string), MealsEaten/RawMealsEaten/SleepsCompleted(int), StarvationTicks(long), MovementTicksPerTile(int) 추가.
- enum LifeActivity { Idle, GoingToFood, Eating, GoingToBed, SleepingInBed, SleepingOnFloor }. 생활목적지 이동은 기존 MovementStatus와 별개. 먹을종류는 LifeTargetId의 Resource.Kind로 판정.
- LifeReason codes: 빈문자열, NoFood, FoodReserved, FoodUnreachable, NoBed, BedReserved, BedUnreachable, Starving. UI에서 읽는 문장으로 번역. 바닥수면 원인과 식량문제 동시 표시가 필요하면 FoodReason(string), SleepReason(string) getter 추가하고 LifeReason은 현재 우선 원인. Nutrition0 경고는 수치로 별도 표시 가능.
- World.NeedsEnabled getter. 기존 Step 순서를 승인 계획의 5단계로 확장: 외부변경/예약검증→시작상태욕구→기존행동시간/완료→생활결정/예약경쟁→허용수동명령/이동/도착. 신규식사·수면은 다음Tick부터 시간/회복. 경로와5Tick 이동 재사용,기아0에서 다음칸은10Tick로고정. 내부단계는 분리 가능하되 동작순서 보존.
- 기본감소 Nutrition-10/tick(수면/식사포함); 깨어있을때Rest-5,침대수면+50,바닥+25. 임계3000/2000/9000. 일반식사20Tick·1개·+6000,생식40Tick·4개·+6000. clamp0..10000.
- R/단일 DraftButton으로 소집·해제 토글. 즉시 명령/점유/수면목적 해제, 소집중 수동 이동 우선. 구체 API와 행동세대 검증은 InteractionOccupancy 계약을 따른다.
- 정착민별예약,신규식사경쟁 Nutrition→ID,침대 Rest→ID. 유효예약빼앗지않음. 음식종류우선(Meal→Ingredients) 후실제경로길이→ID. 양쪽부족은먹을수있을때식사우선,못먹으면수면유지/시작. 수면중허기도음식확보가능할때만깨움.
- 경로완전차단/대상무효화는예약해제·대안선택. 실패재시도는관련변경 또는최대10Tick. 성공예약은더가까운물자가생겨도유지. 바닥수면은가용침대발생시재평가하고점유확보후이동한다.

## 테스트용 외부변경 경계 (화면 도구 아님)

- QueueSetResourceAmount(int id,int amount):bool. 다음Tick 수량설정,0이상. Exists=true로복원가능. 차이분은Added/Removed계수. 현재확보량보다작아지면점유해제후재선택하여음수예약방지.
- QueueRemoveResource(int id):bool. 다음Tick Exists=false/Amount0/예약해제,제거량기록. 스택ID재사용안함.
- QueueSetBedExists(int id,bool exists):bool. 다음Tick 삭제/복원;삭제시점유해제·대안처리.
- QueueCancelLife(int id):bool. 테스트용안전취소,다음Tick예약/경로/진행해제하고그Tick신규생활결정을건너뜀. 다음Tick부터정상재평가. UIStop은이API를쓰지않는다.
- 오류id/음수는false및LastCommandError(InvalidResource/InvalidBed/InvalidAmount/InvalidColonist),정상접수는이전오류해제. 외부변경은PendingCommandCount에포함. Reset은새World전체복원.

## Runtime/Presentation

Runtime Awake 기본은 LivingScenario. 기존이동검사는 Sandbox를명시초기화. 시간·속도·pause/Reset 정책유지.
WorldMarker ViewKind에Bed 추가. 초기living marker71(64+3+2+2),이동fixture69유지. Resource0/Existsfalse 또는BedExistsfalse marker는재사용하며비활성. 목록에서제거하지않아Reset객체수가늘지않게한다. 표시 Amount는실제값.
SettlementView 기존공개API유지. 새 Text getter NeedsText(선택인의욕구), LifeText(생활/대상/진행/이유), ReservationText(총량/예약/가용·침대소유). CommandFeedbackText는LifeBusy문장추가. 같은World의수량/예약/침대/생활상태를매갱신반영.침대는별도색/표시로빈/예약/수면구분,자원수량가리지않음. 비소집생활에서Move거부시R소집안내, 소집중DRAFTED표시와Undraft조작을제공하고선택/시간은유지. 새 OS입력이나자동화없음.

## 단독 파일 소유권 — 작성만 병렬

| 담당 | 쓰기 범위 |
|---|---|
| life_sim | Assets/Game/Simulation/*.cs 및신규meta. 전체생활/예약/데이터/시나리오/이동연계. asmdef수정금지 |
| life_view | Assets/Game/Presentation/*.cs 및신규meta. WorldMarker/SettlementView 표시확장. asmdef/Runtime/Tests수정금지 |
| life_tests | Assets/Game/Tests/**/*.cs/meta, Tools/expected-*-tests.json. 기존46검사 의미보존. 신규생활T2-01~12/2400Tick/회귀 |
| root | Runtime,Demo smoke,Editor/scene/asmdef,Tools(예상목록제외),문서,최종통합,모든Unity실행 |

공동API변경은root에게요청. 작성종료후쓰기동결,root가컴파일·EditMode·PlayMode·build·Player를직렬실행. 신규생활사람확인은별도미검증으로기록.
