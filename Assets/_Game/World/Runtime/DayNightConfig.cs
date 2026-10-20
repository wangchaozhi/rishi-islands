using UnityEngine;

namespace Rishi.World
{
    /// <summary>
    /// 昼夜配置：一天的长度、日月光照、环境光、雾、第一天节奏。DayNightCycle 读取它，策划在 Inspector 里直接调。
    /// 资产在 Assets/Config/DayNight.asset，重建场景不会覆盖。
    /// </summary>
    [CreateAssetMenu(menuName = "Rishi/昼夜配置", fileName = "DayNight")]
    public class DayNightConfig : ScriptableObject
    {
        [Header("时间")]
        [Range(0, 24)] [Tooltip("场景开始时的钟点")]
        public float startTime = 7f;
        [Tooltip("现实中多少分钟走完一整天")]
        public float dayLengthMinutes = 24f;
        [Tooltip("按住 T 快进的倍数")]
        public float fastForwardMultiplier = 120f;

        [Header("太阳")]
        public float sunAzimuth = 35f;
        public float sunMaxIntensity = 2.3f;
        public Gradient sunColor = DefaultSunColor();

        [Header("月亮")]
        public float moonIntensity = 0.12f;
        [Tooltip("月亮落后太阳对面的角度（度）：>0 时清晨还能在西边天空看到月亮")]
        public float moonLag = 30f;

        [Header("环境光")]
        [Tooltip("雨林林冠下全靠天光和反弹光，适当提高")]
        public float dayAmbient = 1.5f;
        [Tooltip("夜里的环境光：越低越黑，没有手电筒几乎看不清")]
        public float nightAmbient = 0.12f;
        [Tooltip("环境光与反射探针的刷新间隔（秒）")]
        public float environmentUpdateInterval = 1.5f;

        [Header("雾")]
        [Tooltip("不比地平线亮，否则远景发白")]
        public Color dayFog = new Color(0.44f, 0.56f, 0.7f);
        public Color sunsetFog = new Color(0.86f, 0.62f, 0.45f);
        public Color nightFog = new Color(0.03f, 0.045f, 0.075f);
        public float baseFogDensity = 0.0015f;
        [Tooltip("清晨薄雾的额外浓度（倍数）")]
        public float morningMist = 1.8f;
        [Tooltip("夜里的雾浓度倍数（夜里海雾更浓）")]
        public float nightFogMultiplier = 2.4f;

        [Header("节奏")]
        [Tooltip("拿到第一块碎片之前放慢时间，保证第一章能在白天玩完")]
        public bool slowUntilFirstFragment = true;
        public float firstDaySpeed = 0.25f;

        public static Gradient DefaultSunColor()
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.42f, 0.18f), 0f), new GradientColorKey(new Color(1f, 0.78f, 0.55f), 0.25f), new GradientColorKey(new Color(1f, 0.96f, 0.9f), 1f) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) });
            return g;
        }
    }
}
