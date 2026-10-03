using LastLight.Maze;
using UnityEngine;

namespace LastLight.Debugging
{
    /// <summary>
    /// 임시 테스트용 스크립트. Play 모드 진입 시 지정한 층수로 미로를 즉시 생성한다.
    /// 7단계(게임 루프)에서 정식 층 진행 로직으로 교체되면 이 스크립트는 제거해도 된다.
    /// </summary>
    public class MazeTestBootstrap : MonoBehaviour
    {
        [SerializeField] private MazeBuilder mazeBuilder;
        [SerializeField] private int testFloor = 1;

        private void Start()
        {
            mazeBuilder.BuildFloor(testFloor);
        }
    }
}