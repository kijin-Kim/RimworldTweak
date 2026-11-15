# 구조 설계

Simulation, Runtime, Presentation, Demo, Editor와 Edit/PlayMode 테스트 어셈블리를 분리했다. 현재 구현 API는 [데모 계약](Contracts/DemoRuntime.md)을 따르며 실제 통과 여부는 Status와 검증 기록을 기준으로 한다.

## Unity 구성

Unity 6.3 LTS 6000.3.28f1 설치와 ProjectVersion.txt 고정을 확인했다. Universal 2D, 직교 카메라, Windows 데스크톱을 기준으로 시작한다. Input System, uGUI, Unity Test Framework를 사용하고 패키지는 해석 후 manifest와 lock에 고정했다. 실제 버전은 [Setup.md](Setup.md)를 따른다.

Visible Meta Files와 Force Text를 사용한다. 에디터 버전은 ProjectSettings/ProjectVersion.txt, 패키지는 Packages/manifest.json과 생성된 packages-lock.json이 실제 기준이다. 버전 변경은 검증 후 기록한다.

## 폴더 구성 (Content는 예정)

```text
Assets/Game/
  Simulation/
  Runtime/
  Presentation/
  Demo/
  Editor/
  Content/Scenarios/
  Content/Definitions/
  Scenes/SimulationSandbox.unity
  Tests/EditMode/Simulation/
  Tests/PlayMode/Integration/
Packages/
ProjectSettings/
Tools/Run-Tests.ps1
```

## 코드 경계

| 영역 | 책임 | 참조 |
|---|---|---|
| Simulation | 상태·시간·작업·자원 규칙 | 표준 C#만 |
| Runtime | 초기화, Tick 전달, 명령 전달, 재시작 | Simulation, Unity |
| Presentation | 입력, 맵·정착민·UI 표시 | Runtime, 읽기용 Simulation 정보, Unity |
| EditMode 테스트 | 상태와 규칙 | Simulation, 테스트 라이브러리 |
| PlayMode 테스트 | 장면 연결과 수명 | Runtime, Presentation, 테스트 라이브러리 |

각 영역을 asmdef로 구분한다. Simulation은 No Engine References를 켜 UnityEngine 의존을 컴파일 단계에서 막는다. 데이터·서비스가 화면을 역참조하지 않는다.

시뮬레이션은 정착민 ID·논리 위치·자원·욕구·작업 진행·시간을 소유한다. 화면은 Transform·Sprite·애니메이션·선택·카메라를 소유한다. 게임 결과는 애니메이션이나 Transform을 읽어 결정하지 않는다.

초기에는 읽기 전용 상태 조회로 화면을 갱신한다. 매 프레임 월드 전체 복사나 범용 이벤트 시스템은 도입하지 않는다. 변경 알림은 필요가 확인되면 추가한다.

## 명령과 데이터

- UI는 상태를 직접 수정하지 않고 명령을 전달한다. Simulation이 유효성을 검사한다.
- 안정적인 게임 ID를 사용하고 Unity Instance ID를 저장용 ID로 사용하지 않는다.
- 동일 시점 명령과 작업 처리 순서를 명시한다. 난수 시드만으로 재현성이 보장되지는 않는다.
- ScriptableObject는 설정·시나리오 원본이며 실행 시 일반 C# 데이터로 변환한다. 실행 상태로 원본 에셋을 수정하지 않는다.
- 테스트는 같은 초기 데이터 형식을 코드로 구성한다. 초기 콘텐츠 편집기는 만들지 않는다.

## 시간과 수명

일정한 논리 Tick으로 진행한다. 초기 1배속에서는 현실 시간 1초당 10 Tick을 시험한다. 게임 내 하루 길이는 별도 미정이다. Runtime은 unscaledDeltaTime에 1배속/3배속을 적용해 전달하고 순수 C# SimulationSession이 decimal 잔여 시간을 누적해 0.1초당 Tick 수를 계산한다. 테스트는 주어진 시간 또는 Step으로 직접 진행한다. 실제 프레임 시간은 float 측정값이므로 현실 시계와 정확히 같은 시각에 Tick이 바뀐다는 보장은 하지 않는다.

Time.timeScale을 유일한 시간 기준으로 삼지 않는다. 일시정지·배속은 전달 게임 시간으로 제어한다. 프레임당 처리량 제한이 있더라도 남은 시간은 이월하며 Tick을 조용히 버리지 않는다. 일시정지와 단일 Tick, 1/3배속을 구현했다. 이동은 배속과 무관하게 5 Tick/칸이다.

재시작은 새 World를 만들고 잔여 시간·이동/통행 명령·경로·선택을 초기화하며 pause와 선택 배속은 유지한다. UI는 marker와 경로 dot을 재사용하고 버튼 리스너를 한 번만 등록한다. 명령 대기열은 World가 소유하며 상태 이벤트 구독은 없다. 월드를 전역 정적 객체로 보관하지 않는 것을 기본으로 한다. Play Mode는 도메인·장면 재로드를 모두 수행하도록 고정했다.

## 시험 장면과 저장 방향

0-A는 템플릿 장면으로 시작·컴파일·Play Mode를 확인한다. 0-C 이후 SimulationSandbox에 실행 진입점, 카메라, 작은 맵, 정착민 표시, 상태 UI, 재시작 버튼, EventSystem을 연결한다. 작업 0에서 생활이나 이동은 구현하지 않는다.

작업 7의 저장은 상태·물자·진행도를 보존하고 경로·예약은 복원 시 재검증하는 안을 따른다. 세부 저장 포맷은 그 단계에서 확정한다.

초기에는 ECS, Jobs, Burst, Addressables, 외부 DI, 별도 .NET 테스트 프로젝트를 도입하지 않는다. 성능이나 실제 요구가 생기면 근거를 기록하고 검토한다.

## 병렬 구현 경계

[ParallelWork.md](ParallelWork.md)의 계약과 소유권을 따른다. B의 초기 데이터·읽기 상태 계약뿐 아니라 Runtime 초기화·Tick·Reset 의미를 정한 뒤 C/D를 나누어 작성한다. D의 Simulation 수정은 B 담당 종료 후 명시적으로 인계한다. Scene·Bootstrap·Content·공유 asmdef는 통합 담당 단독 소유다. 검증 도구는 CLI 방식이며 Computer Use에 의존하지 않는다.

구현된 초기 상태 API와 검증 규칙은 [초기 월드 계약](Contracts/InitialWorld.md)을 따른다. 현재 확장은 [이동·시간 계약](Contracts/MovementTime.md)을 따른다. 불변 ColonistSeed와 실행 ColonistState를 분리하고 World마다 위치·경로·진행·명령이 독립이다. Tick마다 통행 변경과 경로 재검사, 이동 명령, ID순 이동을 수행한다. 생활·생산 상태는 후속 범위다.

화면은 같은 World의 이동 위치를 ID로 갱신하며 통행 revision 변경 시 타일을 갱신한다. 선택·목적지·예약 테두리와 경로 dot은 WorldMarker와 분리한다. BFS 경로는 동·북·서·남 순서이며 원점 제외·목적지 포함이다. Presentation의 InputSystem 단축키와 EventSystem 맵 클릭은 같은 Runtime 명령 경로를 사용한다.

## 생활 상태 확장

World는 ResourceState/BedState와 정착민 욕구·생활 상태를 독립 소유한다. seed는 불변이다. Simulation의 외부변경→욕구→기존행동완료→예약/결정→이동/도착 순서를 유지한다. Runtime 기본은 LivingScenario, 이동 회귀는 Sandbox를 명시한다. Presentation은 읽기 조회와 마커 재사용만 수행한다. 상세 API·소유권은 [생활 계약](Contracts/Living.md), 결과는 [생활 검증](Verification/2026-11-15-living.md)을 따른다.

소집 예외: SetDrafted/QueueStop은 사용자 제어 명령으로 즉시 적용하며 Tick을 진행하지 않는다. 정착민별 IsDrafted가 자동 생활 결정을 차단한다. 식사·수면 예약과 이동 명령을 같은 World에서 정리한 뒤 수동 이동만 기존 Tick 큐를 사용한다. Release는 다음Tick 자동 결정을 허용한다.

## 단일 점유와 수면 목적

점유권은World내오브젝트에OwnerId/OwnerActionId로저장한다. Pawn의ActionId는행동취소·교체때증가하고접근→사용때유지한다. 수면목적HasSleepIntent는목표90까지유지하되명시취소/소집에서지운다. 바닥수면은가용대상변경과제한된재시도로대체침대를획득한다. UI는R토글하나와접근/사용단계를표시한다. [계약](Contracts/InteractionOccupancy.md)참조.

## 작업 3 채집·운반·저장 구현

[채집·운반·저장 계획](Plans/Task3-Gather-Haul-Storage.md)에 공통 행동 취소, 운반물·저장 칸 상태, 출발/도착 점유 전환, 수량 집계와 Tick 순서의 확장안을 정리했다. 상태 변경은 Simulation에 두고 Runtime은 명령 전달, Presentation은 선택·미리보기·표시를 맡는다. API·파일 소유권은 [작업3 계약](Contracts/GatherHaulStorage.md)으로 확정했다. 생활/노동 공통 정리, 저장 칸과 보관 스택의 공유 권한, 물자 이전과 소비의 별도 집계를 구현했다. 기존 시나리오는 유지하고 기본 실행에 WorkScenario를 사용한다.

## 식별과 하단 선택 패널

[표시 계약](Contracts/ReadableSettlementUI.md)에 따라 Presentation만 월드 읽기 상태를 시각화한다. MapSymbolGraphic은 종류별 벡터 메시, SettlementView.Inspection은 ID 색상/상시 이름/하단 정보/점유 대상 연결, Work 부분은 도구와 저장 바닥·외곽선을 담당한다. Simulation·Runtime의 규칙은 변경하지 않는다. 한글은 Windows 시스템 글꼴로 표시하고 글꼴 파일을 배포하지 않는다.


## 일시정지 명령 갱신 (2026-11-15)

[즉시 명령 계약](Contracts/ImmediatePlayerCommands.md)에 따라 플레이어 설정·취소를 Tick과 분리한다. 자동 행동 선택과 진행은 Tick에서만 수행한다. [검증 기록](Verification/2026-11-15-immediate-commands.md)은 무시간 상태/표시 검사와 Windows 실행을 구분한다.
