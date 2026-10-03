using LastLight.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LastLight.UI
{
    /// <summary>
    /// 손전등 ON/OFF 버튼의 아이콘 이미지와 배경 색을 BatterySystem.IsOn 상태에 맞춰 갱신한다.
    /// 버튼을 누르는 동작(OnClick)은 Inspector에서 BatterySystem.Toggle()로 직접 연결하면 되고,
    /// 이 스크립트는 "지금 상태가 어떻게 보이는지" 표시와, 플레이어가 몬스터에게 잡히는 동안
    /// 버튼이 안 눌리게 잠그는 것까지 담당한다.
    /// </summary>
    public class FlashlightToggleButton : MonoBehaviour
    {
        [SerializeField] private BatterySystem battery;
        [Tooltip("이 버튼 자체의 Button 컴포넌트. 잡히는 동안 Interactable을 꺼서 못 누르게 한다.")]
        [SerializeField] private Button button;
        [Tooltip("버튼 안의 아이콘 이미지(켜짐/꺼짐 그림이 바뀌는 쪽).")]
        [SerializeField] private Image icon;
        [Tooltip("색이 바뀌는 배경 이미지. 버튼 자체의 Image 컴포넌트를 연결하면 된다.")]
        [SerializeField] private Image background;
        [SerializeField] private Sprite onSprite;
        [SerializeField] private Sprite offSprite;
        [SerializeField] private Color onColor = new Color(195f / 255f, 195f / 255f, 100f / 255f, 1f);
        [SerializeField] private Color offColor = new Color(50f / 255f, 50f / 255f, 50f / 255f, 1f);

        private void OnEnable()
        {
            PlayerCatchSignal.CatchStarted += HandleCatchStarted;

            if (battery == null) return;

            battery.OnToggled += HandleToggled;
            ApplyVisual(battery.IsOn); // 씬 시작 시점 상태를 바로 반영
        }

        private void OnDisable()
        {
            PlayerCatchSignal.CatchStarted -= HandleCatchStarted;

            if (battery != null)
                battery.OnToggled -= HandleToggled;
        }

        private void HandleToggled(bool isOn) => ApplyVisual(isOn);

        private void HandleCatchStarted()
        {
            if (button != null)
                button.interactable = false;
        }

        private void ApplyVisual(bool isOn)
        {
            if (icon != null)
                icon.sprite = isOn ? onSprite : offSprite;

            if (background != null)
                background.color = isOn ? onColor : offColor;
        }
    }
}