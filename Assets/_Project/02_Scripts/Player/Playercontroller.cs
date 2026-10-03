using UnityEngine;

namespace LastLight.Player
{
    /// <summary>
    /// 조이스틱 입력을 받아 캐릭터를 이동시키는 컨트롤러.
    /// Rigidbody2D.linearVelocity로 이동시켜 벽(Collider2D)과의 충돌이 물리 엔진에서 자연스럽게 처리되게 함.
    /// (Unity 6부터 Rigidbody2D.velocity는 linearVelocity로 이름이 바뀜)
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private FloatingJoystick joystick;
        [SerializeField] private Animator animator;

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 1.5f;

        private Rigidbody2D _rb;
        private Vector2 _moveInput;
        private bool _movementEnabled = true;

        /// <summary>
        /// 마지막으로 이동한(=조이스틱을 기울인) 방향. 손전등이 향하는 기준으로 VisionController가 읽어간다.
        /// 입력이 없을 때는 마지막 방향을 그대로 유지한다.
        /// </summary>
        public Vector2 FacingDirection { get; private set; } = Vector2.up;

        // 문자열로 애니메이터 파라미터를 매 프레임 조회하면 비용이 크므로 해시값을 미리 캐싱
        private static readonly int MoveXHash = Animator.StringToHash("MoveX");
        private static readonly int MoveYHash = Animator.StringToHash("MoveY");
        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate; // 부드러운 시각적 움직임
        }

        private void Update()
        {
            if (!_movementEnabled)
            {
                _moveInput = Vector2.zero;
                UpdateAnimator();
                return;
            }

            // 입력 감지는 프레임마다(반응성), 실제 이동 계산은 FixedUpdate에서(물리 일관성)
            _moveInput = joystick != null ? joystick.InputVector : Vector2.zero;

            if (_moveInput.sqrMagnitude > 0.01f)
                FacingDirection = _moveInput.normalized;

            UpdateAnimator();
        }

        private void FixedUpdate()
        {
            _rb.linearVelocity = _moveInput * moveSpeed;
        }

        private void UpdateAnimator()
        {
            if (animator == null) return;

            bool isMoving = _moveInput.sqrMagnitude > 0.01f;
            animator.SetBool(IsMovingHash, isMoving);

            if (isMoving)
            {
                animator.SetFloat(MoveXHash, _moveInput.x);
                animator.SetFloat(MoveYHash, _moveInput.y);
            }
        }

        /// <summary>몬스터에게 잡혔을 때 등, 외부에서 이동 입력을 잠그고 싶을 때 호출.</summary>
        public void SetMovementEnabled(bool enabled)
        {
            _movementEnabled = enabled;
            if (!enabled)
                _moveInput = Vector2.zero;
        }
    }
}