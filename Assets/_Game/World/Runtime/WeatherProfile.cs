using UnityEngine;

namespace Rishi.World
{
    /// <summary>
    /// 一种天气的参数。动态天气（WeatherSystem）在两种天气之间按比例混合所有数值；
    /// CG01 的海上风暴（StormFX）只用「风暴」一组字段。资产在 Assets/Config/Weather_*.asset。
    /// </summary>
    [CreateAssetMenu(menuName = "Rishi/天气", fileName = "Weather")]
    public class WeatherProfile : ScriptableObject
    {
        [Tooltip("显示给玩家的名字（F8 切换时的提示）")]
        public string displayName = "晴";

        [Header("天空与光照")]
        [Range(0, 1)] [Tooltip("云量：0 = 万里无云，1 = 满天")]
        public float cloudCover = 0.45f;
        [Range(0, 1)] [Tooltip("阴沉程度：天空变灰、太阳圆盘被遮住")]
        public float overcast;
        [Tooltip("阳光强度倍数")]
        public float sunIntensity = 1f;
        [Tooltip("白天环境光倍数")]
        public float ambientIntensity = 1f;
        [Tooltip("体积光（光柱）倍数")]
        public float volumetric = 1f;

        [Header("雾")]
        [Tooltip("雾浓度倍数（动态天气用）")]
        public float fogMultiplier = 1f;
        [Range(0, 1)] [Tooltip("雾色往 fogColor 靠的程度")]
        public float fogTint;

        [Header("风与海")]
        [Tooltip("风力倍数（植物摆幅）")]
        public float windStrength = 1f;
        [Tooltip("叠加到风强度上的加成")]
        public float windBoost;
        [Tooltip("海浪倍数")]
        public float waveScale = 1f;

        [Header("雨与雷")]
        [Range(0, 1)] [Tooltip("雨量：雨幕、雨声、地面变湿、水面涟漪")]
        public float rain;
        [Range(0, 1)] [Tooltip("压低白天鸟叫的程度")]
        public float birdDamp;
        public bool lightning;
        [Tooltip("随机闪电的间隔范围（秒）")]
        public Vector2 flashInterval = new Vector2(3.5f, 8f);

        [Header("风暴（CG01 用）")]
        [Tooltip(">= 0 时把时间固定在这个钟点（风暴用深夜）")]
        public float forceTimeOfDay = -1f;
        [Tooltip("雾浓度（绝对值）")]
        public float fogDensity = 0.0015f;
        public Color fogColor = new Color(0.44f, 0.56f, 0.7f);

        /// <summary>CG01 海上风暴的默认参数：深夜、大浪、浓雾、狂风、闪电。</summary>
        public static WeatherProfile CreateStorm()
        {
            var w = CreateInstance<WeatherProfile>();
            w.name = "Weather_Storm";
            w.displayName = "海上风暴";
            w.forceTimeOfDay = 1.5f;
            w.waveScale = 1.55f;
            w.fogDensity = 0.0016f;
            w.fogColor = new Color(0.03f, 0.04f, 0.05f);
            w.windBoost = 2.5f;
            w.lightning = true;
            w.flashInterval = new Vector2(3.5f, 8f);
            w.cloudCover = 1f;
            w.overcast = 1f;
            w.rain = 1f;
            w.birdDamp = 1f;
            return w;
        }

        /// <summary>动态天气的六种默认天气，按 晴 / 多云 / 阴 / 小雨 / 暴雨雷暴 / 浓雾 的顺序。</summary>
        public static WeatherProfile CreateDefault(string id)
        {
            var w = CreateInstance<WeatherProfile>();
            w.name = "Weather_" + id;
            switch (id)
            {
                case "Clear":
                    w.displayName = "晴"; w.cloudCover = 0.3f;
                    break;
                case "Cloudy":
                    w.displayName = "多云"; w.cloudCover = 0.62f; w.sunIntensity = 0.85f; w.volumetric = 1.1f; w.windStrength = 1.2f;
                    break;
                case "Overcast":
                    w.displayName = "阴"; w.cloudCover = 0.95f; w.overcast = 0.75f; w.sunIntensity = 0.3f; w.ambientIntensity = 0.75f;
                    w.volumetric = 0.3f; w.fogMultiplier = 1.6f; w.fogTint = 0.6f; w.fogColor = new Color(0.55f, 0.58f, 0.6f);
                    w.windStrength = 1.3f; w.waveScale = 1.15f; w.birdDamp = 0.3f;
                    break;
                case "Rain":
                    w.displayName = "小雨"; w.cloudCover = 1f; w.overcast = 0.85f; w.sunIntensity = 0.22f; w.ambientIntensity = 0.65f;
                    w.volumetric = 0.25f; w.fogMultiplier = 2.4f; w.fogTint = 0.75f; w.fogColor = new Color(0.5f, 0.54f, 0.57f);
                    w.windStrength = 1.5f; w.waveScale = 1.25f; w.rain = 0.45f; w.birdDamp = 0.9f;
                    break;
                case "Thunderstorm":
                    w.displayName = "暴雨雷暴"; w.cloudCover = 1f; w.overcast = 1f; w.sunIntensity = 0.1f; w.ambientIntensity = 0.45f;
                    w.volumetric = 0.15f; w.fogMultiplier = 3.5f; w.fogTint = 0.85f; w.fogColor = new Color(0.32f, 0.35f, 0.39f);
                    w.windStrength = 1.8f; w.windBoost = 1.2f; w.waveScale = 1.5f; w.rain = 1f; w.birdDamp = 1f;
                    w.lightning = true; w.flashInterval = new Vector2(6f, 16f);
                    break;
                case "Fog":
                    w.displayName = "浓雾"; w.cloudCover = 0.8f; w.overcast = 0.55f; w.sunIntensity = 0.45f; w.ambientIntensity = 0.8f;
                    w.volumetric = 1.6f; w.fogMultiplier = 9f; w.fogTint = 0.85f; w.fogColor = new Color(0.66f, 0.7f, 0.72f);
                    w.windStrength = 0.4f; w.waveScale = 0.7f; w.birdDamp = 0.5f;
                    break;
            }
            return w;
        }
    }
}
