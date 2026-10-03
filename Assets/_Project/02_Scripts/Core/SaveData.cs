using UnityEngine;

namespace LastLight.Core
{
    /// <summary>
    /// 로컬 저장 전담. 지금은 PlayerPrefs로 최고 도달 층수만 저장한다.
    /// 뒤끝(BackEnd) 랭킹을 붙일 때, 호출부(GameManager 등)는 그대로 두고 이 파일 안쪽 구현만 바꾸면 되게
    /// 창구를 하나로 모아뒀다.
    /// </summary>
    public static class SaveData
    {
        private const string BestFloorKey = "LastLight_BestFloor";

        /// <summary>지금까지 기록된 최고 도달 층수. 기록이 없으면 0.</summary>
        public static int BestFloor => PlayerPrefs.GetInt(BestFloorKey, 0);

        /// <summary>층에 도달했을 때 호출. 기존 최고 기록보다 높을 때만 갱신한다.</summary>
        public static void ReportFloor(int floor)
        {
            if (floor <= BestFloor) return;

            PlayerPrefs.SetInt(BestFloorKey, floor);
            PlayerPrefs.Save();
        }
    }
}