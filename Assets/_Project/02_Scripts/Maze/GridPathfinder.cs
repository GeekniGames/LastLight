using System.Collections.Generic;
using UnityEngine;

namespace LastLight.Maze
{
    /// <summary>
    /// 타일 격자(true=통로) 위에서 BFS로 최단 경로를 찾는다. 미로가 모두 격자 기반이라
    /// A*보다 단순한 BFS로도 충분히 빠르고, 가중치가 없는 격자에서는 BFS가 곧 최단 경로다.
    /// 몬스터 AI가 반복 호출하므로(추격 중 주기적 재탐색), 물리엔진을 쓰지 않고 배열만 다뤄 가볍다.
    /// </summary>
    public static class GridPathfinder
    {
        private static readonly Vector2Int[] Dirs4 =
        {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
        };

        /// <summary>월드 좌표를 타일 격자 좌표로 변환.</summary>
        public static Vector2Int WorldToCell(Vector2 world, Vector2 gridOrigin, float cellSize)
        {
            return new Vector2Int(
                Mathf.FloorToInt((world.x - gridOrigin.x) / cellSize),
                Mathf.FloorToInt((world.y - gridOrigin.y) / cellSize));
        }

        /// <summary>타일 격자 좌표의 중심 월드 좌표.</summary>
        public static Vector2 CellToWorldCenter(Vector2Int cell, Vector2 gridOrigin, float cellSize)
        {
            return gridOrigin + new Vector2((cell.x + 0.5f) * cellSize, (cell.y + 0.5f) * cellSize);
        }

        /// <summary>
        /// start에서 goal까지의 경로를 칸 단위로 찾아 result에 담는다(순서: start 다음 칸 ~ goal).
        /// 못 찾으면 false를 반환하고 result를 비운다. maxSteps는 탐색 범위를 제한해 너무 먼 목표에서
        /// 과도한 탐색을 하지 않게 막는다(성능 안전장치).
        /// </summary>
        public static bool TryFindPath(bool[,] open, Vector2Int start, Vector2Int goal,
                                       List<Vector2Int> result, int maxSteps = 4000)
        {
            result.Clear();

            int w = open.GetLength(0);
            int h = open.GetLength(1);

            if (!InBounds(start, w, h) || !InBounds(goal, w, h)) return false;
            if (!open[start.x, start.y] || !open[goal.x, goal.y]) return false;
            if (start == goal) return true; // 이미 도착

            var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            cameFrom[start] = start;

            int steps = 0;
            bool found = false;

            while (queue.Count > 0 && steps < maxSteps)
            {
                Vector2Int current = queue.Dequeue();
                steps++;

                if (current == goal)
                {
                    found = true;
                    break;
                }

                foreach (Vector2Int d in Dirs4)
                {
                    Vector2Int next = current + d;
                    if (!InBounds(next, w, h) || !open[next.x, next.y]) continue;
                    if (cameFrom.ContainsKey(next)) continue;

                    cameFrom[next] = current;
                    queue.Enqueue(next);
                }
            }

            if (!found) return false;

            // goal에서 start까지 역추적한 뒤 뒤집어서 반환
            Vector2Int node = goal;
            while (node != start)
            {
                result.Add(node);
                node = cameFrom[node];
            }
            result.Reverse();

            return true;
        }

        private static bool InBounds(Vector2Int c, int w, int h)
        {
            return c.x >= 0 && c.y >= 0 && c.x < w && c.y < h;
        }
    }
}