using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastLight.Maze
{
    [Flags]
    public enum WallSide
    {
        None = 0,
        North = 1,
        East = 2,
        South = 4,
        West = 8
    }

    public class MazeCell
    {
        public bool Visited;
        public WallSide Walls = WallSide.North | WallSide.East | WallSide.South | WallSide.West;
    }

    /// <summary>
    /// Recursive Backtracking 미로 생성 알고리즘.
    /// 재귀 대신 스택을 직접 다루는 반복문으로 구현 — 미로가 커져도 콜스택 오버플로우 위험이 없고
    /// 함수 호출 오버헤드도 없음. Unity 오브젝트에 의존하지 않는 순수 C# 로직이라 재사용/테스트가 쉬움.
    /// </summary>
    public class MazeGenerator
    {
        public int Width { get; }
        public int Height { get; }
        public MazeCell[,] Cells { get; }

        private readonly System.Random _rng;

        public MazeGenerator(int width, int height, int seed)
        {
            Width = Mathf.Max(2, width);
            Height = Mathf.Max(2, height);
            _rng = new System.Random(seed);

            Cells = new MazeCell[Width, Height];
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    Cells[x, y] = new MazeCell();
        }

        public void Generate()
        {
            var stack = new Stack<Vector2Int>();
            var start = new Vector2Int(0, 0);

            Cells[start.x, start.y].Visited = true;
            stack.Push(start);

            while (stack.Count > 0)
            {
                Vector2Int current = stack.Peek();
                Vector2Int? next = GetRandomUnvisitedNeighbor(current);

                if (next.HasValue)
                {
                    RemoveWallBetween(current, next.Value);
                    Cells[next.Value.x, next.Value.y].Visited = true;
                    stack.Push(next.Value);
                }
                else
                {
                    stack.Pop();
                }
            }
        }

        /// <summary>BFS로 시작 지점(0,0)에서 가장 멀리 떨어진 셀을 찾는다. 출구 배치에 사용.</summary>
        public Vector2Int FindFarthestCellFromStart()
        {
            var start = new Vector2Int(0, 0);
            var distance = new Dictionary<Vector2Int, int> { [start] = 0 };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);

            Vector2Int farthest = start;

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();

                foreach (Vector2Int neighbor in GetOpenNeighbors(current))
                {
                    if (distance.ContainsKey(neighbor)) continue;

                    distance[neighbor] = distance[current] + 1;
                    queue.Enqueue(neighbor);

                    if (distance[neighbor] > distance[farthest])
                        farthest = neighbor;
                }
            }

            return farthest;
        }

        private IEnumerable<Vector2Int> GetOpenNeighbors(Vector2Int cell)
        {
            WallSide walls = Cells[cell.x, cell.y].Walls;

            if ((walls & WallSide.North) == 0 && cell.y + 1 < Height)
                yield return cell + Vector2Int.up;

            if ((walls & WallSide.South) == 0 && cell.y - 1 >= 0)
                yield return cell + Vector2Int.down;

            if ((walls & WallSide.East) == 0 && cell.x + 1 < Width)
                yield return cell + Vector2Int.right;

            if ((walls & WallSide.West) == 0 && cell.x - 1 >= 0)
                yield return cell + Vector2Int.left;
        }

        private Vector2Int? GetRandomUnvisitedNeighbor(Vector2Int cell)
        {
            var candidates = new List<Vector2Int>(4);

            if (cell.y + 1 < Height && !Cells[cell.x, cell.y + 1].Visited) candidates.Add(cell + Vector2Int.up);
            if (cell.y - 1 >= 0 && !Cells[cell.x, cell.y - 1].Visited) candidates.Add(cell + Vector2Int.down);
            if (cell.x + 1 < Width && !Cells[cell.x + 1, cell.y].Visited) candidates.Add(cell + Vector2Int.right);
            if (cell.x - 1 >= 0 && !Cells[cell.x - 1, cell.y].Visited) candidates.Add(cell + Vector2Int.left);

            if (candidates.Count == 0) return null;

            return candidates[_rng.Next(candidates.Count)];
        }

        private void RemoveWallBetween(Vector2Int a, Vector2Int b)
        {
            Vector2Int delta = b - a;

            if (delta == Vector2Int.up)
            {
                Cells[a.x, a.y].Walls &= ~WallSide.North;
                Cells[b.x, b.y].Walls &= ~WallSide.South;
            }
            else if (delta == Vector2Int.down)
            {
                Cells[a.x, a.y].Walls &= ~WallSide.South;
                Cells[b.x, b.y].Walls &= ~WallSide.North;
            }
            else if (delta == Vector2Int.right)
            {
                Cells[a.x, a.y].Walls &= ~WallSide.East;
                Cells[b.x, b.y].Walls &= ~WallSide.West;
            }
            else if (delta == Vector2Int.left)
            {
                Cells[a.x, a.y].Walls &= ~WallSide.West;
                Cells[b.x, b.y].Walls &= ~WallSide.East;
            }
        }
    }
}