using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using Rishi.Core;

namespace Rishi.World
{
    /// <summary>
    /// 日夜交替：驱动太阳/月亮方向与颜色、天空、环境光、雾和反射探针。
    /// 按住 T 时间快进（调试用）。
    /// </summary>
    [ExecuteAlways]
    public class DayNightCycle : MonoBehaviour
    {
        [Range(0, 24)] public float timeOfDay = 8.5f;
        [Tooltip("现实中多少分钟走完一整天")]
        public float dayLengthMinutes = 24f;
        public bool running = true;
        public float fastForwardMultiplier = 120f;

        [Header("引用")]
        public Light sun;
        public Light moon;
        public ReflectionProbe skyProbe;

        [Header("太阳")]
        public float sunAzimuth = 35f;
        public float sunMaxIntensity = 2.3f;
        public Gradient sunColor;
        public float moonIntensity = 0.25f;
        [Tooltip("月亮落后太阳对面的角度（度）：>0 时清晨还能在西边天空看到月亮")]
        public float moonLag = 30f;

        [Header("环境")]
        public float dayAmbient = 1.5f; // 雨林林冠下全靠天光和反弹光，适当提高
        public float nightAmbient = 0.35f;
        public Color dayFog = new Color(0.44f, 0.56f, 0.7f); // 不比地平线亮，否则远景发白
        public Color sunsetFog = new Color(0.86f, 0.62f, 0.45f);
        public Color nightFog = new Color(0.03f, 0.045f, 0.075f);
        public float environmentUpdateInterval = 1.5f;

        static readonly int SunDirId = Shader.PropertyToID("_IslandSunDir");
        static readonly int MoonDirId = Shader.PropertyToID("_IslandMoonDir");

        [Header("节奏")]
        [Tooltip("拿到第一块碎片之前放慢时间，保证第一章能在白天玩完")]
        public bool slowUntilFirstFragment = true;
        public float firstDaySpeed = 0.25f;
        [Header("雾")]
        public float baseFogDensity = 0.0015f;
        [Tooltip("清晨薄雾的额外浓度（倍数）")]
        public float morningMist = 1.8f;
        [Tooltip(">= 0 时强制使用这个雾浓度（风暴等事件用）")]
        public float fogOverride = -1f;

        [Header("水下（UnderwaterFX 写入）")]
        [Range(0, 1)] public float underwater;
        public Color underwaterColor = new Color(0.05f, 0.3f, 0.36f);
        public float underwaterDensity = 0.05f;

        float envTimer;
        int probeRenderId = -1;
        float rushTarget = -1f, rushRate;

        public float SunElevation { get; private set; }
        public int Day { get; private set; } = 1;
        public bool Rushing => rushTarget >= 0f;

        /// <summary>在 seconds 秒内把时间快进到 hour 点（用于「太阳以不自然的速度落下」之类的事件）。</summary>
        public void RushTo(float hour, float seconds)
        {
            float ahead = Mathf.Repeat(hour - timeOfDay, 24f);
            rushTarget = hour;
            rushRate = ahead / Mathf.Max(seconds, 0.1f);
        }

        void Reset()
        {
            sunColor = new Gradient();
            sunColor.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.42f, 0.18f), 0f), new GradientColorKey(new Color(1f, 0.78f, 0.55f), 0.25f), new GradientColorKey(new Color(1f, 0.96f, 0.9f), 1f) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) });
        }

        void Update()
        {
            if (Application.isPlaying && running)
            {
                float before = timeOfDay;
                if (Rushing)
                {
                    float step = rushRate * Time.deltaTime;
                    if (Mathf.Repeat(rushTarget - timeOfDay, 24f) <= step) { timeOfDay = rushTarget; rushTarget = -1f; }
                    else timeOfDay = Mathf.Repeat(timeOfDay + step, 24f);
                }
                else
                {
                    float speed = 24f / (dayLengthMinutes * 60f);
                    if (slowUntilFirstFragment && GameSignals.Fragments == 0) speed *= firstDaySpeed;
                    var kb = Keyboard.current;
                    if (kb != null && kb.tKey.isPressed) speed *= fastForwardMultiplier;
                    timeOfDay = Mathf.Repeat(timeOfDay + speed * Time.deltaTime, 24f);
                }
                if (timeOfDay < before - 12f) Day++; // 过了午夜
            }
            Apply(Application.isPlaying);
        }

        /// <summary>根据 timeOfDay 设置所有光照参数。updateEnvironment 为 true 时按间隔刷新环境光和反射。</summary>
        public void Apply(bool updateEnvironment)
        {
            if (sun == null) return;
            if (sunColor == null || sunColor.colorKeys.Length < 2) Reset();

            float angle = (timeOfDay - 6f) / 24f * 360f;
            sun.transform.rotation = Quaternion.Euler(0, sunAzimuth, 0) * Quaternion.Euler(angle, 0, 0);
            Vector3 toSun = -sun.transform.forward;
            // 月亮不在太阳正对面：落后 moonLag 度、轨道略偏，清晨能看到残月挂在西边
            Vector3 toMoon = -(Quaternion.Euler(0, sunAzimuth + 12f, 0) * Quaternion.Euler(angle + 180f - moonLag, 0, 0) * Vector3.forward);
            SunElevation = toSun.y;

            float day = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-0.05f, 0.3f, toSun.y));
            float sunset = Mathf.Clamp01(1 - Mathf.Abs(toSun.y - 0.03f) / 0.22f) * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-0.2f, 0f, toSun.y));
            float night = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-0.2f, 0.02f, toSun.y));

            sun.intensity = sunMaxIntensity * day;
            sun.color = sunColor.Evaluate(Mathf.Clamp01(toSun.y * 2.5f));
            sun.enabled = toSun.y > -0.06f;

            if (moon != null)
            {
                moon.transform.rotation = Quaternion.LookRotation(-toMoon);
                moon.intensity = moonIntensity * night;
                moon.enabled = night > 0.01f && toMoon.y > 0.02f;
            }
            RenderSettings.sun = sun.enabled || moon == null ? sun : moon;

            Shader.SetGlobalVector(SunDirId, toSun);
            Shader.SetGlobalVector(MoonDirId, toMoon);

            RenderSettings.ambientIntensity = Mathf.Lerp(nightAmbient, dayAmbient, day);
            Color fog = Color.Lerp(nightFog, dayFog, day);
            fog = Color.Lerp(fog, sunsetFog, sunset * 0.7f);
            RenderSettings.fogColor = fog;
            // 清晨薄雾：5 点起渐浓，日出后约 8 点半散去
            float mist = Mathf.Clamp01(1f - Mathf.Abs(timeOfDay - 7.0f) / 2.0f);
            RenderSettings.fogDensity = fogOverride >= 0f ? fogOverride : baseFogDensity * (1f + morningMist * mist * mist);
            if (underwater > 0f)
            {
                RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, underwaterColor, underwater);
                RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, underwaterDensity, underwater);
            }

            if (!updateEnvironment) return;
            envTimer -= Time.deltaTime;
            if (envTimer <= 0f)
            {
                envTimer = environmentUpdateInterval;
                DynamicGI.UpdateEnvironment();
                if (skyProbe != null && (probeRenderId < 0 || skyProbe.IsFinishedRendering(probeRenderId)))
                    probeRenderId = skyProbe.RenderProbe();
            }
        }

        /// <summary>格式化成 hh:mm 给 HUD 显示。</summary>
        public string Clock => $"第 {Day} 天  {Mathf.FloorToInt(timeOfDay):00}:{Mathf.FloorToInt(timeOfDay % 1f * 60f):00}";
    }
}
