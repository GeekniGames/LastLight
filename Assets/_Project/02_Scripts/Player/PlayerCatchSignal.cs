using System;

namespace LastLight.Core
{
    /// <summary>
    /// 플레이어가 몬스터에게 잡히는 흐름에서 쓰는 신호 두 가지.
    /// - CatchStarted: 붙잡히는 연출이 시작되는 순간(아직 게임오버는 아님). UI를 잠그는 용도.
    /// - Caught: 연출이 끝나 게임오버가 확정된 순간.
    /// 어떤 몬스터 타입이든 이 신호만 Raise하면 되고, GameManager/UI가 한 곳에서 구독해 처리한다.
    /// </summary>
    public static class PlayerCatchSignal
    {
        public static event Action CatchStarted;
        public static event Action Caught;

        public static void RaiseCatchStarted() => CatchStarted?.Invoke();
        public static void Raise() => Caught?.Invoke();
    }
}