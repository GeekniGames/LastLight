using UnityEngine;

namespace LastLight.Vision
{
    /// <summary>
    /// 길목에 놓이는 횃불 등 고정 광원에 붙인다. 손전등 ON/OFF나 배터리 잔량과 무관하게 항상 따뜻한 빛을 낸다.
    /// 배터리팩 발광(ItemLight)과 구조는 같지만, VisionController 안에서 별도 채널(색/반경/세기)을 쓰기 때문에
    /// 배터리팩은 청록, 횃불은 주황 같은 식으로 서로 다른 색을 동시에 켤 수 있다.
    /// </summary>
    public class TorchLight : MonoBehaviour
    {
        private void OnEnable()
        {
            if (VisionController.Instance != null)
                VisionController.Instance.RegisterTorchLight(transform);
        }

        private void OnDisable()
        {
            if (VisionController.Instance != null)
                VisionController.Instance.UnregisterTorchLight(transform);
        }
    }
}