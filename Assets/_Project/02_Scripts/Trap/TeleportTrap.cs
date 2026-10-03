using System.Collections.Generic;
using UnityEngine;
using LastLight.Monsters;

namespace LastLight.Maze
{
    /// <summary>F4 텔레포트 함정. 밟으면 미로 안의 무작위 통로 칸으로 순간이동시킨다.</summary>
    public class TeleportTrap : TrapBase, IRequiresMaze
    {
        [Header("Teleport Trap")]
        [SerializeField] private MazeBuilder maze;
        [Tooltip("출구 근처(이 거리, 월드 유닛 안)로는 텔레포트되지 않는다. 출구로 바로 보내 층을 건너뛰는 걸 막기 위함.")]
        [SerializeField] private float exitExclusionRadius = 1.5f;
        [Tooltip("지금 있던 자리 근처(이 거리 안)로는 텔레포트되지 않는다. 의미 없는 제자리 이동 방지.")]
        [SerializeField] private float minTeleportDistance = 2f;

        // 후보 칸 목록. 매 발동마다 새로 채우지만 리스트 자체는 재사용해 할당을 줄인다.
        private readonly List<Vector2Int> _candidates = new List<Vector2Int>();

        protected override void OnTriggered(GameObject target, bool isPlayer)
        {
            if (maze == null || maze.OpenGrid == null) return;

            Vector2? destination = PickDestination(target.transform.position);
            if (!destination.HasValue) return;

            target.transform.position = destination.Value;

            if (!isPlayer)
            {
                // 몬스터는 텔레포트 직후 기존 경로/추격 상태가 새 위치와 안 맞으니 리셋해서 배회부터 다시 시작시킨다.
                ITeleportAware teleportAware = target.GetComponent<ITeleportAware>();
                teleportAware?.OnTeleported();
            }
        }

        private Vector2? PickDestination(Vector3 currentWorld)
        {
            bool[,] grid = maze.OpenGrid;
            int w = grid.GetLength(0);
            int h = grid.GetLength(1);

            Vector2 origin = maze.GridOrigin;
            float cellSize = maze.CellSize;
            Vector3 exitWorld = maze.ExitWorldPosition;

            _candidates.Clear();

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    if (!grid[x, y]) continue;

                    Vector2 world = GridPathfinder.CellToWorldCenter(new Vector2Int(x, y), origin, cellSize);

                    if (Vector2.Distance(world, exitWorld) < exitExclusionRadius) continue;
                    if (Vector2.Distance(world, currentWorld) < minTeleportDistance) continue;

                    _candidates.Add(new Vector2Int(x, y));
                }
            }

            if (_candidates.Count == 0) return null; // 후보가 없으면(아주 작은 미로 등) 이동하지 않음

            Vector2Int pick = _candidates[UnityEngine.Random.Range(0, _candidates.Count)];
            return GridPathfinder.CellToWorldCenter(pick, origin, cellSize);
        }

        /// <summary>MazeBuilder가 오브젝트 풀에서 꺼낸 직후 호출해서 자기 자신을 넣어준다.</summary>
        public void InjectMaze(MazeBuilder mazeBuilder) => maze = mazeBuilder;
    }
}