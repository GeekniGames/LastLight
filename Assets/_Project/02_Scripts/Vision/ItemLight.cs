using UnityEngine;

namespace LastLight.Vision
{
    /// <summary>
    /// 배터리팩처럼 손전등과 무관하게 항상 은은히 빛나야 하는 아이템에 붙인다.
    /// 켜질 때 VisionController에 스스로 등록하고, 꺼지거나 파괴될 때(=획득됐을 때) 자동으로 해제된다.
    /// 발광 색/반경/세기/펄스 속도는 VisionController의 Item Glow 설정 하나로 모든 아이템에 공통 적용된다.
    /// </summary>
    public class ItemLight : MonoBehaviour
    {
        private void OnEnable()
        {
            if (VisionController.Instance != null)
                VisionController.Instance.RegisterItemLight(transform);
        }

        // 만약 이 오브젝트의 OnEnable이 VisionController.Awake보다 먼저 실행돼서 등록에 실패했다면
        // (Instance가 그때는 null이었다면) 여기서 한 번 더 시도한다. 이미 등록됐으면 조용히 무시된다.
        private void Start()
        {
            if (VisionController.Instance != null)
                VisionController.Instance.RegisterItemLight(transform);
        }

        private void OnDisable()
        {
            if (VisionController.Instance != null)
                VisionController.Instance.UnregisterItemLight(transform);
        }
    }
}