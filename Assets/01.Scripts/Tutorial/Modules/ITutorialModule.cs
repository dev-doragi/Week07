using System.Collections;

/// <summary>
/// 튜토리얼 스텝을 구성하는 모듈의 기본 인터페이스
/// 각 모듈은 독립적으로 Initialize → Execute → Cleanup 생명주기를 따름
/// </summary>
public interface ITutorialModule
{
    /// <summary>
    /// 모듈 초기화 (조건 설정, 이벤트 구독 등)
    /// </summary>
    void Initialize(TutorialStep step);

    /// <summary>
    /// 모듈 실행 (조건이 만족될 때까지 대기 또는 동작 수행)
    /// </summary>
    IEnumerator Execute();

    /// <summary>
    /// 모듈 정리 (리소스 해제, 이벤트 구독 해제 등)
    /// </summary>
    void Cleanup();
}