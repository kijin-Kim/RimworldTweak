# RimworldTweak

정착지 운영으로 방어를 준비하고, 정찰과 전술로 피해 없이 습격을 해결하는 콜로니 시뮬레이션 프로젝트. 이름은 작업 폴더명이며 최종 게임명은 미정이다.

## 문서부터 읽기

| 문서 | 역할 |
|---|---|
| [현재 상태](Docs/Status.md) | 완료·진행·미검증 상태와 다음 작업 |
| [게임 기획](Docs/GameDesign.md) | 게임의 방향과 전투·정보전 원칙 |
| [1단계 상세 기획](Docs/Settlement.md) | 생활과 생산의 규칙 및 초기 수치 |
| [개발 작업](Docs/Roadmap.md) | 기능 단위 작업 순서와 완료 기준 |
| [병렬 작업 계획](Docs/ParallelWork.md) | 병렬 묶음, 선행 계약, 소유권과 통합 순서 |
| [초기 월드 계약](Docs/Contracts/InitialWorld.md) | 공개 데이터 API, 유효성·격리 규칙과 소유권 |
| [구조 설계](Docs/Architecture.md) | Unity 프로젝트와 시뮬레이션·화면 경계 |
| [설치와 실행](Docs/Setup.md) | 에디터 선택, 설치 후 프로젝트 생성 절차 |
| [검증 계획](Docs/Testing.md) | 자동 검사, 화면 검사, 플레이 검증 |
| [결정 기록](Docs/Decisions.md) | 확정 사항, 선택 근거, 재검토 조건 |
| [문서 운영](Docs/Workflow.md) | 문서 수정과 작업 보고 규칙 |

정착민 선택·이동·중지와 시간 조작에 더해 자동 식사·수면을 사용할 수 있다. R 한 키/버튼으로 소집·해제한다. 소집하면 현재 명령과 점유를 취소하고 플레이어 이동이 우선한다. 음식 스택과 침대는 한 Pawn만 점유하며, 바닥 수면자는 빈 침대를 다시 선택한다. [생활 데모 실행·확인](Docs/LivingDemo.md), [현재 상태](Docs/Status.md), [최신 점유 검증](Docs/Verification/2026-11-15-occupancy.md)를 참고한다. 채집·운반·저장은 이번 작업에서 구현했으며 [작업 데모](Docs/WorkDemo.md)와 [검증 상태](Docs/Status.md)를 참고한다. 농사·조리·건설·전투·파일 저장은 아직 없다.

공유 폴더 유지, Computer Use·원격 연결·push 금지. 기존 기준점 `7d20688` 이후 채집·운반·저장, 표시 개선, 정지 명령 처리를 사용자 승인된3개 커밋으로 정리했다. [중간 상태·최종 검증](Docs/Verification/2026-11-15-feature-commits.md)을 참고하며 이후 추가 커밋은 다시 승인받는다.

[생활 계획](Docs/Plans/Task2-Needs-Food-Sleep.md) · [생활 계약/파일 소유권](Docs/Contracts/Living.md) · [기존 이동 검증](Docs/Verification/2026-11-15-movement.md)

이동·시간 조작은 사용자 정상 확인을 받았다. 생활·소집·점유도 자동 검증 및 사용자 정상 확인 보고를 받았다.

[R 토글·단일 점유 계획](Docs/Plans/Interaction-Occupancy-Draft.md)과 [공통 계약](Docs/Contracts/InteractionOccupancy.md)에 따라 구현·자동 검증했다. 2026-11-15 사용자 정상 확인 보고를 받았다.

[채집·운반·저장 계획](Docs/Plans/Task3-Gather-Haul-Storage.md)을 승인받아 [공통 계약](Docs/Contracts/GatherHaulStorage.md)에 따라 구현했다. [검증 기록](Docs/Verification/2026-11-15-work.md), [새 조작 확인](Docs/WorkDemo.md)을 따른다.

[식별·하단 선택 UI](Docs/ReadableUiDemo.md): 정착민 고정 색상/상시 이름, 종류별 오브젝트 모양, 저장 구역 바닥·외곽선, 하단 행동·운반·목적지·점유 정보를 제공한다. [공통 계약](Docs/Contracts/ReadableSettlementUI.md)과 [검증 기록](Docs/Verification/2026-11-15-readable-ui.md)을 따른다.

정지 중 소집·취소·필터·지정 변경도 즉시 적용하고 저장 구역 생성 성공 후 도구를 해제한다. 관련 기능은 사용자 정상 확인을 받았다. [즉시 명령 계약](Docs/Contracts/ImmediatePlayerCommands.md)을 따른다.


## 공통 자동 작업 기반

2026-11-16 기능별 커밋 1: 작업 후보 조회·시작·취소와 오래된 명령 거부를 연결했다. 건설·재배·조리·개인 설정·파일 저장은 후속 커밋이다. 중간 검사 결과는 [커밋 검증 기록](Docs/Verification/2026-11-16-four-feature-commits.md)을 따른다.


## 건설·재배 통합

자재 반입·완공·철거, 파종·성장·수확, 미소집 수동 이동 차단과 건설 현장 표시를 구현했다. 조리·개인 설정·파일 저장은 아직 후속 단계다. 기능별 커밋 검증 기록을 따른다.
