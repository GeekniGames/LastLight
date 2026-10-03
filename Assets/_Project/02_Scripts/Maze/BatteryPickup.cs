using LastLight.Core;
using LastLight.Player;
using UnityEngine;

namespace LastLight.Maze
{
    /// <summary>
    /// 배터리팩(Trigger Collider2D 필요)에 붙는 스크립트.
    /// 플레이어가 닿으면 BatterySystem을 최대치 대비 비율만큼 충전하고 비활성화된다.
    /// 오브젝트 풀에서 재사용되므로 Destroy하지 않고 SetActive(false)만 한다.
    /// 비활성화되면 붙어 있는 ItemLight의 OnDisable이 자동으로 호출돼 발광도 같이 꺼진다.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class BatteryPickup : MonoBehaviour
    {
        [Tooltip("먹었을 때 채워지는 양(최대 배터리 대비 비율, 0~1). MazeBuilder가 배치할 때 값을 덮어쓸 수 있다.")]
        [Range(0f, 1f)]
        [SerializeField] private float rechargePercent = 0.28f;

        private void Reset()
        {
            var col = GetComponent<Collider2D>();
            col.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponent<PlayerController>() == null) return;

            if (BatterySystem.Instance != null)
                BatterySystem.Instance.AddPercent(rechargePercent);

            gameObject.SetActive(false);
        }

        /// <summary>MazeBuilder가 층을 배치할 때 충전량을 지정하고 싶으면 호출.</summary>
        public void SetRechargePercent(float percent)
        {
            rechargePercent = Mathf.Clamp01(percent);
        }
    }
}