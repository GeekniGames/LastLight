using LastLight.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastLight.UI
{
    /// <summary>
    /// 메인 화면 총괄. 시작 버튼을 누르면 게임 씬으로 전환하고, 최고 기록(도달 층수)을 보여준다.
    /// 랭킹/상점/설정은 아직 세부 기능이 없어서, 버튼은 있지만 누르면 "준비 중" 안내만 짧게 보여준다.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Scene")]
        [Tooltip("시작 버튼을 눌렀을 때 전환할 게임 씬 이름. Build Settings에 등록돼 있어야 한다.")]
        [SerializeField] private string gameplaySceneName = "GameScene";

        [Header("UI")]
        [SerializeField] private TMP_Text bestFloorText;



        private void Start()
        {
            UpdateBestFloorText();
        }

        private void UpdateBestFloorText()
        {
            if (bestFloorText == null) return;

            int best = SaveData.BestFloor;
            bestFloorText.text = best > 0 ? $"최고 기록: {best}층" : "최고 기록: -";
        }

        /// <summary>시작 버튼 OnClick에 연결.</summary>
        public void OnStartButton()
        {
            SceneManager.LoadScene(gameplaySceneName);
        }

        /// <summary>랭킹 버튼 OnClick에 연결. 아직 기능 없음 — 나중에 구현.</summary>
        public void OnRankingButton() { }

        /// <summary>상점 버튼 OnClick에 연결. 아직 기능 없음 — 나중에 구현.</summary>
        public void OnShopButton() { }

        /// <summary>설정 버튼 OnClick에 연결. 아직 기능 없음 — 나중에 구현.</summary>
        public void OnSettingsButton() { }
    }
}