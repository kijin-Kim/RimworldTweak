# RimworldTweak

현재 개발 기준은 이 폴더의 통합 작업본이다. 새 정착지는 250×250이며 무기 장착, 개인 우선 작업, 부상자 구조가 가능하다. 개발자 모드에서 일반 습격을 즉시 시작할 수 있다. 실행 방법과 최신 검증 결과는 [현재 상태](Docs/Status.md)를 따른다.

정착지 운영으로 방어를 준비하고, 정찰과 전술로 피해 없이 습격을 해결하는 콜로니 시뮬레이션 프로젝트. 이름은 작업 폴더명이며 최종 게임명은 미정이다.

## 문서부터 읽기

일반 실행은 독립된 타이틀 화면에서 시작한다. **새 게임**을 누르면 정착지가 열리고, **불러오기**에서는 저장 이름과 진행 시점을 확인한 뒤 선택할 수 있다. [타이틀 화면과 직접 확인 순서](Docs/Verification/2026-11-19-title.md)

[정착지 아트 적용·검증](Docs/Verification/2026-11-18-settlement-art.md)에서 실제 폰·의상 조합, 흙·나무·침대·작물과 바닥 무기 기준 장면, 사람 확인 순서를 볼 수 있다.

[최신 UI 사람 확인 카드](Docs/Verification/2026-11-17-manual-acceptance.md): 기존 저장과 분리된 실행 명령, 실제 한글 IME·탐색·작업·아트 확인 순서. 자동 검사와 사람 확인은 별도다.

[림월드식 기본 전투 우선 계획](Docs/Plans/Combat-Baseline-Incremental.md): 유지할 지휘·사격·엄폐·무기 역할·부상/복귀부터 확인하고, 이후 확장을 하나씩 선택하는 현재 전투 기획. 전체 확장과 구현 착수는 미확정이다.

[기본 전투 기능 분리·병렬 계획](Docs/Plans/Combat-Baseline-Parallel.md): 건강/부상/회복, 방어구, 구조/치료를 포함한 기능별 선행·공통 자료·파일 소유권·통합 검증. B1~B5는 수락 묶음이며 실제 배정은 이 계획을 참고한다. 현재 실행 전 제안이다.

[림월드식 UI 구현 인계](Docs/Plans/RimWorld-UI-Handoff.md)에서 카메라·HUD 배치, 건축가·대상별 명령, 기존 기능 연결과 검증 순서를 확인할 수 있다. 관찰 근거와 공식 캡처는 [림월드 PC 기본판 화면 분석](Docs/Reviews/RimWorld-UI-Reference.md)에 있다.

[준비형 전투·공격 원정·보급 초안](Docs/Plans/TacticalCombat-Offense-Logistics.md): 전술 상성·전쟁 사례, 개별 지휘와 새 전투 시스템 비교, 공격/철수·보급·Fog of War의 연결. 논의용 제안이며 구현·규칙 확정은 아니다.

[저장·UI·아트 요구사항과 완료·검증 기준](Docs/Plans/SaveLoad-UX-Art-Acceptance.md): 기능별 입력·기대 결과, 저장 실패 보존, 기존 명령 이관, 아트 교체와 실제 화면 확인 기준. 구현·검증은 진행 중이며 최신 결과는 Status와 진행 검증 기록을 따른다.

[프로토타입 우선 구현과 성능 개선 지침](Docs/Plans/RimWorld-Performance-Application.md): 기존 책임·상태 경계만 유지하며 기능을 먼저 구현하고, 프로토타입 실행 측정 뒤 필요한 개선만 선택한다. 캐시·전용 계측·검색 최적화는 선행 작업이 아니다.

현재 후속 계획은 [저장·불러오기, UI·조작, 탑다운 2D 아트](Docs/Plans/SaveLoad-UX-Art.md)다. 2026-11-17 사용자 요청에 따라 자급 정착지 플레이테스트를 이번 범위에서 제외했다. 신규 화면·아트의 구현과 검증이 진행 중이며 기능별 실제 검증 상태는 Status를 따른다.

| 문서 | 역할 |
|---|---|
| [아트 작업 인계](Docs/Art/Handoff.md) | 다른 세션의 시작점: 범위·도구·로컬 원본·주의사항·검증·다음 작업 |
| [조리·작업계획 검증](Docs/Verification/2026-11-15-cooking-work-policy.md) | 새 기능·검사·직접 확인 순서 |
| [저장·불러오기 검증](Docs/Verification/2026-11-16-save-load.md) | 파일 왕복·진행 복원·직접 확인 |
| [현재 상태](Docs/Status.md) | 완료·진행·미검증 상태와 다음 작업 |
| [게임 기획](Docs/GameDesign.md) | 게임의 방향과 전투·정보전 원칙 |
| [1단계 상세 기획](Docs/Settlement.md) | 생활과 생산의 규칙 및 초기 수치 |
| [개발 작업](Docs/Roadmap.md) | 기능 단위 작업 순서와 완료 기준 |
| [병렬 작업 계획](Docs/ParallelWork.md) | 병렬 운영 원칙과 기존 배정 이력 |
| [현재 3세션 계획](Docs/Plans/SettlementFiveFeatures.md) | 다섯 기능의 담당·파일 소유권·통합/완료 기준 |
| [조작·건설 표시 수정](Docs/Verification/2026-11-15-player-control-construction-ui.md) | 미소집 이동 차단·선택 작업자의 건설 현장 정보 |
| [건설·재배 검증](Docs/Verification/2026-11-15-construction-farming.md) | 실행 결과·새 조작·사용자 확인 순서 |
| [G0-C 검증·인계](Docs/Verification/2026-11-15-g0-c.md) | 구현된 공통 접점·회귀 결과·기능 세션 수신 기준 |
| [G0 준비 확정서](Docs/Plans/G0-Preparation.md) | 공간·API·수치·조작·저장 규칙, 명세/실행 구분 |
| [세션 지시서](Docs/Plans/Sessions/Integration.md) | 통합S0, 기능SA/SB의 실제 인계 절차 |
| [전체 세션 로드맵](Docs/Plans/MultiSessionRoadmap.md) | 후반부를 포함한 장기 참고 계획 |
| [현재 공통 계약](Docs/Contracts/SettlementFiveFeatures.md) | 건설·재배·조리·작업계획·저장의 공통 접점 |
| [초기 월드 계약](Docs/Contracts/InitialWorld.md) | 공개 데이터 API, 유효성·격리 규칙과 소유권 |
| [구조 설계](Docs/Architecture.md) | Unity 프로젝트와 시뮬레이션·화면 경계 |
| [설치와 실행](Docs/Setup.md) | 에디터 선택, 설치 후 프로젝트 생성 절차 |
| [검증 계획](Docs/Testing.md) | 자동 검사, 화면 검사, 플레이 검증 |
| [결정 기록](Docs/Decisions.md) | 확정 사항, 선택 근거, 재검토 조건 |
| [문서 운영](Docs/Workflow.md) | 문서 수정과 작업 보고 규칙 |

정착민 선택·이동·중지와 시간 조작에 더해 자동 식사·수면을 사용할 수 있다. R 한 키/버튼으로 소집·해제한다. 소집하면 현재 명령과 점유를 취소하고 플레이어 이동이 우선한다. 음식 스택과 침대는 한 Pawn만 점유하며, 바닥 수면자는 빈 침대를 다시 선택한다. [생활 데모 실행·확인](Docs/LivingDemo.md), [현재 상태](Docs/Status.md), [최신 점유 검증](Docs/Verification/2026-11-15-occupancy.md)를 참고한다. 채집·운반·저장은 이번 작업에서 구현했으며 [작업 데모](Docs/WorkDemo.md)와 [검증 상태](Docs/Status.md)를 참고한다. 건설·재배가 연결되었고 조리·개인 작업계획의 최신 검증은 위 기록을 따른다. 파일 저장·불러오기는 최신 저장 검증 기록을 따른다. 전투는 아직 없다.

공유 폴더 유지, Computer Use·원격 연결·push 금지. 기존 기준점 `7d20688` 이후 채집·운반·저장, 표시 개선, 정지 명령 처리를 사용자 승인된3개 커밋으로 정리했다. [중간 상태·최종 검증](Docs/Verification/2026-11-15-feature-commits.md)을 참고하며 이후 추가 커밋은 다시 승인받는다.

[생활 계획](Docs/Plans/Task2-Needs-Food-Sleep.md) · [생활 계약/파일 소유권](Docs/Contracts/Living.md) · [기존 이동 검증](Docs/Verification/2026-11-15-movement.md)

이동·시간 조작은 사용자 정상 확인을 받았다. 생활·소집·점유도 자동 검증 및 사용자 정상 확인 보고를 받았다.

[R 토글·단일 점유 계획](Docs/Plans/Interaction-Occupancy-Draft.md)과 [공통 계약](Docs/Contracts/InteractionOccupancy.md)에 따라 구현·자동 검증했다. 2026-11-15 사용자 정상 확인 보고를 받았다.

[채집·운반·저장 계획](Docs/Plans/Task3-Gather-Haul-Storage.md)을 승인받아 [공통 계약](Docs/Contracts/GatherHaulStorage.md)에 따라 구현했다. [검증 기록](Docs/Verification/2026-11-15-work.md), [새 조작 확인](Docs/WorkDemo.md)을 따른다.

[식별·하단 선택 UI](Docs/ReadableUiDemo.md): 정착민 고정 색상/상시 이름, 종류별 오브젝트 모양, 저장 구역 바닥·외곽선, 하단 행동·운반·목적지·점유 정보를 제공한다. [공통 계약](Docs/Contracts/ReadableSettlementUI.md)과 [검증 기록](Docs/Verification/2026-11-15-readable-ui.md)을 따른다.

정지 중 소집·취소·필터·지정 변경도 즉시 적용하고 저장 구역 생성 성공 후 도구를 해제한다. 관련 기능은 사용자 정상 확인을 받았다. [즉시 명령 계약](Docs/Contracts/ImmediatePlayerCommands.md)을 따른다.
