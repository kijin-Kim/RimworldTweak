# 상호작용 점유·R 소집 공통 계약

2026-11-15 사용자 승인. [계획](../Plans/Interaction-Occupancy-Draft.md)의 I-1~6/O-01~12를 구현 기준으로 확정한다. 기존 Living 계약의 소집 입력·복수 스택 예약·바닥 수면 고정 규칙을 대체한다.

## 상태와 불변 조건

- ColonistState: IsDrafted, ActionId(long), HasSleepIntent(bool) 조회. ActionId는 각 Pawn의 행동 세대로 취소/종료/새 행동 경계에서 증가하며 접근→사용 전환에서는 유지한다. HasSleepIntent는 휴식20 이하에서 시작,90 회복 완료 또는 명시 취소/소집에서 제거. 음식 해결과 대상 변경에서는 유지.
- ResourceState: OwnerId(int?), OwnerActionId(long), IsInUse(bool). Reservations는 호환 읽기 사전으로0~1개만 포함. ReservedAmount는 확보 수량, AvailableAmount는 미확보 수량(Amount-ReservedAmount), AccessibleAmount는 소유자없을때만Amount, 나머지0. 음식스택 단일소유 강제.
- BedState: OwnerId/OwnerActionId. IsOccupied는 실제 사용중(수면) 표시이고 OwnerId가있으면 접근중도독점 상태다.
- Reserve/Release/Consume/Occupy는 PawnId+ActionId를 함께 검사한다. 이전행동의 해제/완료는 현재소유권에 영향없음. World는 명령과 오브젝트를 자기 인스턴스 안에서만 처리.
- QueueCancelLife(id)는 호출시 현재ActionId를 캡처. QueueCancelLife(id, expectedActionId)도 제공하여 늦은취소를 검사한다. 적용시 불일치하면 무시. 실제취소는 목적·현재행동·미적용이동·진행·점유를 함께 제거한다.

## 선택과 취소

- R/버튼은 ToggleDraft(id). 먼저 소집 상태 갱신 후 현재명령·점유 취소. Pause중즉시적용, Tick/욕구불변. 해제시 다음Tick새생활선택. X/Stop/Release UI와 단축키 제거.
- QueueStop은 기존 내부호환 API로 유지하나 사용자UI와연결하지 않는다. 소집중 현재타일 우클릭은 이동중단, R은 항상해제.
- 음식선택은 Meal→Ingredients, 실제경로길이→ID. 신규경쟁은Nutrition→PawnID. 침대는Rest→PawnID. 기존유효점유는유지.
- 바닥수면자와 신규수면자는 같은 경쟁대상. HasSleepIntent이면서Rest<9000이면Rest>2000이어도대상. 빈침대획득성공후에만바닥수면종료. 실패하면진행유지. availabilityRevision/위치변경또는최대10Tick재시도.
- 접근중/수면중대상삭제·차단시 목적유지. 식사개입에도수면목적유지. 회복90에서목적제거/수면완료1회. 바닥→침대전환은완료가아님. 이동중에는깨어있는욕구변화.

## 화면과 검증

DraftButton 하나: Draft [R]/Undraft [R]. 소유자와 APPROACH/USING 단계, 자원총량/확보량/타Pawn접근가능량 표시. 기존미확보량을사용가능량으로표기하지않는다.

O-01~12 검사와2400Tick생활,Reset/배속회귀,Windows build/smoke/PNG를 실행한다. 결과수치를이전4/2로고정하지않으며기아0·전Pawn생활순환·독점·보존을확인한다.

이번 파일 소유권은 단일 통합 담당(root): Simulation(상태/명령/점유), Runtime/Presentation(토글/표시), Tests/expected목록, Demo smoke, Docs. 같은저장소를수정하는 I-3/I-4는직렬구현하며Unity실행중소스동결. Computer Use·커밋·원격작업금지.
