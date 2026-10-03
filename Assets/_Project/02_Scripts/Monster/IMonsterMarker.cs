namespace LastLight.Monsters
{
    /// <summary>
    /// 모든 몬스터 타입에 붙이는 빈 마커 인터페이스.
    /// 함정처럼 "이게 몬스터인지"만 판별하면 되는 쪽에서, 몬스터 종류가 늘어나도
    /// 타입을 하나하나 알 필요 없이 이 인터페이스 하나만 검사하면 되게 해준다.
    /// </summary>
    public interface IMonsterMarker
    {
    }
}