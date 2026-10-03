using System;
using System.Collections.Generic;
using DG.Tweening;
using LastLight.Core;
using LastLight.Maze;
using LastLight.Vision;
using LastLight.Player;
using UnityEngine;

namespace LastLight.Monsters
{
    /// <summary>
    /// D1 방랑자: 기본형 몬스터. 평소엔 통로를 따라 느리게 배회하다가,
    /// 손전등(또는 플레이어 주변 빛)에 노출되면 플레이어를 향해 추격한다.
    /// 이동은 물리 힘이 아니라 격자 경로(GridPathfinder)를 따라가는 방식이라 벽에 안 끼고,
    /// 플레이어의 이동 방식(Rigidbody2D + linearVelocity)과 동일한 축을 쓴다.
    ///
    /// 손전등 처치: 빛을 계속 맞으면 경고 → 경직 → 소멸 순으로 진행된다. 경고 중엔 계속 움직이지만
    /// 경직에 들어가면 완전히 멈춘다. 어느 단계든 빛이 끊기면 진행도가 즉시 0으로 풀린다(다시 움직임).
    /// 처치를 시도하는 동안(빛을 맞는 동안)에는 플레이어 배터리가 평소보다 더 빠르게 소모된다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class WandererMonster : MonoBehaviour, IMonsterMarker, ITeleportAware
    {
        private enum State { Wander, Chase }
        private enum KillPhase { None, Warning, Stagger }

        [Header("References")]
        [SerializeField] private MazeBuilder maze;
        [SerializeField] private Transform player;
        [SerializeField] private Animator animator;
        [Tooltip("색 틴트를 적용할 렌더러. 비워두면 색 연출은 생략된다.")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [Tooltip("경직 중 떨림 연출과 소멸 축소 애니메이션에 쓸 트랜스폼. 보통 스프라이트 자식 오브젝트. 비워두면 본체를 그대로 쓴다.")]
        [SerializeField] private Transform spriteVisual;

        [Header("Speed")]
        [SerializeField] private float wanderSpeed = 1f;
        [SerializeField] private float chaseSpeed = 2.2f;
        [Tooltip("걷기 애니메이션 재생 속도 배율(배회/추격 각각). 그림은 하나만 있어도 속도감이 달라 보인다.")]
        [SerializeField] private float wanderAnimSpeed = 0.7f;
        [SerializeField] private float chaseAnimSpeed = 1.4f;

        [Header("Wander")]
        [Tooltip("배회 중 다음 목표 칸까지의 최대 거리(타일 칸 수).")]
        [SerializeField] private int wanderRangeTiles = 6;
        [SerializeField] private Vector2 wanderPauseRange = new Vector2(0.5f, 2f);

        [Header("Chase")]
        [Tooltip("빛을 처음 감지한 뒤 실제로 뛰기 시작하기까지 제자리에서 버티는 시간(초). 그동안 플레이어 쪽을 바라만 본다.")]
        [SerializeField] private float noticeDuration = 1f;
        [Tooltip("몇 초 간격으로 플레이어까지의 경로를 다시 계산할지.")]
        [SerializeField] private float repathInterval = 0.4f;
        [Tooltip("빛에서 벗어난 뒤 이 시간(초) 동안 못 찾으면 배회로 돌아간다.")]
        [SerializeField] private float loseSightGrace = 2f;
        [Tooltip("목표 칸에 이 거리(월드 유닛) 안으로 들어오면 다음 칸으로 넘어간다.")]
        [SerializeField] private float waypointReachDist = 0.15f;

        [Header("Light Kill (손전등 처치)")]
        [Tooltip("빛을 맞기 시작해서 경직에 들어가기까지 걸리는 시간(초).")]
        [SerializeField] private float warningDuration = 1.5f;
        [Tooltip("경직 상태가 유지되는 시간(초). 이 시간이 끝나면 소멸한다.")]
        [SerializeField] private float staggerDuration = 1.2f;
        [Tooltip("경고 중 틴트 색.")]
        [SerializeField] private Color warningColor = new Color(1f, 0.85f, 0.3f, 1f);
        [Tooltip("경직 중 틴트 색.")]
        [SerializeField] private Color staggerColor = new Color(1f, 0.35f, 0.3f, 1f);
        [Tooltip("경직 중 떨림 폭(월드 유닛).")]
        [SerializeField] private float staggerJitterStrength = 0.04f;
        [Tooltip("소멸(축소+페이드) 연출 시간(초).")]
        [SerializeField] private float dissolveDuration = 0.35f;
        [Tooltip("빛을 맞는 동안 배터리가 평소 대비 몇 배 속도로 소모되는지. 2 = 총 소모량이 평소의 2배.")]
        [SerializeField] private float extraDrainMultiplier = 2f;
        [Tooltip("빛이 끊긴 뒤 경고/경직 진행도가 실제로 풀리기까지의 유예 시간(초). 너무 즉각적으로 풀리지 않게 한다.")]
        [SerializeField] private float exposureReleaseDelay = 1f;

        [Header("Catch Player (추격 중 플레이어를 잡는 조건)")]
        [Tooltip("이 거리(월드 유닛) 안으로 붙으면 포획이 시작된다.")]
        [SerializeField] private float catchDistance = 0.4f;
        [Tooltip("포획이 시작된 뒤 게임오버가 확정되기까지의 연출 시간(초). 이 동안 플레이어 입력은 잠긴다.")]
        [SerializeField] private float catchGraceDuration = 0.7f;

        [Header("Warning Icon (경고 단계 동안 머리 위에 뜨는 느낌표)")]
        [Tooltip("느낌표 등 경고 아이콘 자식 오브젝트. 비워두면 아이콘 연출 없이 색 틴트만 적용된다.")]
        [SerializeField] private Transform warningIcon;
        [SerializeField] private float warningIconPopDuration = 0.2f;
        [SerializeField] private float warningIconHideDuration = 0.12f;

        /// <summary>이 몬스터가 손전등에 처치됐을 때 발생(Analytics 등에서 구독 가능).</summary>
        public event Action OnKilled;

        private Rigidbody2D _rb;
        private State _state = State.Wander;

        private readonly List<Vector2Int> _path = new List<Vector2Int>();
        private int _pathIndex;
        private float _repathTimer;
        private float _lostSightTimer;
        private float _wanderPauseTimer;

        private Vector2 _moveInput;
        private Vector2? _facingOverrideDir; // 알아채는 동안 움직이지 않아도 플레이어 쪽을 바라보게 하는 값
        private bool _isNoticing;
        private float _noticeTimer;
        private static readonly int MoveXHash = Animator.StringToHash("MoveX");
        private static readonly int MoveYHash = Animator.StringToHash("MoveY");
        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");

        // 손전등 처치 진행 상태
        private KillPhase _killPhase = KillPhase.None;
        private float _exposureTimer;
        private float _unlitTimer;
        private bool _isDead;
        private bool _isCatchingPlayer;
        private float _catchTimer;
        private Vector3 _spriteBaseLocalPos;
        private Vector3 _warningIconScale = Vector3.one;
        private Tween _iconTween;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            if (spriteVisual != null)
                _spriteBaseLocalPos = spriteVisual.localPosition;

            if (warningIcon != null)
            {
                _warningIconScale = warningIcon.localScale;
                warningIcon.localScale = Vector3.zero; // 평소엔 숨김
            }
        }

        // 나중에 몬스터를 오브젝트 풀로 재사용하게 되면(배터리팩처럼), 다시 켜질 때 이전 층에서의
        // 처치 진행도/색/상태가 남아있지 않도록 초기화한다. 지금 당장은 재사용을 안 해도 안전하다.
        private void OnEnable()
        {
            _isDead = false;
            _killPhase = KillPhase.None;
            _exposureTimer = 0f;
            _unlitTimer = 0f;
            _state = State.Wander;
            _moveInput = Vector2.zero;
            _isNoticing = false;
            _noticeTimer = 0f;
            _facingOverrideDir = null;

            if (_rb != null) _rb.simulated = true;
            if (spriteRenderer != null) spriteRenderer.color = Color.white;
            if (spriteVisual != null)
            {
                spriteVisual.localPosition = _spriteBaseLocalPos;
                spriteVisual.localScale = Vector3.one;
            }

            _iconTween?.Kill();
            if (warningIcon != null)
                warningIcon.localScale = Vector3.zero;
        }

        private void Update()
        {
            if (_isDead) return;

            if (_isCatchingPlayer)
            {
                UpdateCatchingPlayer();
                return; // 포획 연출 중엔 다른 어떤 로직도 돌지 않는다
            }

            bool lit = VisionController.Instance != null && VisionController.Instance.IsPointLit(transform.position);
            bool litByFlashlight = VisionController.Instance != null && VisionController.Instance.IsPointLitByFlashlight(transform.position);

            UpdateLightExposure(litByFlashlight); // 처치 판정은 손전등 부채꼴에만 반응
            if (_isDead) return;

            if (_killPhase == KillPhase.Stagger)
            {
                // 경직 중엔 완전히 멈춘다(배회/추격/감지 판단 자체를 건너뜀)
                _moveInput = Vector2.zero;
                _isNoticing = false;
                _facingOverrideDir = null;
            }
            else if (_state == State.Chase)
            {
                // 이미 추격 중: 빛을 놓치면 유예 시간 뒤 배회로 복귀
                if (lit)
                {
                    _lostSightTimer = 0f;
                }
                else
                {
                    _lostSightTimer += Time.deltaTime;
                    if (_lostSightTimer >= loseSightGrace)
                        _state = State.Wander;
                }

                UpdateChase();
            }
            else if (lit)
            {
                // 아직 추격 전: 처음 감지한 순간 곧바로 뛰지 않고, 제자리에서 플레이어를 바라보며 잠깐 버틴다.
                if (!_isNoticing)
                {
                    _isNoticing = true;
                    _noticeTimer = 0f;
                }

                _noticeTimer += Time.deltaTime;
                _moveInput = Vector2.zero;

                if (player != null)
                    _facingOverrideDir = ((Vector2)player.position - (Vector2)transform.position).normalized;

                if (_noticeTimer >= noticeDuration)
                {
                    _isNoticing = false;
                    _facingOverrideDir = null;
                    _state = State.Chase;
                    _lostSightTimer = 0f;
                    _repathTimer = 0f; // 전환 즉시 경로 새로 계산
                }
            }
            else
            {
                // 빛이 없는 평소 상태: 감지 중이었다면 취소하고 배회를 이어간다.
                _isNoticing = false;
                _facingOverrideDir = null;
                UpdateWander();
            }

            UpdateAnimator();
        }

        /// <summary>
        /// 빛 노출 시간을 누적해 경고 → 경직 → 소멸 단계를 판정한다.
        /// 빛이 끊기면 누적 시간을 즉시 0으로 되돌린다(어느 단계든 바로 풀림).
        /// 노출 중에는 배터리도 평소보다 더 빠르게 소모시킨다.
        /// </summary>
        private void UpdateLightExposure(bool lit)
        {
            if (_isCatchingPlayer) return; // 플레이어를 잡는 중엔 손전등 처치 판정 자체를 중단

            if (lit)
            {
                _exposureTimer += Time.deltaTime;
                _unlitTimer = 0f;

                if (extraDrainMultiplier > 1f && BatterySystem.Instance != null)
                {
                    float extraPerSecond = BatterySystem.Instance.DrainPerSecond * (extraDrainMultiplier - 1f);
                    BatterySystem.Instance.Drain(extraPerSecond * Time.deltaTime);
                }
            }
            else if (_exposureTimer > 0f)
            {
                // 빛이 끊긴 순간 바로 풀리지 않고, 지금 상태(경고/경직) 그대로 유예 시간만큼 유지하다가 풀린다.
                _unlitTimer += Time.deltaTime;
                if (_unlitTimer >= exposureReleaseDelay)
                {
                    _exposureTimer = 0f;
                    _unlitTimer = 0f;
                }
            }

            KillPhase newPhase;
            if (_exposureTimer <= 0f)
                newPhase = KillPhase.None;
            else if (_exposureTimer < warningDuration)
                newPhase = KillPhase.Warning;
            else if (_exposureTimer < warningDuration + staggerDuration)
                newPhase = KillPhase.Stagger;
            else
            {
                Die();
                return;
            }

            if (newPhase != _killPhase)
            {
                _killPhase = newPhase;
                ApplyPhaseTint();

                if (_killPhase == KillPhase.Warning)
                    ShowWarningIcon();
                else
                    HideWarningIcon(); // None으로 풀렸든 Stagger로 넘어갔든, 경고 단계를 벗어났으면 숨김
            }

            if (_killPhase == KillPhase.Stagger)
                ApplyStaggerJitter();
            else if (spriteVisual != null)
                spriteVisual.localPosition = _spriteBaseLocalPos;
        }

        /// <summary>경고 단계에 들어가는 순간 느낌표를 살짝 튀어오르듯 보여준다.</summary>
        private void ShowWarningIcon()
        {
            if (warningIcon == null) return;

            _iconTween?.Kill();
            warningIcon.localScale = Vector3.zero;
            _iconTween = warningIcon.DOScale(_warningIconScale, warningIconPopDuration).SetEase(Ease.OutBack);
        }

        /// <summary>경고 단계를 벗어나면(경직으로 넘어가든, 빛을 놓쳐 풀리든) 느낌표를 훅 숨긴다.</summary>
        private void HideWarningIcon()
        {
            if (warningIcon == null) return;

            _iconTween?.Kill();
            _iconTween = warningIcon.DOScale(Vector3.zero, warningIconHideDuration).SetEase(Ease.InQuad);
        }

        private void ApplyPhaseTint()
        {
            if (spriteRenderer == null) return;

            spriteRenderer.color = _killPhase switch
            {
                KillPhase.Warning => warningColor,
                KillPhase.Stagger => staggerColor,
                _ => Color.white,
            };
        }

        private void ApplyStaggerJitter()
        {
            if (spriteVisual == null) return;

            Vector2 offset = UnityEngine.Random.insideUnitCircle * staggerJitterStrength;
            spriteVisual.localPosition = _spriteBaseLocalPos + (Vector3)offset;
        }

        /// <summary>경직이 끝까지 유지돼 소멸이 확정됐을 때 호출. 축소+페이드 후 비활성화한다.</summary>
        private void Die()
        {
            _isDead = true;
            _moveInput = Vector2.zero;
            _rb.linearVelocity = Vector2.zero;
            _rb.simulated = false;

            _iconTween?.Kill();
            if (warningIcon != null)
                warningIcon.localScale = Vector3.zero;

            OnKilled?.Invoke();

            Transform dissolveTarget = spriteVisual != null ? spriteVisual : transform;
            Sequence seq = DOTween.Sequence();
            seq.Join(dissolveTarget.DOScale(0f, dissolveDuration).SetEase(Ease.InBack));

            if (spriteRenderer != null)
            {
                seq.Join(DOTween.To(() => spriteRenderer.color.a,
                    a =>
                    {
                        Color c = spriteRenderer.color;
                        c.a = a;
                        spriteRenderer.color = c;
                    }, 0f, dissolveDuration));
            }

            seq.OnComplete(() => gameObject.SetActive(false));
        }

        /// <summary>포획이 시작되는 순간 호출. 몬스터와 플레이어 모두 그 자리에 멈추고, 연출 시간 뒤 게임오버를 확정한다.</summary>
        private void StartCatchingPlayer()
        {
            _isCatchingPlayer = true;
            _catchTimer = 0f;
            _moveInput = Vector2.zero;

            if (player != null)
            {
                PlayerController pc = player.GetComponent<PlayerController>();
                if (pc != null)
                    pc.SetMovementEnabled(false);
            }

            PlayerCatchSignal.RaiseCatchStarted();
        }

        private void UpdateCatchingPlayer()
        {
            _moveInput = Vector2.zero;
            UpdateAnimator();

            _catchTimer += Time.deltaTime;
            if (_catchTimer >= catchGraceDuration)
                PlayerCatchSignal.Raise(); // 게임오버 확정. 이후 GameManager가 화면 전환을 처리한다.
        }

        private void FixedUpdate()
        {
            _rb.linearVelocity = _moveInput;
        }

        private void UpdateChase()
        {
            if (player == null || maze == null || maze.OpenGrid == null)
            {
                _moveInput = Vector2.zero;
                return;
            }

            if (Vector2.Distance(transform.position, player.position) <= catchDistance)
            {
                StartCatchingPlayer();
                return;
            }

            _repathTimer -= Time.deltaTime;
            if (_repathTimer <= 0f)
            {
                _repathTimer = repathInterval;
                RepathTo(player.position);
            }

            FollowPath(chaseSpeed);
        }

        private void UpdateWander()
        {
            if (maze == null || maze.OpenGrid == null)
            {
                _moveInput = Vector2.zero;
                return;
            }

            bool arrivedOrEmpty = _pathIndex >= _path.Count;

            if (arrivedOrEmpty)
            {
                if (_wanderPauseTimer > 0f)
                {
                    _wanderPauseTimer -= Time.deltaTime;
                    _moveInput = Vector2.zero;
                    return;
                }

                PickWanderTarget();
                _wanderPauseTimer = UnityEngine.Random.Range(wanderPauseRange.x, wanderPauseRange.y);
            }

            FollowPath(wanderSpeed);
        }

        /// <summary>현재 위치 기준 무작위 방향으로 배회 목표 칸을 골라 경로를 계산한다.</summary>
        private void PickWanderTarget()
        {
            Vector2 origin = maze.GridOrigin;
            float cellSize = maze.CellSize;
            bool[,] grid = maze.OpenGrid;

            Vector2Int start = GridPathfinder.WorldToCell(transform.position, origin, cellSize);

            for (int attempt = 0; attempt < 6; attempt++)
            {
                int dx = UnityEngine.Random.Range(-wanderRangeTiles, wanderRangeTiles + 1);
                int dy = UnityEngine.Random.Range(-wanderRangeTiles, wanderRangeTiles + 1);
                Vector2Int target = start + new Vector2Int(dx, dy);

                if (target.x < 0 || target.y < 0 || target.x >= grid.GetLength(0) || target.y >= grid.GetLength(1))
                    continue;
                if (!grid[target.x, target.y]) continue;

                if (GridPathfinder.TryFindPath(grid, start, target, _path))
                {
                    _pathIndex = 0;
                    return;
                }
            }

            _path.Clear();
            _pathIndex = 0;
        }

        private void RepathTo(Vector3 targetWorld)
        {
            Vector2 origin = maze.GridOrigin;
            float cellSize = maze.CellSize;
            bool[,] grid = maze.OpenGrid;

            Vector2Int start = GridPathfinder.WorldToCell(transform.position, origin, cellSize);
            Vector2Int goal = GridPathfinder.WorldToCell(targetWorld, origin, cellSize);

            if (!GridPathfinder.TryFindPath(grid, start, goal, _path))
                _path.Clear();

            _pathIndex = 0;
        }

        /// <summary>_path의 다음 칸을 향해 지정된 속도로 이동한다. 도착하면 다음 칸으로 인덱스를 올린다.</summary>
        private void FollowPath(float speed)
        {
            if (_pathIndex >= _path.Count)
            {
                _moveInput = Vector2.zero;
                return;
            }

            Vector2 targetWorld = GridPathfinder.CellToWorldCenter(_path[_pathIndex], maze.GridOrigin, maze.CellSize);
            Vector2 toTarget = targetWorld - (Vector2)transform.position;

            if (toTarget.magnitude <= waypointReachDist)
            {
                _pathIndex++;
                if (_pathIndex >= _path.Count)
                {
                    _moveInput = Vector2.zero;
                    return;
                }
                targetWorld = GridPathfinder.CellToWorldCenter(_path[_pathIndex], maze.GridOrigin, maze.CellSize);
                toTarget = targetWorld - (Vector2)transform.position;
            }

            _moveInput = toTarget.normalized * speed;
        }

        private void UpdateAnimator()
        {
            if (animator == null) return;

            bool isMoving = _moveInput.sqrMagnitude > 0.01f;
            animator.SetBool(IsMovingHash, isMoving);

            if (isMoving)
            {
                Vector2 dir = _moveInput.normalized;
                animator.SetFloat(MoveXHash, dir.x);
                animator.SetFloat(MoveYHash, dir.y);
            }
            else if (_facingOverrideDir.HasValue)
            {
                // 감지 대기 중: 움직이진 않지만 애니메이터 방향값만 플레이어 쪽으로 돌려 Idle 포즈가 그쪽을 보게 한다.
                Vector2 dir = _facingOverrideDir.Value;
                animator.SetFloat(MoveXHash, dir.x);
                animator.SetFloat(MoveYHash, dir.y);
            }

            animator.speed = _state == State.Chase ? chaseAnimSpeed : wanderAnimSpeed;
        }


        /// <summary>텔레포트 함정 등으로 위치가 갑자기 바뀌었을 때 호출. 기존 경로/추격 상태를 버리고
        /// 새 위치에서 배회부터 다시 시작한다.</summary>
        public void OnTeleported()
        {
            _path.Clear();
            _pathIndex = 0;
            _state = State.Wander;
            _isNoticing = false;
            _noticeTimer = 0f;
            _facingOverrideDir = null;
            _lostSightTimer = 0f;
            _wanderPauseTimer = 0f; // 즉시 새 배회 목표를 고르게
            _moveInput = Vector2.zero;
        }
    }
}