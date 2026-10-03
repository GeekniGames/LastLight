using LastLight.Core;
using UnityEngine;

namespace LastLight.Maze
{
    /// <summary>F1 배터리 함정. 밟으면 배터리가 최대치 대비 비율만큼 즉시 급감한다.</summary>
    public class BatteryTrap : TrapBase
    {
        [Header("Battery Trap")]
        [Tooltip("발동 시 깎이는 배터리 양(최대 배터리 대비 비율).")]
        [Range(0f, 1f)]
        [SerializeField] private float drainPercent = 0.25f;

        protected override void OnTriggered(GameObject target, bool isPlayer)
        {
            // 몬스터는 배터리가 없으니 affectsMonsters를 켜둬도 여기서는 플레이어만 실제로 영향받는다.
            if (!isPlayer || BatterySystem.Instance == null) return;

            BatterySystem.Instance.Drain(BatterySystem.Instance.Max * drainPercent);
        }
    }
}