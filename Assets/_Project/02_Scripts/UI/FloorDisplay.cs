using LastLight.Core;
using TMPro;
using UnityEngine;

namespace LastLight.UI
{
    /// <summary>인게임 HUD의 층수 표시. GameManager.CurrentFloor 값이 바뀐 프레임에만 텍스트를 갱신한다.</summary>
    public class FloorDisplay : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private TMP_Text floorText;
        [Tooltip("표시 형식. {0} 자리에 층수가 들어간다. 예: 'B{0}' -> B1, B2 ...")]
        [SerializeField] private string format = "B{0}";

        private int _lastFloor = -1;

        private void Update()
        {
            if (gameManager == null || floorText == null) return;
            if (gameManager.CurrentFloor == _lastFloor) return;

            _lastFloor = gameManager.CurrentFloor;
            floorText.text = string.Format(format, _lastFloor);
        }
    }
}