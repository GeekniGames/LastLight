using System.Collections.Generic;
using LastLight.Core;
using UnityEngine;
using UnityEngine.Tilemaps;
using LastLight.Vision;

namespace LastLight.Maze
{
    /// <summary>
    /// MazeGenerator가 만든 셀/벽 데이터를 타일 격자로 변환해 Tilemap에 그린다.
    ///
    /// 타일 격자 규칙: 미로의 열마다 폭, 행마다 높이를 확률대로 무작위(1~N칸)로 뽑고,
    /// 셀 (cx, cy)는 (열 폭 x 행 높이) 크기의 바닥 블록이 된다. 셀 사이 벽은 wallThickness 두께이며,
    /// 뚫린 벽은 그 열 폭(또는 행 높이)만큼 바닥으로 이어진다. 나머지 타일은 벽이다.
    /// 같은 열/행을 따라 뻗은 통로는 굵기가 같고, 열과 행이 만나는 곳은 자연스럽게 넓은 방이 된다.
    /// 통로(바닥)에 닿아 있는 벽만 그려서, 벽 뒤 공간은 비어 있는(검은) 허공이 된다.
    /// </summary>
    public class MazeBuilder : MonoBehaviour
    {
        [Header("Tilemaps")]
        [SerializeField] private Tilemap floorTilemap;
        [SerializeField] private Tilemap wallTilemap; // TilemapCollider2D + CompositeCollider2D 권장

        [Header("Tiles")]
        [SerializeField] private TileBase floorTile;
        [Tooltip("벽 정면 타일(바로 아래 칸이 바닥인 벽). 여러 개 넣으면 무작위로 섞어서 깐다.")]
        [SerializeField] private TileBase[] wallFaceTiles;
        [Tooltip("벽 윗면 타일(정면이 아닌 나머지 벽). 여러 개 넣으면 무작위로 섞어서 깐다.")]
        [SerializeField] private TileBase[] wallTopTiles;

        [Header("Objects")]
        [SerializeField] private Transform exitMarkerPrefab;
        [SerializeField] private Transform player;

        [Header("Maze Layout (타일 단위)")]
        [Tooltip("통로 굵기 확률(상대값). 0번 = 1칸, 1번 = 2칸, 2번 = 3칸 ... 배열 길이가 최대 굵기다. 합이 100일 필요는 없다. 0이면 그 굵기는 안 나온다.")]
        [SerializeField] private float[] passageWidthWeights = { 35f, 27f, 18f, 12f, 5f, 3f };
        [Tooltip("벽 두께(타일 수). 3 이상이면 통로에 닿은 가장자리만 그려지고 안쪽은 검은 허공이 된다.")]
        [SerializeField, Min(1)] private int wallThickness = 3;

        [Header("Maze Size (셀 개수. 셀 하나의 크기는 그 열 폭/행 높이에 따라 달라진다)")]
        [SerializeField] private int minCells = 5;
        [SerializeField] private int maxCells = 15;

        [Header("Battery Packs")]
        [SerializeField] private BatteryPickup batteryPickupPrefab;
        [Tooltip("한 층에 놓일 배터리팩 개수. 지금은 고정값이고, 나중에 층 구간별 난이도 표로 확장할 예정(GetBatteryPackCountForFloor 참고).")]
        [SerializeField, Min(0)] private int batteryPackCount = 2;
        [Tooltip("배터리팩 하나를 먹었을 때 채워지는 양(최대 배터리 대비 비율).")]
        [Range(0f, 1f)]
        [SerializeField] private float batteryRechargePercent = 0.28f;
        [SerializeField] private int batteryPrewarmCount = 10;

        [Header("Torches (길목 조명)")]
        [SerializeField] private TorchLight torchPrefab;
        [Tooltip("1층에서 놓이는 횃불 개수. 층이 깊어질수록 이 값에서 0까지 선형으로 줄어든다.")]
        [SerializeField] private int torchCountAtFloor1 = 4;
        [Tooltip("이 층수에 도달하면 횃불이 아예 안 나온다.")]
        [SerializeField] private int torchDisappearFloor = 15;
        [SerializeField] private int torchPrewarmCount = 10;
        [Tooltip("왼쪽/오른쪽 벽에 붙을 때 치우치는 양(셀 크기 대비 비율, 0~0.5).")]
        [Range(0f, 0.5f)]
        [SerializeField] private float torchSideWallOffset = 0.42f;
        [Tooltip("뒤쪽(정면) 벽에 붙을 때 치우치는 양(셀 크기 대비 비율, 0~0.5).")]
        [Range(0f, 0.5f)]
        [SerializeField] private float torchBackWallOffset = 0.3f;
        [Tooltip("횃불끼리 이 거리(월드 유닛) 안으로는 같이 놓이지 않는다. 너무 다닥다닥 붙는 걸 방지.")]
        [SerializeField] private float torchMinSpacing = 3f;
        [Tooltip("정면(뒤쪽) 벽에 걸리는 횃불을 추가로 더 위로 올리는 양(월드 유닛). 벽에 걸린 느낌을 살리기 위함.")]
        [SerializeField] private float torchBackWallLift = 0.35f;

        [Header("Traps")]
        [Tooltip("배치할 함정 종류들. 여러 개 넣으면 한 층에 섞여서 무작위로 나온다.")]
        [SerializeField] private TrapBase[] trapPrefabs;
        [Tooltip("한 층에 놓일 함정 개수의 범위(이 사이에서 매 층 랜덤). 나중에 층 구간별 난이도 표로 확장할 예정(GetTrapCountForFloor 참고).")]
        [SerializeField, Min(0)] private int trapCountMin = 1;
        [SerializeField, Min(0)] private int trapCountMax = 2;
        [SerializeField] private int trapPrewarmCount = 6;

        private readonly List<Vector3Int> _floorPositions = new List<Vector3Int>();
        private readonly List<Vector3Int> _wallFacePositions = new List<Vector3Int>();
        private readonly List<Vector3Int> _wallTopPositions = new List<Vector3Int>();

        private Transform _exitInstance;
        private Vector2Int _exitCell;
        private MazeGenerator _current;
        private ObjectPool<BatteryPickup> _batteryPool;
        private ObjectPool<TorchLight> _torchPool;
        private ObjectPool<TrapBase>[] _trapPools;

        // 이번 층에 이미 뭔가(배터리팩, 함정 등) 놓인 칸. 서로 겹치지 않게 하는 데 쓴다.
        private readonly HashSet<Vector2Int> _occupiedCells = new HashSet<Vector2Int>();

        // 이번 층의 열/행 굵기와 시작 타일 위치(출구·시작 위치 계산에 사용)
        private int[] _colWidths;
        private int[] _rowHeights;
        private int[] _colOrigins;
        private int[] _rowOrigins;

        private void Awake()
        {
            if (batteryPickupPrefab != null)
                _batteryPool = new ObjectPool<BatteryPickup>(batteryPickupPrefab, transform, batteryPrewarmCount);

            if (torchPrefab != null)
                _torchPool = new ObjectPool<TorchLight>(torchPrefab, transform, torchPrewarmCount);

            if (trapPrefabs != null && trapPrefabs.Length > 0)
            {
                _trapPools = new ObjectPool<TrapBase>[trapPrefabs.Length];
                for (int i = 0; i < trapPrefabs.Length; i++)
                {
                    if (trapPrefabs[i] != null)
                        _trapPools[i] = new ObjectPool<TrapBase>(trapPrefabs[i], transform, trapPrewarmCount);
                }
            }
        }

        // --- 시야(빛 막힘) 계산용 공개 정보 ---
        /// <summary>현재 층의 타일 격자. true = 통로(빛 통과), false = 벽/공허(빛 차단). 층이 바뀌면 새 배열로 교체됨.</summary>
        public bool[,] OpenGrid { get; private set; }

        /// <summary>실제로 그려지는 벽(통로에 닿은 가장자리 벽 칸)이면 true. 벽 안쪽의 검은 칸은 false.</summary>
        public bool[,] VisibleWallGrid { get; private set; }

        /// <summary>층을 새로 그릴 때마다 1씩 증가. 시야 계산이 캐시를 무효화하는 기준으로 쓴다.</summary>
        public int GridVersion { get; private set; }

        /// <summary>격자 (0,0) 타일의 왼쪽 아래 모서리 월드 좌표.</summary>
        public Vector2 GridOrigin { get; private set; }

        /// <summary>타일 1칸의 월드 크기.</summary>
        public float CellSize { get; private set; } = 1f;

        /// <summary>현재 출구 마커의 월드 좌표. 텔레포트 함정 등이 출구 근처를 피하는 데 사용.</summary>
        public Vector3 ExitWorldPosition => _exitInstance != null ? _exitInstance.position : Vector3.zero;

        /// <summary>새 층으로 진입할 때 호출. 이전 미로를 지우고 층수에 맞는 크기로 새 미로를 그린다.</summary>
        public void BuildFloor(int floor)
        {
            if (floorTilemap == null || wallTilemap == null || floorTile == null
                || wallFaceTiles == null || wallFaceTiles.Length == 0
                || wallTopTiles == null || wallTopTiles.Length == 0)
            {
                Debug.LogError("[MazeBuilder] Tilemap 또는 Tile 참조가 비어 있습니다. (Wall Face Tiles / Wall Top Tiles에 타일을 1개 이상 넣어야 합니다)");
                return;
            }

            int wall = Mathf.Max(1, wallThickness);

            int cells = Mathf.Clamp(minCells + (floor - 1), minCells, maxCells);
            int seed = System.Guid.NewGuid().GetHashCode();
            _current = new MazeGenerator(cells, cells, seed: seed);
            _current.Generate();

            // 열마다 폭, 행마다 높이를 확률대로 뽑는다
            var layoutRng = new System.Random(seed ^ 0x5F3759DF);
            _colWidths = RollSizes(cells, layoutRng);
            _rowHeights = RollSizes(cells, layoutRng);

            bool[,] open = BuildTileGrid(_current, _colWidths, _rowHeights, wall,
                                         out _colOrigins, out _rowOrigins);

            OpenGrid = open;
            CellSize = floorTilemap.layoutGrid.cellSize.x;
            GridOrigin = floorTilemap.CellToWorld(Vector3Int.zero);
            GridVersion++;

            PaintTiles(open);
            PlaceExit();
            PlacePlayerAtStart();

            _occupiedCells.Clear();
            PlaceBatteryPacks(floor);
            PlaceTorches(floor);
            PlaceTraps(floor);
        }

        /// <summary>굵기 확률(passageWidthWeights)대로 count개의 굵기(1~N칸)를 뽑는다.</summary>
        private int[] RollSizes(int count, System.Random rng)
        {
            var sizes = new int[count];

            int max = passageWidthWeights != null ? passageWidthWeights.Length : 0;
            float total = 0f;
            for (int i = 0; i < max; i++)
                total += Mathf.Max(0f, passageWidthWeights[i]);

            for (int n = 0; n < count; n++)
            {
                if (total <= 0f)
                {
                    sizes[n] = 1; // 확률이 비어 있으면 1칸으로
                    continue;
                }

                float r = (float)rng.NextDouble() * total;
                int size = max;
                for (int i = 0; i < max; i++)
                {
                    r -= Mathf.Max(0f, passageWidthWeights[i]);
                    if (r < 0f)
                    {
                        size = i + 1;
                        break;
                    }
                }
                sizes[n] = size;
            }

            return sizes;
        }

        /// <summary>
        /// 셀/벽 데이터를 타일 격자로 변환. true = 통로(바닥).
        /// 셀 (cx, cy)는 (열 폭 x 행 높이) 바닥 블록이고, 셀 사이의 벽은 wall 타일 두께다.
        /// 뚫린 벽은 그 열 폭(북쪽) 또는 행 높이(동쪽)만큼 바닥으로 채워 통로가 이어지게 한다.
        /// </summary>
        private static bool[,] BuildTileGrid(MazeGenerator maze, int[] colW, int[] rowH, int wall,
                                             out int[] colOrigin, out int[] rowOrigin)
        {
            // 각 열/행이 시작하는 타일 좌표(앞쪽 굵기들의 누적합)
            colOrigin = new int[maze.Width];
            int w = wall;
            for (int c = 0; c < maze.Width; c++)
            {
                colOrigin[c] = w;
                w += colW[c] + wall;
            }

            rowOrigin = new int[maze.Height];
            int h = wall;
            for (int r = 0; r < maze.Height; r++)
            {
                rowOrigin[r] = h;
                h += rowH[r] + wall;
            }

            var open = new bool[w, h];

            for (int cx = 0; cx < maze.Width; cx++)
            {
                for (int cy = 0; cy < maze.Height; cy++)
                {
                    int ox = colOrigin[cx];
                    int oy = rowOrigin[cy];
                    int cw = colW[cx];
                    int rh = rowH[cy];

                    FillRect(open, ox, oy, cw, rh);

                    // 남/서쪽은 이웃 셀의 북/동쪽 벽과 같은 벽이라 북/동만 검사하면 충분
                    // (북쪽 이웃은 같은 열이라 폭이 같고, 동쪽 이웃은 같은 행이라 높이가 같다)
                    WallSide walls = maze.Cells[cx, cy].Walls;
                    if ((walls & WallSide.North) == 0) FillRect(open, ox, oy + rh, cw, wall);
                    if ((walls & WallSide.East) == 0) FillRect(open, ox + cw, oy, wall, rh);
                }
            }

            return open;
        }

        private static void FillRect(bool[,] grid, int x, int y, int width, int height)
        {
            for (int ix = x; ix < x + width; ix++)
                for (int iy = y; iy < y + height; iy++)
                    grid[ix, iy] = true;
        }

        private void PaintTiles(bool[,] open)
        {
            floorTilemap.ClearAllTiles();
            wallTilemap.ClearAllTiles();
            _floorPositions.Clear();
            _wallFacePositions.Clear();
            _wallTopPositions.Clear();

            int w = open.GetLength(0);
            int h = open.GetLength(1);
            var visibleWalls = new bool[w, h];

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    if (open[x, y])
                    {
                        _floorPositions.Add(new Vector3Int(x, y, 0));
                    }
                    else if (HasOpenNeighbor(open, x, y, w, h))
                    {
                        visibleWalls[x, y] = true;
                        // 바로 아래(남쪽) 칸이 바닥이면 화면에서 벽의 앞면이 보이므로 정면 타일,
                        // 그 외에는 위에서 본 윗면 타일을 쓴다.
                        bool floorBelow = y > 0 && open[x, y - 1];
                        (floorBelow ? _wallFacePositions : _wallTopPositions).Add(new Vector3Int(x, y, 0));
                    }
                }
            }

            // 타일마다 SetTile을 부르지 않고 한 번에 배치(Tilemap 갱신 횟수를 최소화)
            Apply(floorTilemap, _floorPositions, floorTile);
            Apply(wallTilemap, _wallFacePositions, wallFaceTiles);
            Apply(wallTilemap, _wallTopPositions, wallTopTiles);

            VisibleWallGrid = visibleWalls;
        }

        private static bool HasOpenNeighbor(bool[,] open, int x, int y, int w, int h)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    int nx = x + dx;
                    int ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    if (open[nx, ny]) return true;
                }
            }
            return false;
        }

        private static void Apply(Tilemap map, List<Vector3Int> positions, TileBase tile)
        {
            Vector3Int[] posArray = positions.ToArray();
            var tiles = new TileBase[posArray.Length];
            System.Array.Fill(tiles, tile);
            map.SetTiles(posArray, tiles);
        }

        /// <summary>변형 타일이 여러 개면 칸마다 무작위로 골라 배치한다(층을 새로 그릴 때 한 번만 실행).</summary>
        private static void Apply(Tilemap map, List<Vector3Int> positions, TileBase[] variants)
        {
            int count = positions.Count;
            if (count == 0) return;

            Vector3Int[] posArray = positions.ToArray();
            var tiles = new TileBase[count];

            if (variants.Length == 1)
            {
                System.Array.Fill(tiles, variants[0]);
            }
            else
            {
                for (int i = 0; i < count; i++)
                    tiles[i] = variants[Random.Range(0, variants.Length)];
            }

            map.SetTiles(posArray, tiles);
        }

        /// <summary>셀 (cx, cy)의 바닥 블록 중앙의 월드 좌표.</summary>
        private Vector3 CellCenterWorld(int cx, int cy)
        {
            var origin = new Vector3Int(_colOrigins[cx], _rowOrigins[cy], 0);

            // 블록의 왼쪽 아래 타일 중심에서 (굵기 - 1)/2 타일만큼 옮기면 블록의 정중앙
            float offsetX = (_colWidths[cx] - 1) * 0.5f * CellSize;
            float offsetY = (_rowHeights[cy] - 1) * 0.5f * CellSize;
            return floorTilemap.GetCellCenterWorld(origin) + new Vector3(offsetX, offsetY, 0f);
        }

        private void PlaceExit()
        {
            _exitCell = _current.FindFarthestCellFromStart();

            if (_exitInstance == null && exitMarkerPrefab != null)
                _exitInstance = Instantiate(exitMarkerPrefab, transform);

            if (_exitInstance != null)
                _exitInstance.position = CellCenterWorld(_exitCell.x, _exitCell.y);
        }

        private void PlacePlayerAtStart()
        {
            if (player == null) return;

            player.position = CellCenterWorld(0, 0);
        }

        /// <summary>
        /// 시작 칸(0,0)과 출구 칸을 제외한 무작위 셀에 배터리팩을 놓는다.
        /// 이전 층의 배터리팩은 풀로 되돌리고(재사용), 이번 층 몫만 다시 꺼내 배치한다.
        /// </summary>
        private void PlaceBatteryPacks(int floor)
        {
            if (_batteryPool == null) return;

            _batteryPool.ReleaseAll();

            int count = GetBatteryPackCountForFloor(floor);
            if (count <= 0) return;

            var candidates = new List<Vector2Int>();
            for (int cx = 0; cx < _current.Width; cx++)
            {
                for (int cy = 0; cy < _current.Height; cy++)
                {
                    if (cx == 0 && cy == 0) continue; // 시작 칸
                    if (cx == _exitCell.x && cy == _exitCell.y) continue; // 출구 칸
                    if (_occupiedCells.Contains(new Vector2Int(cx, cy))) continue; // 이미 다른 게 놓인 칸

                    candidates.Add(new Vector2Int(cx, cy));
                }
            }

            var rng = new System.Random();
            int placed = Mathf.Min(count, candidates.Count);

            for (int i = 0; i < placed; i++)
            {
                int pick = rng.Next(candidates.Count);
                Vector2Int cell = candidates[pick];
                candidates.RemoveAt(pick); // 같은 칸에 두 개가 겹치지 않게
                _occupiedCells.Add(cell);

                BatteryPickup pack = _batteryPool.Get();
                pack.transform.position = CellCenterWorld(cell.x, cell.y);
                pack.SetRechargePercent(batteryRechargePercent);
            }
        }

        /// <summary>
        /// 층수에 따른 배터리팩 개수. 지금은 고정값을 돌려주지만, 이후 기획서 난이도 구간(1-5 / 6-15 / 16-30 / 31+)에
        /// 맞춰 이 함수 안에서만 값을 바꾸면 되도록 자리를 잡아뒀다.
        /// </summary>
        private int GetBatteryPackCountForFloor(int floor)
        {
            return batteryPackCount;
        }

        /// <summary>
        /// 시작/출구 칸, 이미 다른 게 놓인 칸을 제외한 무작위 칸에 횃불을 놓는다.
        /// 함정과 달리 통로 폭 제한은 없다(이동을 막지 않는 순수 장식+조명이라 좁은 외길에도 자연스럽다).
        /// </summary>
        /// <summary>
        /// 벽에 맞닿은 바닥 타일만 후보로 삼아, 그 벽 쪽으로 살짝 치우친 위치에 횃불을 놓는다.
        /// 배터리팩/함정이 쓰는 '거친 칸' 단위가 아니라 실제 타일 격자(OpenGrid/VisibleWallGrid) 기준으로 계산한다.
        /// </summary>
        /// <summary>
        /// 정면(뒤쪽)과 좌우 벽에 맞닿은 바닥 타일만 후보로 삼아 횃불을 건다. 아래쪽(남쪽) 벽은
        /// 윗면(평평한 지붕) 타일이라 제외 — 지금 아트로는 걸어둔 모양이 안 나와서다.
        /// 정면 벽은 '벽에 걸린' 느낌이 나도록 좌우보다 더 위로 올려서 배치한다.
        /// </summary>
        private void PlaceTorches(int floor)
        {
            if (_torchPool == null || OpenGrid == null || VisibleWallGrid == null) return;

            _torchPool.ReleaseAll();

            int count = GetTorchCountForFloor(floor);
            if (count <= 0) return;

            bool[,] open = OpenGrid;
            bool[,] wall = VisibleWallGrid;
            int w = open.GetLength(0);
            int h = open.GetLength(1);

            var candidates = new List<Vector3>();

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    if (!open[x, y]) continue;

                    Vector2Int? wallDir = null;
                    float wallOffsetAmount = 0f;
                    float extraLift = 0f;

                    if (y + 1 < h && wall[x, y + 1])
                    {
                        wallDir = Vector2Int.up; // 정면(뒤쪽) 벽
                        wallOffsetAmount = torchBackWallOffset;
                        extraLift = torchBackWallLift;
                    }
                    else if (x + 1 < w && wall[x + 1, y])
                    {
                        wallDir = Vector2Int.right;
                        wallOffsetAmount = torchSideWallOffset;
                    }
                    else if (x - 1 >= 0 && wall[x - 1, y])
                    {
                        wallDir = Vector2Int.left;
                        wallOffsetAmount = torchSideWallOffset;
                    }
                    // 아래쪽(남쪽) 벽은 의도적으로 후보에 안 넣음

                    if (!wallDir.HasValue) continue;

                    Vector2 tileCenter = GridPathfinder.CellToWorldCenter(new Vector2Int(x, y), GridOrigin, CellSize);
                    Vector2 offset = (Vector2)wallDir.Value * (CellSize * wallOffsetAmount) + Vector2.up * extraLift;
                    candidates.Add(tileCenter + offset);
                }
            }

            var rng = new System.Random();

            for (int i = 0; i < count && candidates.Count > 0; i++)
            {
                int pick = rng.Next(candidates.Count);
                Vector3 pos = candidates[pick];

                TorchLight torch = _torchPool.Get();
                torch.transform.position = pos;

                candidates.RemoveAll(c => Vector3.Distance(c, pos) < torchMinSpacing);
            }
        }

        /// <summary>1층엔 torchCountAtFloor1개, torchDisappearFloor층부터는 0개가 되도록 선형으로 줄인다.</summary>
        private int GetTorchCountForFloor(int floor)
        {
            if (floor >= torchDisappearFloor) return 0;

            float t = Mathf.Clamp01((floor - 1) / (float)Mathf.Max(1, torchDisappearFloor - 1));
            return Mathf.RoundToInt(Mathf.Lerp(torchCountAtFloor1, 0, t));
        }

        /// <summary>
        /// 시작/출구 칸, 이미 다른 게 놓인 칸, 그리고 '피할 길이 없는 외길(열 폭도 1, 행 높이도 1)'을 제외한
        /// 무작위 칸에 함정을 섞어서 놓는다. 함정 종류는 trapPrefabs 중에서 매번 무작위로 고른다.
        /// </summary>
        private void PlaceTraps(int floor)
        {
            if (_trapPools == null || _trapPools.Length == 0) return;

            foreach (var pool in _trapPools)
                pool?.ReleaseAll();

            int count = GetTrapCountForFloor(floor);
            if (count <= 0) return;

            var candidates = new List<Vector2Int>();
            for (int cx = 0; cx < _current.Width; cx++)
            {
                for (int cy = 0; cy < _current.Height; cy++)
                {
                    if (cx == 0 && cy == 0) continue; // 시작 칸
                    if (cx == _exitCell.x && cy == _exitCell.y) continue; // 출구 칸
                    if (_occupiedCells.Contains(new Vector2Int(cx, cy))) continue; // 이미 다른 게 놓인 칸
                    if (_colWidths[cx] < 2 || _rowHeights[cy] < 2) continue; // 가로·세로 둘 다 2 이상이어야 실제로 피할 공간이 생김

                    candidates.Add(new Vector2Int(cx, cy));
                }
            }

            var rng = new System.Random();
            int placed = Mathf.Min(count, candidates.Count);

            for (int i = 0; i < placed; i++)
            {
                int cellPick = rng.Next(candidates.Count);
                Vector2Int cell = candidates[cellPick];
                candidates.RemoveAt(cellPick);
                _occupiedCells.Add(cell);

                int poolPick = rng.Next(_trapPools.Length);
                ObjectPool<TrapBase> pool = _trapPools[poolPick];
                if (pool == null) continue;

                TrapBase trap = pool.Get();
                trap.transform.position = CellCenterWorld(cell.x, cell.y);

                if (trap is IRequiresMaze requiresMaze)
                    requiresMaze.InjectMaze(this);
            }
        }

        /// <summary>
        /// 층수에 따른 함정 개수. 지금은 Min~Max 범위에서 매 층 무작위로 뽑지만,
        /// 이후 난이도 구간표에 맞춰 이 함수 안에서 층수별로 범위 자체를 바꿀 수 있게 자리를 잡아뒀다.
        /// </summary>
        private int GetTrapCountForFloor(int floor)
        {
            return Random.Range(trapCountMin, trapCountMax + 1);
        }
    }
}