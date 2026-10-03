namespace LastLight.Monsters
{
    /// <summary>
    /// 텔레포트 함정처럼 외부 요인으로 위치가 급격히 바뀌었을 때 알림을 받는 몬스터용 인터페이스.
    /// 구현한 몬스터는 기존 경로/추격 상태를 버리고 새 위치 기준으로 행동을 다시 시작해야 한다.
    /// (안 그러면 옮겨지기 전 위치 기준 목표를 향해 벽을 뚫고 일직선으로 움직이는 것처럼 보일 수 있음)
    /// </summary>
    public interface ITeleportAware
    {
        void OnTeleported();
    }
}