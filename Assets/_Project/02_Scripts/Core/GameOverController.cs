using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastLight.UI
{
    /// <summary>
    /// 게임오버 화면. GameManager가 플레이어가 잡혔을 때(PlayerCatchSignal.Caught) 이 패널을 보여준다.
    /// 재시작/메인으로는 씬을 통째로 다시 불러오는 방식이라 모든 상태(배터리, 몬스터, UI 잠금 등)가
    /// 깨끗하게 초기화된다. '광고 보고 이어하기'는 광고 SDK 연동 전까지는 버튼만 있는 더미 상태.
    /// </summary>
    public class GameOverController : MonoBehaviour
    {
        [Header("UI")]
        [Tooltip("게임오버 UI 전체 묶음. 평소엔 꺼져 있다가 Show()에서 켜진다.")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text floorText;
        [Tooltip("이번 판이 신기록이면 켜지는 텍스트/오브젝트. 없으면 비워둬도 됨.")]
        [SerializeField] private GameObject newRecordBadge;

        [Header("Scenes")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        private void Awake()
        {
            if (panelRoot != null)
                panelRoot.SetActive(false);
        }

        /// <summary>GameManager가 게임오버 확정 시 호출.</summary>
        public void Show(int floorReached, bool isNewRecord)
        {
            if (panelRoot != null)
                panelRoot.SetActive(true);

            if (floorText != null)
                floorText.text = floorReached.ToString();

            if (newRecordBadge != null)
                newRecordBadge.SetActive(isNewRecord);
        }

        /// <summary>재시작 버튼 OnClick에 연결.</summary>
        public void OnRestartButton()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        /// <summary>메인으로 버튼 OnClick에 연결.</summary>
        public void OnMainMenuButton()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(mainMenuSceneName);
        }

        /// <summary>광고 보고 이어하기 버튼 OnClick에 연결. 광고 SDK 연동 전까지는 더미(아직 아무 동작 없음).</summary>
        public void OnWatchAdContinueButton()
        {
            // TODO: 광고 SDK 연동 후 — 보상형 광고 재생 → 성공 시 배터리 충전 + 몬스터로부터 안전한 위치로 복귀 + 패널 닫기 등 구현
        }
    }
}