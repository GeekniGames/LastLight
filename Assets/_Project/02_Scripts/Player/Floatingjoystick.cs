using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

namespace LastLight.Player
{
    /// <summary>
    /// 화면 중앙 하단 반투명 플로팅 조이스틱.
    /// 평소엔 흐릿하게(또는 투명) 있다가, 터치한 지점으로 이동해서 표시됨.
    /// 이 스크립트는 전체 터치 영역(하단 패널) RectTransform에 붙인다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class FloatingJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("References")]
        [SerializeField] private RectTransform joystickBackground;
        [SerializeField] private RectTransform joystickHandle;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Settings")]
        [SerializeField] private float handleRange = 100f;
        [SerializeField] private float fadeDuration = 0.15f;
        [SerializeField] private float inactiveAlpha = 0.25f;

        private RectTransform _touchAreaRect;
        private Vector2 _inputVector = Vector2.zero;
        private Tween _fadeTween;

        /// <summary>-1~1 범위의 정규화된 입력 벡터. PlayerController가 매 프레임 읽는다.</summary>
        public Vector2 InputVector => _inputVector;

        private void Awake()
        {
            _touchAreaRect = (RectTransform)transform;

            if (canvasGroup != null)
                canvasGroup.alpha = inactiveAlpha;

            joystickHandle.anchoredPosition = Vector2.zero;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // 터치 지점을 로컬 좌표로 변환해 배경(조이스틱 원판)을 그 위치로 이동
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _touchAreaRect, eventData.position, eventData.pressEventCamera, out Vector2 localPoint);

            joystickBackground.anchoredPosition = localPoint;

            OnDrag(eventData);
            FadeTo(1f);
        }

        public void OnDrag(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                joystickBackground, eventData.position, eventData.pressEventCamera, out Vector2 localPoint);

            _inputVector = localPoint.magnitude > handleRange
                ? localPoint.normalized
                : localPoint / handleRange;

            joystickHandle.anchoredPosition = _inputVector * handleRange;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _inputVector = Vector2.zero;
            joystickHandle.anchoredPosition = Vector2.zero;
            FadeTo(inactiveAlpha);
        }

        private void FadeTo(float targetAlpha)
        {
            if (canvasGroup == null) return;

            _fadeTween?.Kill();
            _fadeTween = canvasGroup.DOFade(targetAlpha, fadeDuration).SetEase(Ease.OutQuad);
        }

        private void OnDestroy()
        {
            _fadeTween?.Kill();
        }
    }
}