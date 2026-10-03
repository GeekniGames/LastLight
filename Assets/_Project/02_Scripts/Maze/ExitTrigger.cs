using System;
using LastLight.Player;
using UnityEngine;

namespace LastLight.Maze
{
    /// <summary>
    /// 출구 마커(Trigger Collider2D 필요)에 붙는 스크립트.
    /// 플레이어가 닿으면 정적 이벤트를 발생시킨다. 출구 오브젝트는 층마다 재사용되므로
    /// 구독자(GameManager)는 시작할 때 한 번만 구독하면 된다.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class ExitTrigger : MonoBehaviour
    {
        /// <summary>플레이어가 출구에 닿았을 때 발생.</summary>
        public static event Action Reached;

        private void Reset()
        {
            var col = GetComponent<Collider2D>();
            col.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponent<PlayerController>() == null) return;

            Reached?.Invoke();
        }
    }
}