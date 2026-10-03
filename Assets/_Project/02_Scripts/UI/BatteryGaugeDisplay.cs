using LastLight.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastLight.UI
{
    /// <summary>
    /// 인게임 HUD의 배터리 게이지. Slider 값은 매 프레임 갱신(배터리가 계속 흐르는 값이라서)하고,
    /// 퍼센트 텍스트는 정수 퍼센트가 실제로 바뀐 프레임에만 갱신해 불필요한 문자열 생성을 줄인다.
    /// </summary>
    public class BatteryGaugeDisplay : MonoBehaviour
    {
        [SerializeField] private BatterySystem battery;
        [Tooltip("Min Value 0, Max Value 1로 설정된 Slider.")]
        [SerializeField] private Slider slider;
        [SerializeField] private TMP_Text percentText;

        private int _lastPercent = -1;

        private void Update()
        {
            if (battery == null) return;

            float normalized = battery.Normalized;

            if (slider != null)
                slider.value = normalized;

            int percent = Mathf.RoundToInt(normalized * 100f);
            if (percentText != null && percent != _lastPercent)
            {
                _lastPercent = percent;
                percentText.text = percent + "%";
            }
        }
    }
}