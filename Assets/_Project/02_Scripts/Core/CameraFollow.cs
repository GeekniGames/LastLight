using UnityEngine;

namespace LastLight.Core
{
    /// <summary>
    /// An orthographic camera that follows the player smoothly.
    /// - Move the execution order forward by 100 to move the camera before VisionController(LateUpdate).
    /// (If the order is reversed, the flashlight mask follows the camera one frame behind, causing it to misalign with the character)
    /// - Fix the number of tiles displayed horizontally, and automatically resync the zoom when the screen ratio changes.
    /// - If the target suddenly moves far away (such as during a floor transition or a teleport trap), it does not follow smoothly and moves immediately.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        [Header("Follow")]
        [SerializeField] private Transform target;
        [Tooltip("작을수록 빠르게 따라붙는다. 0.05~0.15 정도를 추천.")]
        [SerializeField] private float smoothTime = 0.08f;
        [Tooltip("카메라와 대상 사이 거리가 이 값(월드 유닛)보다 커지면 부드럽게 이동하지 않고 즉시 이동한다.")]
        [SerializeField] private float snapDistance = 12f;

        [Header("Zoom")]
        [Tooltip("화면 가로에 보일 타일 수(타일 1칸 = 1유닛 기준).")]
        [SerializeField] private float tilesVisibleWidth = 9f;

        private Camera _cam;
        private Vector3 _velocity;
        private float _lastAspect = -1f;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _cam.orthographic = true;

            ApplyZoom();
            SnapToTarget();
        }

        private void LateUpdate()
        {
            if (target == null) return;

            ApplyZoom();

            Vector3 current = transform.position;
            Vector3 goal = new Vector3(target.position.x, target.position.y, current.z);

            if ((goal - current).sqrMagnitude > snapDistance * snapDistance)
            {
                transform.position = goal;
                _velocity = Vector3.zero;
                return;
            }

            transform.position = Vector3.SmoothDamp(current, goal, ref _velocity, smoothTime);
        }

        /// <summary>대상 위치로 카메라를 즉시 옮긴다.</summary>
        public void SnapToTarget()
        {
            if (target == null) return;

            transform.position = new Vector3(target.position.x, target.position.y, transform.position.z);
            _velocity = Vector3.zero;
        }

        // 화면 비율이 바뀐 경우에만 다시 계산 (매 프레임 계산하지 않음)
        private void ApplyZoom()
        {
            if (Screen.height <= 0) return;

            float aspect = (float)Screen.width / Screen.height;
            if (Mathf.Abs(aspect - _lastAspect) < 1e-4f) return;

            _lastAspect = aspect;
            _cam.orthographicSize = tilesVisibleWidth / (2f * aspect);
        }
    }
}