using LastLight.Vision;
using UnityEngine;

namespace LastLight.Maze
{
    /// <summary>F6 손전등 고장. 밟으면 일정 시간 동안 손전등이 깜빡인다(VisionController가 실제 효과 처리).</summary>
    public class TorchMalfunctionTrap : TrapBase
    {
        [Header("Torch Malfunction Trap")]
        [Tooltip("손전등이 깜빡이는 지속 시간(초).")]
        [SerializeField] private float flickerDuration = 3f;

        protected override void OnTriggered(GameObject target, bool isPlayer)
        {
            // 몬스터는 손전등이 없으니 플레이어가 밟았을 때만 의미가 있다.
            if (!isPlayer || VisionController.Instance == null) return;

            VisionController.Instance.TriggerFlicker(flickerDuration);
        }
    }
}