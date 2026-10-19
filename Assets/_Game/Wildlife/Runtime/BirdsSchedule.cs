using UnityEngine;
using Rishi.Audio;
using Rishi.Core;
using Rishi.World;

namespace Rishi.Wildlife
{
    /// <summary>白天鸟群活动，入夜后飞走（关闭鸟群控制器）；天亮后回来。</summary>
    public class BirdsSchedule : MonoBehaviour
    {
        public DayNightCycle dayNight;
        public MonoBehaviour controller;
        public float nightThreshold = -0.04f;

        float check;

        void Update()
        {
            if (dayNight == null || controller == null) return;
            check -= Time.deltaTime;
            if (check > 0) return;
            check = 2f;
            bool day = dayNight.SunElevation > nightThreshold;
            if (day == controller.gameObject.activeSelf) return;
            foreach (Transform bird in controller.transform) bird.gameObject.SetActive(false);
            controller.gameObject.SetActive(day);
        }
    }
}
