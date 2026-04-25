/// <summary>
/// 씬 전환 시 스테이지 정보를 안전하게 전달하기 위한 정적 데이터 컨텍스트입니다.
/// 새로운 씬의 StageManager가 데이터를 소비하는 구조로 동작합니다.
/// </summary>

public static class StageLoadContext
{
    private static int _stageIndex = -1;
    private static bool _isTutorial = false;

    public static bool HasValue => _stageIndex != -1 || _isTutorial;
    public static bool IsTutorial => _isTutorial;

    public static void SetStageTutorial()
    {
        _stageIndex = -1;
        _isTutorial = true;
    }

    public static void SetStageIndex(int index)
    {
        _stageIndex = index;
        _isTutorial = false;
    }

    public static int GetStageIndex()
    {
        int value = _stageIndex != -1 ? _stageIndex : 0;
        _stageIndex = -1;
        // 튜토리얼 플래그는 명시적으로 TutorialClear()가 호출되기 전까지 유지
        return value;
    }

    /// <summary>
    /// 튜토리얼이 종료되면 명시적으로 호출하여 컨텍스트를 초기화합니다.
    /// </summary>
    public static void TutorialClear()
    {
        _stageIndex = -1;
        _isTutorial = false;
    }
}