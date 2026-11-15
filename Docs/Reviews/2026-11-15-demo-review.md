# Windows 데모 병렬 구현과 교차 검토

날짜: 2026-11-15. 범위 0-C/D/E2/F. 구현 성공은 아래 의견이 아니라 실제 검증 기록으로 판정한다.

## 계약과 소유권

[DemoRuntime 계약](../Contracts/DemoRuntime.md)을 작성한 뒤 Runtime, Presentation, 테스트 담당에게 겹치지 않는 파일을 배정했다. 기존 WorldState 변경은 Runtime 담당에게 인계했다. root는 asmdef, Scene, Bootstrap, Editor 빌드, CLI 도구와 문서를 맡고 모든 Unity 실행을 직렬 수행한다. 각 담당은 구현 완료 후 쓰기를 동결했다.

## 검토 결과와 반영

| 검토 | 판단 | 최종 반영 |
|---|---|---|
| Runtime | decimal 누적과 checked Step은 실패 전 상태 보존, Reset은 새 World | 분할 시간과 overflow, 세션 간 독립성 검사 유지 |
| 화면/테스트 | 실제 Image, Text, RectTransform 계약 및 셀 중심 좌표 일치 | PlayMode에서 좌표·수량·개수 및 재연결 검사 |
| Runtime의 도구 검토 | Smoke 입력 해시만으로는 오래된 exe와 현재 소스 일치 증명 불가 | 빌드 입력 및 산출물 해시를 저장하고 Smoke에서 대조하도록 보완 |
| 테스트의 smoke 검토 | marker metadata 검사는 실제 표시 검사와 다름 | PlayMode 실제 컴포넌트 검사와 Player PNG 검토를 함께 제시 |
| 테스트의 PNG 검토 | 파일 존재·길이만으로 이미지 완전성 증명 불가 | 고유 실행 폴더 사용, 실행 종료 후 실제 PNG 디코드·이미지 검토 |
| 공통 한계 | onClick.Invoke는 마우스 조작 증거가 아님 | 사람의 화면·조작 체크리스트를 별도 미검증으로 유지 |

이벤트 기반 상태 통지 대신 조회 방식과 재사용 marker를 선택했다. 초기 단계에서는 구독 수명 관리가 단순하고 Reset 시 UI를 다시 만들 필요가 없다. 향후 대규모 맵에서 전체 조회 비용이 실제 문제가 되면 변경 알림을 검토한다. 이번 구현은 초기 snapshot 변경 시에만 맵을 다시 그리고 Tick/HUD는 매 프레임 읽는다.

## 실패에서 확인한 사항

첫 Prepare 실행에서 BuildReport의 오류·경고 수가 int인데 기록 구조가 uint라 컴파일 실패했다. int로 맞춘 뒤 재컴파일했다. 실패 종료 후 소유 프로세스 없이 UnityLockfile이 남았다. 파일은 삭제하지 않았고, 프로세스 검사 및 독점 파일 열기로 소유자가 없음을 확인한 후 Unity 자체 재열기로 정상 회복했다. 실행 중 에디터 강제 종료는 하지 않았다.

두 번째 실패는 Run-Tests 확장 중 testPlatform 값이 문자열 치환에서 빠진 것이었다. run.json과 Unity 명령행에서 누락을 확인하고 고유 실행 경로로 식별한 해당 프로세스만 종료했다. 실패 결과(0개/결과 XML 없음)를 보존했다. 플랫폼 값을 복구하고 문자열 아닌 인자·빈 인자를 거부하도록 추가한 후 EditMode 21개와 PlayMode 5개를 실행해 통과했다.

첫 Windows 빌드는 BuildReport 오류·경고 0건이었으나 URP 필터·런타임 설정, VolumeProfile filter, Standalone 배칭의 재직렬화로 입력 해시 검사가 실패했다. 변경 내용을 검토해 안정화한 다음 빌드는 입력 변경 0건으로 통과했다.

숨긴 Player 창에서는 D3D12 PNG 저장 실패, D3D11 PNG는 검은 화면이었다. 이 둘은 유효 렌더 증거로 채택하지 않았다. 기본 D3D12 그대로 실제 데모 창을 표시하면 정상 렌더가 저장됨을 확인하여 그래픽 API를 바꾸지 않았다. Smoke에만 데모 창 표시를 적용하고, 1280×800 PNG 디코드와 표본 색 수(8개 초과) 검사로 빈 화면을 거부한다. 검은 이미지 1색은 실패, 실제 데모 70색은 통과했다. 표본 색 수 검사는 의미 있는 배치를 증명하지 않으므로 실제 PNG 검토와 PlayMode 데이터 검사를 함께 유지한다.
