public enum InGameState
{
    None,
    Prepare,       // 그리드 배치 대기 상태
    WavePlaying,   // 웨이브 진행 상태
    WaveEnded,     // 웨이브 종료 (결과 연출 중)
    StageCleared,  // 스테이지 클리어
    StageFailed    // 스테이지 실패
}