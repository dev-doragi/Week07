public enum E_UnitCategory
{
    None = 0,

    Wheel,    // 탑승체의 바퀴
    Core,     // 플레이어가 지켜야 할 핵심
    Attack,   // 공격 유닛 (포탑 등)
    Defense,  // 방어 유닛 (장갑판, 벽 등)
    Support,   // 지원 유닛 (버프 제단, 힐러 등)

    All = ~0
}