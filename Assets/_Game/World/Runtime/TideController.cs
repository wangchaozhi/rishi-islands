using UnityEngine;
using Rishi.Core;

namespace Rishi.World
{
    /// <summary>
    /// 潮汐：按昼夜系统的游戏时间算出海平面，写入 GameSignals.SeaLevel（游泳、水下、鱼群、小艇都读它）、
    /// 着色器全局变量 _IslandSeaLevel（海面算水深：岸边的泡沫、涌浪、浅水区跟着潮水走）。海面网格的高度由 OceanFollow 跟上。
    /// </summary>
    [ExecuteAlways]
    public class TideController : MonoBehaviour
    {
        public TideConfig config;
        public DayNightCycle dayNight;

        static readonly int SeaLevelId = Shader.PropertyToID("_IslandSeaLevel");

        /// <summary>从第一天 0 点起算的游戏小时数。</summary>
        public float Hours => dayNight != null ? (dayNight.Day - 1) * 24f + dayNight.timeOfDay : 0f;

        void Update() => Apply();

        void OnDisable()
        {
            GameSignals.SeaLevel = 0f;
            Shader.SetGlobalFloat(SeaLevelId, 0f);
        }

        /// <summary>立刻按当前时间更新海平面（离线截图时直接调用）。</summary>
        public void Apply()
        {
            if (config == null) return;
            float h = Hours;
            float level = config.LevelAt(h);
            GameSignals.SeaLevel = level;
            GameSignals.Tide01 = config.amplitude > 0f ? Mathf.InverseLerp(-config.amplitude, config.amplitude, level) : 0.5f;
            GameSignals.TideRising = config.RisingAt(h);
            Shader.SetGlobalFloat(SeaLevelId, level);
        }
    }
}
