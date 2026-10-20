using UnityEngine;

namespace Rishi.World
{
    /// <summary>潮汐配置：海平面随游戏时间涨落（半日潮，一天两次涨落）。资产在 Assets/Config/Tide.asset。</summary>
    [CreateAssetMenu(menuName = "Rishi/潮汐配置", fileName = "Tide")]
    public class TideConfig : ScriptableObject
    {
        [Tooltip("潮差的一半（米）：高潮时海平面 +amplitude，低潮时 -amplitude")]
        [Range(0f, 2f)] public float amplitude = 0.8f;
        [Tooltip("一次涨落的周期（游戏小时）。真实的半日潮约 12.42 小时")]
        public float periodHours = 12.42f;
        [Tooltip("第一天的第一次高潮在几点（游戏时间）。默认 10:06：清晨 7 点醒来时正在涨潮，傍晚 4 点多退到最低，深夜 10 点半又是高潮")]
        public float firstHighTideHour = 10.1f;

        /// <summary>从第一天 0 点起算的游戏小时数对应的海平面高度（米）。</summary>
        public float LevelAt(float hours) => amplitude * Mathf.Cos(2f * Mathf.PI * (hours - firstHighTideHour) / periodHours);

        /// <summary>这一刻是否在涨潮。</summary>
        public bool RisingAt(float hours) => Mathf.Sin(2f * Mathf.PI * (hours - firstHighTideHour) / periodHours) < 0f;
    }
}
