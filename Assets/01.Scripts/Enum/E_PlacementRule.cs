public enum PlacementRule
{
    InitialOnly,          // 게임 시작 시에만 배치 (플레이어는 설치/제거 불가)
    NeedsFoundationBelow, // footprint 최하단 셀들 바로 아래가 전부 받침대여야 함
    NeedsAdjacent         // footprint 주변(상하좌우)에 다른 유닛이 하나라도 있어야 함
}