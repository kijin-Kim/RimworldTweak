# Unity 설치와 실행 — 작업 0-A

2026-11-15 작업 0-A 완료. 실제 검사 결과는 [검증 기록](Verification/2026-11-15-0A.md), 현재 진행 상태는 [Status.md](Status.md)를 따른다.

## 고정한 환경

- 프로젝트 루트: `E:\Dev\RimworldTweak`.
- Unity: `6000.3.28f1` / revision `8f082d2fe476`.
- 에디터: `C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe`.
- 원본 템플릿: 에디터 번들 Universal 2D `6.1.6`.
- 생성된 장면: `Assets/Scenes/SampleScene.unity`.
- 현재 그래픽 API: DX12. 카메라는 Orthographic, URP의 Renderer2D를 사용한다.
- Force Text, Visible Meta Files, Input System 활성화.
- Play Mode 진입 시 Domain Reload와 Scene Reload를 모두 수행한다.

## 패키지 버전

| 패키지 | 고정 버전 |
|---|---|
| Universal Render Pipeline | 17.3.0 |
| Input System | 1.20.1 |
| Test Framework | 1.6.0 |
| uGUI | 2.0.0 |

모든 직접 의존성은 [manifest.json](../Packages/manifest.json), 전이 의존성은 [packages-lock.json](../Packages/packages-lock.json)을 기준으로 한다. 직접 의존성 52개와 해석된 의존성 65개를 확인했다. 번들 템플릿의 패키지를 에디터가 호환 버전으로 해석·갱신한 뒤 고정했으며 원본 tgz의 오래된 버전을 강제로 되돌리지 않았다.

템플릿에 포함된 2D 도구, IDE 연동, Visual Scripting, Timeline 등의 패키지는 유지했다. 이 기능을 게임에 도입하거나 코드로 사용한 것은 아니다. 자동 업데이트를 가정하지 않으며 이후 버전 변경은 manifest와 lock의 변경을 함께 검토한다.

## 이후 실행 정책

사용자 지시에 따라 이후 에이전트는 Computer Use를 사용하지 않는다. 에이전트의 검사는 CLI와 테스트/Editor 스크립트로 진행하며 같은 프로젝트의 Unity 실행은 직렬화한다. GUI가 이미 열려 있으면 강제 종료하지 않고 정상 종료를 조율한다. 아래 GUI 안내는 사용자가 직접 확인할 때의 절차이며 에이전트 UI 자동화 절차가 아니다. 0-A의 과거 화면 검증 기록은 그대로 보존한다.

## 프로젝트 열기

Unity Hub에서 Add project from disk로 루트 폴더를 등록하거나 다음 명령으로 연다. 등록은 이번 작업에서 수행하지 않았다.

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe' -projectPath 'E:\Dev\RimworldTweak' -logFile 'E:\Dev\RimworldTweak\Logs\editor.log'
```

1. 패키지 해석과 컴파일 완료를 기다린다.
2. `Assets/Game/Scenes/SimulationSandbox.unity`를 연다.
3. Play 버튼 또는 Ctrl+P로 실행하고 Game 탭을 확인한다.
4. 다시 실행을 중지하고 Console을 확인한다.

초기 템플릿 SampleScene은 보존했다. 데모의 진입 장면은 SimulationSandbox이며 초기 정착지와 선택·이동·목적지·경로, Tick·Pause·1/3배속·Step·Reset을 표시한다. 실행 파일과 수동 확인 절차는 [MovementDemo.md](MovementDemo.md)를 따른다.

## 버전과 설정 관리

- [ProjectVersion.txt](../ProjectSettings/ProjectVersion.txt)를 함께 보존한다.
- 패키지 파일 두 개와 Assets의 .meta, ProjectSettings를 버전 관리 대상에 포함한다.
- Library, Temp, Logs, UserSettings는 .gitignore로 제외한다.
- 템플릿 기본값의 Domain Reload 생략을 해제했다. `m_EnterPlayModeOptionsEnabled: 0`, `m_EnterPlayModeOptions: 0`을 유지한다.
- 모든 Quality 레벨이 `Assets/Settings/UniversalRP.asset`을 사용하며 기본 Renderer는 `Renderer2D.asset`이다. Graphics의 전역 기본값은 템플릿처럼 비어 있으나 Quality override로 URP가 적용된다.

## 검증 범위

프로젝트 기반과 초기 월드의 과거 결과는 [0-A](Verification/2026-11-15-0A.md), [0-B/E1](Verification/2026-11-15-0B-0E1.md)에 남아 있다. 현재 Windows 데모 결과는 Status 및 후속 검증 기록을 따른다. 최초 기준 커밋은 784f242이며 이번 후속 변경은 커밋하지 않는다. 공유 폴더 유지, 원격 연결·push 금지, 후속 커밋 재승인 정책을 유지한다.

최신 실행 파일은 자동 식사·수면이 포함된 생활 데모다. [실행 및 사람 확인](LivingDemo.md), [검증 기록](Verification/2026-11-15-living.md)을 따른다.
