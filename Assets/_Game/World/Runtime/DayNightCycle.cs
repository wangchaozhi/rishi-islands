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
        [Tooltip("昼夜参数（Assets/Config/DayNight.asset）；为空时用默认值")]
        public DayNightConfig config;
        [Range(0, 24)] public float timeOfDay = 7f;
        public bool running = true;

        [Header("引用")]
        public Light sun;
        public Light moon;
        public ReflectionProbe skyProbe;

        [Tooltip(">= 0 时强制使用这个雾浓度（风暴等事件用）")]
        public float fogOverride = -1f;

        static readonly int SunDirId = Shader.PropertyToID("_IslandSunDir");
        static readonly int MoonDirId = Shader.PropertyToID("_IslandMoonDir");

        static DayNightConfig defaults;
        /// <summary>当前生效的配置：没指定资产时用一份默认值。</summary>
        public DayNightConfig Config
        {
            get
            {
                if (config != null) return config;
                if (defaults == null)
                {
                    defaults = ScriptableObject.CreateInstance<DayNightConfig>();
                    defaults.hideFlags = HideFlags.HideAndDontSave;
                }
                return defaults;
            }
        }

        [Header("天气（WeatherSystem 写入，1 / 0 为不影响）")]
        public float weatherSun = 1f;
        [Tooltip("阴影浓度倍数（阴天影子变淡）")]
        public float weatherShadow = 1f;
        public float weatherAmbient = 1f;
        public float weatherFog = 1f;
        public Color weatherFogColor = Color.gray;
        [Range(0, 1)] public float weatherFogTint;

        [Header("水下（UnderwaterFX 写入）")]
        [Range(0, 1)] public float underwater;
        public Color underwaterColor = new Color(0.05f, 0.3f, 0.36f);
        public float underwaterDensity = 0.05f;

        float baseShadow = -1f; // 场景里太阳原本的阴影浓度
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

        void Awake()
        {
            if (Application.isPlaying) timeOfDay = Config.startTime; // 改了配置里的起始钟点不用重建场景
        }

        void Update()
        {
            var c = Config;
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
                    float speed = 24f / (c.dayLengthMinutes * 60f);
                    if (c.slowUntilFirstFragment && GameSignals.Fragments == 0) speed *= c.firstDaySpeed;
                    var kb = Keyboard.current;
                    if (kb != null && kb.tKey.isPressed) speed *= c.fastForwardMultiplier;
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
            var c = Config;
            if (c.sunColor == null || c.sunColor.colorKeys.Length < 2) c.sunColor = DayNightConfig.DefaultSunColor();

            float angle = (timeOfDay - 6f) / 24f * 360f;
            sun.transform.rotation = Quaternion.Euler(0, c.sunAzimuth, 0) * Quaternion.Euler(angle, 0, 0);
            Vector3 toSun = -sun.transform.forward;
            // 月亮不在太阳正对面：落后 moonLag 度、轨道略偏，清晨能看到残月挂在西边
            Vector3 toMoon = -(Quaternion.Euler(0, c.sunAzimuth + 12f, 0) * Quaternion.Euler(angle + 180f - c.moonLag, 0, 0) * Vector3.forward);
            SunElevation = toSun.y;

            float day = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-0.05f, 0.3f, toSun.y));
            float sunset = Mathf.Clamp01(1 - Mathf.Abs(toSun.y - 0.03f) / 0.22f) * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-0.2f, 0f, toSun.y));
            float night = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-0.2f, 0.02f, toSun.y));

            sun.intensity = c.sunMaxIntensity * day * weatherSun;
            if (baseShadow < 0f) baseShadow = sun.shadowStrength;
            sun.shadowStrength = baseShadow * Mathf.Clamp01(weatherShadow);
            sun.color = c.sunColor.Evaluate(Mathf.Clamp01(toSun.y * 2.5f));
            sun.enabled = toSun.y > -0.06f;

            if (moon != null)
            {
                moon.transform.rotation = Quaternion.LookRotation(-toMoon);
                moon.intensity = c.moonIntensity * night;
                moon.enabled = night > 0.01f && toMoon.y > 0.02f;
            }
            RenderSettings.sun = sun.enabled || moon == null ? sun : moon;

            Shader.SetGlobalVector(SunDirId, toSun);
            Shader.SetGlobalVector(MoonDirId, toMoon);

            RenderSettings.ambientIntensity = Mathf.Lerp(c.nightAmbient, c.dayAmbient * weatherAmbient, day);
            Color fog = Color.Lerp(c.nightFog, c.dayFog, day);
            fog = Color.Lerp(fog, c.sunsetFog, sunset * 0.7f);
            RenderSettings.fogColor = fog;
            // 清晨薄雾：5 点起渐浓，日出后约 8 点半散去
            float mist = Mathf.Clamp01(1f - Mathf.Abs(timeOfDay - 7.0f) / 2.0f);
            RenderSettings.fogDensity = fogOverride >= 0f ? fogOverride : c.baseFogDensity * (1f + c.morningMist * mist * mist) * Mathf.Lerp(1f, c.nightFogMultiplier, night) * weatherFog;
            if (fogOverride < 0f && weatherFogTint > 0f)
            {
                // 天气的雾色按当前亮度缩放，阴雨天的白天是灰白、夜里仍然是暗的
                float lum = Mathf.Max(fog.maxColorComponent, 0.02f);
                RenderSettings.fogColor = Color.Lerp(fog, weatherFogColor * lum / Mathf.Max(weatherFogColor.maxColorComponent, 0.02f), weatherFogTint);
            }
            if (underwater > 0f)
            {
                RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, underwaterColor, underwater);
                RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, underwaterDensity, underwater);
            }

            if (!updateEnvironment) return;
            envTimer -= Time.deltaTime;
            if (envTimer <= 0f)
            {
                envTimer = c.environmentUpdateInterval;
                DynamicGI.UpdateEnvironment();
                if (skyProbe != null && (probeRenderId < 0 || skyProbe.IsFinishedRendering(probeRenderId)))
                    probeRenderId = skyProbe.RenderProbe();
            }
        }

        /// <summary>格式化成 hh:mm 给 HUD 显示。</summary>
        public string Clock => $"第 {Day} 天  {Mathf.FloorToInt(timeOfDay):00}:{Mathf.FloorToInt(timeOfDay % 1f * 60f):00}";
    }
}
