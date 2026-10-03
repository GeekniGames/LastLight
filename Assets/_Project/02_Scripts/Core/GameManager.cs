using DG.Tweening;
using LastLight.Maze;
using LastLight.UI;
using UnityEngine;

namespace LastLight.Core
{
    /// <summary>
    /// 층 진행을 총괄한다. 시작할 때 1층을 만들고, 플레이어가 출구에 닿으면
    /// 화면을 잠깐 어둡게 페이드했다가 다음 층 미로를 만들고 다시 밝게 돌아온다.
    /// 배터리는 층이 바뀌어도 초기화하지 않는다(배터리팩으로만 충전).
    /// 플레이어가 몬스터에게 잡히면(PlayerCatchSignal.Caught) 게임을 멈추고 게임오버 화면을 띄운다.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MazeBuilder maze;
        [SerializeField] private GameOverController gameOverController;

        [Header("Floor Transition Fade")]
        [Tooltip("화면 전체를 덮는 검정 Image가 붙은 CanvasGroup. 평소 alpha는 0.")]
        [SerializeField] private CanvasGroup fadeCanvasGroup;
        [SerializeField] private float fadeDuration = 0.3f;

        public int CurrentFloor { get; private set; } = 1;

        private bool _transitioning;
        private int _bestFloorAtRunStart;

        private void OnEnable()
        {
            ExitTrigger.Reached += HandleExitReached;
            PlayerCatchSignal.Caught += HandleCaught;
        }

        private void OnDisable()
        {
            ExitTrigger.Reached -= HandleExitReached;
            PlayerCatchSignal.Caught -= HandleCaught;
        }

        private void Start()
        {
            Time.timeScale = 1f; // 혹시 이전 판에서 멈춰 있었다면 확실하게 풀어줌

            if (fadeCanvasGroup != null)
                fadeCanvasGroup.alpha = 0f;

            _bestFloorAtRunStart = SaveData.BestFloor; // 이번 판 시작 전 기록. 신기록 여부 판단에 사용.

            maze.BuildFloor(CurrentFloor);
            SaveData.ReportFloor(CurrentFloor);
        }

        private void HandleExitReached()
        {
            if (_transitioning) return; // 페이드 중 중복 진입 방지
            _transitioning = true;

            float half = fadeDuration * 0.5f;

            Sequence seq = DOTween.Sequence();

            if (fadeCanvasGroup != null)
                seq.Append(fadeCanvasGroup.DOFade(1f, half).SetEase(Ease.InQuad));
            else
                seq.AppendInterval(half); // 페이드용 CanvasGroup이 없어도 흐름은 그대로 유지

            seq.AppendCallback(() =>
            {
                CurrentFloor++;
                maze.BuildFloor(CurrentFloor);
                SaveData.ReportFloor(CurrentFloor);
            });

            if (fadeCanvasGroup != null)
                seq.Append(fadeCanvasGroup.DOFade(0f, half).SetEase(Ease.OutQuad));
            else
                seq.AppendInterval(half);

            seq.OnComplete(() => _transitioning = false);
        }

        /// <summary>플레이어가 몬스터에게 잡혀 게임오버가 확정됐을 때(WandererMonster가 쏘는 신호).</summary>
        private void HandleCaught()
        {
            bool isNewRecord = CurrentFloor > _bestFloorAtRunStart;

            if (gameOverController != null)
                gameOverController.Show(CurrentFloor, isNewRecord);

            Time.timeScale = 0f; // 게임 월드 전체를 멈춘다(UI 버튼 입력은 영향 없음)
        }
    }
}