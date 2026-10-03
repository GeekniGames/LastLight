using UnityEngine;

namespace LastLight.Maze
{
    /// <summary>
    /// 타일 격자 위의 광선 계산(DDA). Physics2D를 쓰지 않고 격자 칸만 따라가므로 매우 가볍고,
    /// 할당(GC)도 없다. 손전등이 벽에 막히는 지점을 구하는 데 쓴다.
    /// </summary>
    public static class GridRaycast
    {
        /// <summary>
        /// start에서 dir 방향으로 광선을 쏴서, 빛이 닿는 거리를 돌려준다.
        ///
        /// 광선이 벽 칸에 처음 들어오면 그때부터는 '벽 안쪽'으로 취급한다.
        /// - 들어온 방향의 축으로 한 칸 더 넘어가면(= 벽 두께 1칸을 통과) 거기서 끝
        /// - 다른 축으로 넘어가서 통로(빈 칸)가 나오면(= 벽이 끝남) 거기서 끝
        /// - 다른 축으로 넘어갔는데 또 '그려지는 벽'이면, 벽면을 따라 계속 밝힌다
        /// - 다른 축으로 넘어갔는데 그려지지 않는 벽 안쪽 칸이면 거기서 끝 (벽 안쪽으로는 빛이 못 들어감)
        /// 이렇게 해야 광선이 벽면을 스치듯 지나갈 때 벽이 칸마다 끊겨 보이지 않으면서도,
        /// 벽 안쪽을 통과해 반대편 벽까지 밝히는 일이 없다.
        /// maxDist 안에 벽이 없으면 maxDist를 반환한다.
        /// </summary>
        /// <param name="open">true = 통과 가능, false(또는 격자 밖) = 막힘</param>
        /// <param name="visibleWall">true = 실제로 그려지는 벽(통로에 닿은 가장자리 벽 칸)</param>
        /// <param name="dirWorld">반드시 정규화된 방향</param>
        public static float Cast(bool[,] open, bool[,] visibleWall, Vector2 gridOrigin, float cellSize,
                                 Vector2 startWorld, Vector2 dirWorld, float maxDist)
        {
            int w = open.GetLength(0);
            int h = open.GetLength(1);

            float inv = 1f / cellSize;
            float px = (startWorld.x - gridOrigin.x) * inv;
            float py = (startWorld.y - gridOrigin.y) * inv;
            float dx = dirWorld.x;
            float dy = dirWorld.y;
            float maxT = maxDist * inv;

            int x = Mathf.FloorToInt(px);
            int y = Mathf.FloorToInt(py);

            int stepX = dx > 0f ? 1 : -1;
            int stepY = dy > 0f ? 1 : -1;

            float absDx = Mathf.Abs(dx);
            float absDy = Mathf.Abs(dy);

            float tDeltaX = absDx > 1e-6f ? 1f / absDx : float.PositiveInfinity;
            float tDeltaY = absDy > 1e-6f ? 1f / absDy : float.PositiveInfinity;

            float tMaxX = absDx > 1e-6f ? (dx > 0f ? (x + 1 - px) : (px - x)) * tDeltaX : float.PositiveInfinity;
            float tMaxY = absDy > 1e-6f ? (dy > 0f ? (y + 1 - py) : (py - y)) * tDeltaY : float.PositiveInfinity;

            bool inWall = false;
            bool enteredViaX = false;

            while (true)
            {
                float t;
                bool stepIsX;

                if (tMaxX < tMaxY)
                {
                    t = tMaxX;
                    x += stepX;
                    tMaxX += tDeltaX;
                    stepIsX = true;
                }
                else
                {
                    t = tMaxY;
                    y += stepY;
                    tMaxY += tDeltaY;
                    stepIsX = false;
                }

                if (t >= maxT)
                    return maxDist;

                bool inside = x >= 0 && y >= 0 && x < w && y < h;
                bool blocked = !inside || !open[x, y];

                if (!inWall)
                {
                    if (blocked)
                    {
                        inWall = true;
                        enteredViaX = stepIsX;
                    }
                    continue;
                }

                // 벽 안쪽: 벽 두께를 다 통과했거나, 벽이 끝나 통로로 나왔으면 종료
                if (stepIsX == enteredViaX || !blocked)
                    return t * cellSize;

                // 그려지는 벽이 아닌 안쪽 칸으로 들어가면 종료
                if (!inside || !visibleWall[x, y])
                    return t * cellSize;
            }
        }
    }
}