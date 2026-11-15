# 저장·불러오기 실행 카드

2026-11-16. 사용자 조리·작업계획 정상 확인 후 저장 S1/S2 구현 승인. 커밋 금지. 기준 cooking-work-policy-1, HEAD d0069af, 제품208개. 규칙은 G0-Preparation 저장 절을 따른다.

- S0 E:/Dev/RimworldTweak: WorldState.Save.cs, SimulationSession.Save.cs, EntitySaveState.cs, SimulationRunner.Save.cs, SettlementView.Save.cs 및.meta, 기존 공통 파일/asmdef/검사 manifest/최종 문서. 실제 캡처·복원·월드 교체·UI 연결과 Unity 직렬 검사 소유.
- SA feature-a: Simulation SaveData.cs/SaveValidation.cs, Runtime SaveFileService.cs, Presentation SaveLoadPanel.cs, EditMode SaveTests.cs 및.meta, FeatureA 기록. DTO·검증·파일처리·패널 소유.
- SB feature-b: EditMode SaveRoundTripTests.cs, PlayMode SaveIntegrationTests.cs 및.meta, FeatureB 기록. 실제 상태 왕복과 실패 보존 검사 소유.

## 접점

Session.CaptureSaveData(bool paused=false,int speed=1) → SaveCaptureResult Success/Data/ErrorCode.
SimulationSession.TryRestoreSave(SaveData,out SimulationSession,out string error) → bool. 후보를 만들고 검증하며 실패 시 입력/현재 월드에 변화 없음.
Runner.TryApplySave(data), SaveNow(), BeginLoad(), ConfirmLoad() → bool, CancelLoad() → void. SavePath는 검사 경로 주입 가능, 기본은 persistentDataPath/Saves/settlement-v1.json. SaveMessage/IsLoadConfirmationOpen으로 UI연결.
SA SaveFileService는 .tmp→flush→재검증→replace/move,16MiB,UTF-8 JSON. 추가 패키지 없이 BCL DataContractJsonSerializer 사용. enum은 문자열 필드, long은 정수, decimal 시간잔여는 invariant문자열. 타입메타데이터 금지.
SaveWorld.NextEntityId는 다음 할당할 ID이며 내부 last-allocated 필드의+1로 캡처하고-1로 복원한다. 삭제·소진 기록 ID도 보존한다.

## 검증 기준

정지 중 수동 이동 큐, 칸 진행, 먹기/수면·작업·반입·운반, 성장·수확·조리 완료 직전, 정책/필터/소집, 초기 시나리오 Reset을 보존한다. 공유 점유(저장슬롯/스택, 출력/음식, 시설/조리주문)는 동일 객체로 재결합한다. 잘못된 버전/참조/수량/중복 점유/파일 쓰기 실패는 기존 파일·현재 월드 보존. 자동 검사는 TestResults 하위 경로를 주입해 사용자 저장을 사용하지 않는다.

Unity 실행 중 S0 제품 입력 동결, 기능 담당은 자기 작업 공간에서만 쓰며 S0 검사 입력을 변경하지 않는다. 에디터 강제 종료·잠금 삭제·Computer Use·커밋·원격 작업 없음.

## 구현된 공통 확장

S0는 기존 ColonistState/ResourceState/BedState/GatherState/StorageSlotState/StorageZoneState/ConstructionState/FarmingState/CookingState 타입을 partial로 선언 확장했다. 기존 기능 본문은 유지하고 명시적 캡처/복원 메서드는 S0 소유 EntitySaveState.cs에 둔다. 기능 담당에게 이 접점 변경을 인계했다. Runtime.Reflection이나 private 필드 이름에 의존한 복원은 사용하지 않는다.

실제 파일 읽기/쓰기 후 첫 자동 Update는 건너뛰어 동기 I/O 지연이 추가 게임 시간으로 반영되지 않게 한다. 다음 자동 프레임부터 정상 전달하며 명시 AdvanceTime/한Tick 조작은 이 생략의 영향을 받지 않는다. 이 때문에 복귀 시 최대 한 프레임의 일반 경과 시간이 함께 제외될 수 있으며, 파일 처리 시간을 몰아서 진행시키지 않는 쪽을 택했다.

파일 왕복 강화 후 EditMode165개, 시간/UI 확인 포함 PlayMode60개가 통과했다. 빌드/Player/렌더와 최종 입력 해시는 [검증](../../Verification/2026-11-16-save-load.md)에 확정한다.

## 최종 결과

Edit165/Play60 및 Windows 빌드/실제 파일 Player/확인창·복원 PNG 검증을 완료했다. 제품232개 최종 입력과 실행 전후 입력은 [검증 manifest](../../Verification/2026-11-16-save-load-inputs.json)에 기록한다. S0/SA/SB에 동기화한 이 기준으로 이어간다. 다음은 사용자 직접 확인이며 신규 기능·커밋은 별도 승인 범위다.
