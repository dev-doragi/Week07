using System;

public enum GameLogEventType
{
    // Game flow
    GameStart,
    GameEnd,
    StageStart,
    StageEnd,
    WaveStart,
    WaveEnd,
    Pause,
    Resume,

    // Resource
    ResourceGain,
    ResourceSpend,
    SacrificeGain,
    SacrificeSpend,

    // Placement / Build
    UnitPlaced,
    UnitRemoved,
    FacilityPlaced,
    FacilityRemoved,
    BuildStarted,
    BuildCompleted,
    BuildCancelled,

    // Combat
    AttackStarted,
    ProjectileSpawned,
    ProjectileHit,
    DamageDealt,
    DamageReceived,
    UnitKilled,
    StructureDestroyed,

    // Skill / Ritual
    SkillUsed,
    RitualUsed,
    BuffApplied,
    BuffExpired,

    // Ram
    RamStarted,
    RamHit,
    RamEnded,

    // Doctrine / Unlock
    DoctrineSelected,
    UnitUnlocked,
    FacilityUnlocked,
    RitualUnlocked,

    // UI / Selection
    ButtonClicked,
    ShopOpened,
    ShopClosed,
    MapNodeSelected,
    RewardSelected
}
