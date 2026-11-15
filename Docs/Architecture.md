# 구조 설계

0-A 프로젝트 기반, 0-B Simulation 및 Editor 테스트 어셈블리, 0-E1 CLI 실행기를 구현했다. Runtime·Presentation과 Play Mode 통합은 아직 구현 전이다.

## Unity 구성

Unity 6.3 LTS 6000.3.28f1 설치와 ProjectVersion.txt 고정을 확인했다. Universal 2D, 직교 카메라, Windows 데스크톱을 기준으로 시작한다. Input System, uGUI, Unity Test Framework를 사용하고 패키지는 해석 후 manifest와 lock에 고정했다. 실제 버전은 [Setup.md](Setup.md)를 따른다.

Visible Meta Files와 Force Text를 사용한다. 에디터 버전은 ProjectSettings/ProjectVersion.txt, 패키지는 Packages/manifest.json과 생성된 packages-lock.json이 실제 기준이다. 버전 변경은 검증 후 기록한다.

## 폴더 구성 (Runtime·Presentation·Content·시험 장면·PlayMode는 예정)

```text
Assets/Game/
  Simulation/
  Runtime/
  Presentation/
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

일정한 논리 Tick으로 진행한다. 초기 1배속에서는 현실 시간 1초당 10 Tick을 시험한다. 게임 내 하루 길이는 별도 미정이다. Runtime이 경과 시간을 누적해 Tick 수를 계산하고 테스트는 현실 시간을 기다리지 않고 직접 진행한다.

Time.timeScale을 유일한 시간 기준으로 삼지 않는다. 이후 일시정지·배속은 전달 Tick 수로 제어한다. 프레임당 처리량 제한이 있더라도 남은 시간은 이월하며 Tick을 조용히 버리지 않는다. 이 정책의 실제 구현·검증은 0-D와 작업 1에서 한다.

재시작은 월드, 누적 시간, 명령 대기열, 표시 대상, UI 구독을 정리하고 새로 만든다. 월드를 전역 정적 객체로 보관하지 않는 것을 기본으로 한다. Play Mode는 도메인·장면 재로드를 모두 수행하도록 고정했다.

## 시험 장면과 저장 방향

0-A는 템플릿 장면으로 시작·컴파일·Play Mode를 확인한다. 0-C 이후 SimulationSandbox에 실행 진입점, 카메라, 작은 맵, 정착민 표시, 상태 UI, 재시작 버튼, EventSystem을 연결한다. 작업 0에서 생활이나 이동은 구현하지 않는다.

작업 7의 저장은 상태·물자·진행도를 보존하고 경로·예약은 복원 시 재검증하는 안을 따른다. 세부 저장 포맷은 그 단계에서 확정한다.

초기에는 ECS, Jobs, Burst, Addressables, 외부 DI, 별도 .NET 테스트 프로젝트를 도입하지 않는다. 성능이나 실제 요구가 생기면 근거를 기록하고 검토한다.

## 병렬 구현 경계

[ParallelWork.md](ParallelWork.md)의 계약과 소유권을 따른다. B의 초기 데이터·읽기 상태 계약뿐 아니라 Runtime 초기화·Tick·Reset 의미를 정한 뒤 C/D를 나누어 작성한다. D의 Simulation 수정은 B 담당 종료 후 명시적으로 인계한다. Scene·Bootstrap·Content·공유 asmdef는 통합 담당 단독 소유다. 검증 도구는 CLI 방식이며 Computer Use에 의존하지 않는다.

구현된 초기 상태 API와 검증 규칙은 [초기 월드 계약](Contracts/InitialWorld.md)을 따른다. WorldState는 현재 초기 snapshot이며 시간 진행과 mutable 게임 상태는 0-D 이후 범위다.
