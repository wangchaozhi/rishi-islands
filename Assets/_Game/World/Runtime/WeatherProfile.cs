using UnityEngine;

namespace Rishi.World
{
    /// <summary>
    /// 一种天气的参数：钟点、海浪、雾、风、闪电。目前只有 CG01 的风暴在用（Assets/Config/Weather_Storm.asset），
    /// 动态天气系统（开发路线 2w）会为晴、多云、小雨等各建一份。
    /// </summary>
    [CreateAssetMenu(menuName = "Rishi/天气", fileName = "Weather")]
    public class WeatherProfile : ScriptableObject
    {
        [Tooltip(">= 0 时把时间固定在这个钟点（风暴用深夜）")]
        public float forceTimeOfDay = -1f;
        [Tooltip("海浪倍数")]
        public float waveScale = 1f;
        public float fogDensity = 0.0015f;
        public Color fogColor = new Color(0.44f, 0.56f, 0.7f);
        [Tooltip("叠加到风强度上的加成")]
        public float windBoost;
        public bool lightning;
        [Tooltip("随机闪电的间隔范围（秒）")]
        public Vector2 flashInterval = new Vector2(3.5f, 8f);

        /// <summary>CG01 海上风暴的默认参数：深夜、大浪、浓雾、狂风、闪电。</summary>
        public static WeatherProfile CreateStorm()
        {
            var w = CreateInstance<WeatherProfile>();
            w.name = "Weather_Storm";
            w.forceTimeOfDay = 1.5f;
            w.waveScale = 1.55f;
            w.fogDensity = 0.0016f;
            w.fogColor = new Color(0.03f, 0.04f, 0.05f);
            w.windBoost = 2.5f;
            w.lightning = true;
            w.flashInterval = new Vector2(3.5f, 8f);
            return w;
        }
    }
}
