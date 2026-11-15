# 초기 월드 공통 계약 v1

2026-11-15, 0-B/0-E1 구현 기준. Tick·Runtime·화면은 이번 범위 밖이다.

## 공개 API (namespace RimworldTweak.Simulation)

- readonly struct GridPosition(int x, int y): X, Y getter, 값 동등성.
- enum ResourceKind { Wood, Ingredients, Meal }
- sealed class ColonistSeed(int id, GridPosition position): Id, Position getter만.
- sealed class ResourceSeed(int id, ResourceKind kind, int amount, GridPosition position): Id, Kind, Amount, Position getter만.
- sealed class ScenarioDefinition(int width, int height, IEnumerable<GridPosition> blockedTiles, IEnumerable<ColonistSeed> colonists, IEnumerable<ResourceSeed> resources): 입력을 생성 즉시 복사하며 컬렉션 getter는 IReadOnlyList<T>이며 모든 getter는 읽기 전용. null 컬렉션은 ArgumentNullException.
- sealed class WorldState: static WorldState Create(ScenarioDefinition scenario), Width, Height, BlockedTiles, Colonists, Resources 읽기 전용 getter. Create에서 재복사하며 Unity 의존 없음. 공개 mutable 상태 없음.
- sealed class ScenarioValidationException : ArgumentException: Code getter(string).
- static class SandboxScenario: static ScenarioDefinition Create(): 8×8, blocked (3,3), colonist ID 1 at (1,1), ID 2 at (2,1), ID 3 at (1,2); resource ID 101 Wood 40 at (4,1), ID 102 Meal 18 at (4,2). 실제 생활용 밸런스 변경 아님.

## 유효성·순서

Create는 width/height>0 (InvalidDimensions), ID>0 (InvalidId), 개체 전체에서 중복 ID 금지 (DuplicateId), 좌표 범위 [0,width)×[0,height) (OutOfBounds), 양수 수량 (InvalidAmount), 정의된 ResourceKind (InvalidResourceKind), null 개체 (NullEntity), blocked 중복 (DuplicateBlockedTile), blocked 위 개체 금지 (BlockedPosition)를 검사한다. null scenario는 ArgumentNullException. 오류 하나면 완성 월드를 반환하지 않는다. 여러 오류의 우선순위는 보장하지 않는다. 서로 다른 정상 개체의 같은 타일 점유는 B에서 금지하지 않는다.

개체는 ID 오름차순, blocked는 Y→X 순서로 정규화한다. 입력 배열·리스트를 나중에 수정해도 시나리오와 두 월드는 변하지 않아야 한다. 노출 컬렉션을 캐스팅해도 쓰기 불가, 요소는 불변. 현재 생성 시 독립성만 검증하며 Tick 진행 후 독립성은 0-D에서 검증한다.

## 후속 Runtime 계약 (아직 구현하지 않음)

Runtime은 초기 시나리오를 유지하고 Reset 시 새 월드를 생성해 표시층에 재연결한다. Simulation은 UnityEngine·시간 API에 의존하지 않는다. 0-D에서 정수 Tick과 Step을 추가하며 B 파일 소유권을 인계받는다. 현재 화면·Tick을 위한 가짜 구현을 추가하지 않는다.

## 소유권

- simulation_b: Assets/Game/Simulation/*.cs와 해당 .meta만 수정.
- verification_e1: Assets/Game/Tests/EditMode/Simulation/*.cs 및 Tools/*와 해당 .meta만 수정.
- root: 이 계약, 모든 asmdef·폴더 meta, 문서, 최종 통합 수정 및 Unity 검사 실행.
- 검사 담당은 namespace RimworldTweak.Tests.EditMode의 InitialWorldTests를 작성하며 테스트 이름을 Tools/expected-editmode-tests.json에 명시한다.
- 병렬 작성 종료를 각 담당자가 보고한 뒤 입력 쓰기를 동결하고 root만 Unity를 실행한다. 공동 파일 수정 전 root에 변경 요청.

## 구현 인계

0-B/0-E1 구현과 실제 검사 완료 후 양 담당자의 쓰기 동결을 확인했고 소유 파일을 root로 인계했다. 위 배정은 이번 병렬 구현 이력이다. C/D 착수 시 후속 담당 파일을 다시 명시하며 같은 파일의 동시 수정은 금지한다. 결과는 [검증 기록](../Verification/2026-11-15-0B-0E1.md)을 따른다.
