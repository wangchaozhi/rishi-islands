using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Rishi.Audio;
using Rishi.Core;
using Rishi.World;

namespace Rishi.Weather
{
    /// <summary>
    /// 动态天气：按 WeatherConfig 的权重每隔几分钟换一种天气，两种天气之间平滑过渡。
    /// 联动云量与天色、阳光、环境光、雾、风、海浪、体积光；下雨时有雨幕、雨声、地面和岩石变湿、水面涟漪；
    /// 雷暴有闪电和延迟的雷声；雨天鸟叫消失。过场播放时天气效果暂停。按 F8 切到下一种天气（调试用）。
    /// </summary>
    public class WeatherSystem : MonoBehaviour
    {
        public static WeatherSystem Instance { get; private set; }

        public WeatherConfig config;
        public DayNightCycle dayNight;
        [Tooltip("雨滴粒子的材质")]
        public Material rainMaterial;

        [Header("运行状态（只读）")]
        public WeatherProfile current;
        public WeatherProfile target;
        [Range(0, 1)] public float blend = 1f;
        [Range(0, 1)] public float wetness;

        /// <summary>两种天气按比例混合后的数值。</summary>
        public struct Values
        {
            public float cloud, overcast, sun, ambient, volumetric, fog, fogTint, wind, wave, rain, birdDamp;
            public Color fogColor;
            public bool lightning;
            public Vector2 flashInterval;

            public static Values From(WeatherProfile p) => new Values
            {
                cloud = p.cloudCover, overcast = p.overcast, sun = p.sunIntensity, ambient = p.ambientIntensity,
                volumetric = p.volumetric, fog = p.fogMultiplier, fogTint = p.fogTint, fogColor = p.fogColor,
                wind = p.windStrength + p.windBoost, wave = p.waveScale, rain = p.rain, birdDamp = p.birdDamp,
                lightning = p.lightning, flashInterval = p.flashInterval,
            };

            public static Values Lerp(Values a, Values b, float t) => new Values
            {
                cloud = Mathf.Lerp(a.cloud, b.cloud, t), overcast = Mathf.Lerp(a.overcast, b.overcast, t),
                sun = Mathf.Lerp(a.sun, b.sun, t), ambient = Mathf.Lerp(a.ambient, b.ambient, t),
                volumetric = Mathf.Lerp(a.volumetric, b.volumetric, t), fog = Mathf.Lerp(a.fog, b.fog, t),
                fogTint = Mathf.Lerp(a.fogTint, b.fogTint, t), fogColor = Color.Lerp(a.fogColor, b.fogColor, t),
                wind = Mathf.Lerp(a.wind, b.wind, t), wave = Mathf.Lerp(a.wave, b.wave, t),
                rain = Mathf.Lerp(a.rain, b.rain, t), birdDamp = Mathf.Lerp(a.birdDamp, b.birdDamp, t),
                lightning = t < 0.6f ? a.lightning : b.lightning,
                flashInterval = t < 0.5f ? a.flashInterval : b.flashInterval,
            };

            /// <summary>不影响任何东西的中性值（过场时用）。</summary>
            public static Values Neutral => new Values { sun = 1, ambient = 1, volumetric = 1, fog = 1, wind = 1, wave = 1, fogColor = Color.gray };
        }

        public Values Current { get; private set; } = Values.Neutral;

        static readonly int CloudId = Shader.PropertyToID("_IslandCloudCover");
        static readonly int OvercastId = Shader.PropertyToID("_IslandOvercast");
        static readonly int VolumetricId = Shader.PropertyToID("_IslandVolumetricDim");
        static readonly int WaveId = Shader.PropertyToID("_IslandWaveBoost");
        static readonly int RainId = Shader.PropertyToID("_IslandRain");
        static readonly int SkyFlashId = Shader.PropertyToID("_IslandSkyFlash");

        WeatherProfile from;
        float timer, transition;
        ParticleSystem rain;
        AudioSource rainLoop;
        Light lightning;
        float flash, nextFlash = 5f;
        bool flashing; // 上一帧写过闪电，过场开始时要清掉一次

        void Awake()
        {
            Instance = this;
            if (config == null) config = CreateDefaultConfig();
        }

        void Start()
        {
            current = target = from = config.startWeather != null ? config.startWeather : config.weathers.Count > 0 ? config.weathers[0].profile : null;
            if (current == null) { enabled = false; return; }
            blend = 1f;
            timer = Random.Range(config.duration.x, config.duration.y);
            EnsureEffects();
            PrepareWetness();
        }

        void OnDisable()
        {
            RestoreWetness();
            ApplyGlobals(Values.Neutral, 0f);
            Shader.SetGlobalFloat(SkyFlashId, 0f);
            GameSignals.Rain = 0f;
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var kb = Keyboard.current;
            if (kb != null && kb.f8Key.wasPressedThisFrame) Next(4f, toast: true);

            // 到时间就按概率换一种天气
            if (blend >= 1f)
            {
                timer -= dt;
                if (timer <= 0f)
                {
                    bool night = dayNight != null && dayNight.SunElevation < -0.05f;
                    var next = config.Pick(night, GameSignals.Fragments, current, Random.value);
                    TransitionTo(next, config.transitionSeconds);
                }
            }
            else
            {
                blend = Mathf.Min(1f, blend + dt / Mathf.Max(transition, 0.01f));
                if (blend >= 1f) { current = target; from = target; timer = Random.Range(config.duration.x, config.duration.y); }
            }

            float t = Mathf.SmoothStep(0f, 1f, blend);
            var v = Values.Lerp(Values.From(from), Values.From(target), t);
            bool cutscene = GameSignals.CutsceneActive;
            Current = cutscene ? Values.Neutral : v;

            // 下雨时地面慢慢淋湿，雨停后慢慢晾干
            float wetTarget = Current.rain > 0.05f ? Mathf.Clamp01(Current.rain * 2f) : 0f;
            float speed = wetTarget > wetness ? 1f / Mathf.Max(config.wetDrySeconds.x, 1f) : 1f / Mathf.Max(config.wetDrySeconds.y, 1f);
            wetness = Mathf.MoveTowards(wetness, wetTarget, speed * dt);

            Apply(Current, wetness);
            UpdateRain(Current.rain);
            UpdateLightning(Current, dt, cutscene);
            GameSignals.Rain = Current.rain;
            GameSignals.Weather = (t < 0.5f ? from : target).displayName;
        }

        /// <summary>开始往另一种天气过渡。</summary>
        public void TransitionTo(WeatherProfile next, float seconds)
        {
            if (next == null) return;
            // 过渡中途再切：从当前混合到一半的样子出发，避免跳变
            if (blend < 1f) current = blend < 0.5f ? from : target;
            from = current;
            target = next;
            transition = seconds;
            blend = next == current ? 1f : 0f;
        }

        /// <summary>按配置里的顺序切到下一种天气（F8）。</summary>
        public void Next(float seconds, bool toast)
        {
            int i = config.weathers.FindIndex(e => e.profile == target);
            var next = config.weathers[(i + 1) % config.weathers.Count].profile;
            TransitionTo(next, seconds);
            if (toast) GameSignals.Toast($"天气：{next.displayName}");
        }

        /// <summary>立刻完全切换到某种天气（离线截图用，编辑器里也能调用）。wet 为 true 时地面同时变湿，用完调用 ResetVisuals 复原。</summary>
        public void ApplyNow(WeatherProfile p, bool wet)
        {
            current = target = from = p;
            blend = 1f;
            Current = Values.From(p);
            EnsureEffects();
            if (wet) PrepareWetness();
            wetness = wet ? Mathf.Clamp01(p.rain * 2f) : 0f;
            Apply(Current, wetness);
            if (dayNight != null) dayNight.Apply(false);
            UpdateRain(Current.rain);
            if (rain != null)
            {
                if (Current.rain > 0.01f) rain.Simulate(1.5f, true, true);
                else rain.Clear();
            }
        }

        /// <summary>撤掉 ApplyNow 留下的效果（离线截图结束时调用）。</summary>
        public void ResetVisuals()
        {
            RestoreWetness();
            if (rain != null) rain.Clear();
            ApplyGlobals(Values.Neutral, 0f);
            if (dayNight != null) { ApplyDayNight(Values.Neutral); dayNight.Apply(false); }
        }

        // ---------------- 应用到各个系统 ----------------

        void Apply(Values v, float wet)
        {
            ApplyGlobals(v, v.rain);
            if (dayNight != null) ApplyDayNight(v);
            if (WindController.Instance != null) WindController.Instance.weather = v.wind;
            var amb = AmbientController.Instance;
            if (amb != null)
            {
                amb.weatherBirdDamp = v.birdDamp;
                amb.weatherWind = Mathf.Max(0f, v.wind - 1f) * 0.35f + v.rain * 0.15f;
            }
            ApplyWetness(wet);
        }

        static void ApplyGlobals(Values v, float rainAmount)
        {
            Shader.SetGlobalFloat(CloudId, v.cloud);
            Shader.SetGlobalFloat(OvercastId, v.overcast);
            Shader.SetGlobalFloat(VolumetricId, 1f - v.volumetric);
            Shader.SetGlobalFloat(WaveId, v.wave - 1f);
            Shader.SetGlobalFloat(RainId, rainAmount);
        }

        void ApplyDayNight(Values v)
        {
            dayNight.weatherSun = v.sun;
            dayNight.weatherShadow = 1f - v.overcast * 0.75f;
            dayNight.weatherAmbient = v.ambient;
            dayNight.weatherFog = v.fog;
            dayNight.weatherFogColor = v.fogColor;
            dayNight.weatherFogTint = v.fogTint;
        }

        // ---------------- 雨 ----------------

        void EnsureEffects()
        {
            if (rain == null) rain = CreateRain();
            if (lightning == null)
            {
                lightning = new GameObject("Lightning").AddComponent<Light>();
                lightning.transform.SetParent(transform, false);
                lightning.type = LightType.Directional;
                lightning.color = new Color(0.75f, 0.82f, 1f);
                lightning.intensity = 0f;
                lightning.shadows = LightShadows.None;
                lightning.enabled = false;
            }
            if (rainLoop == null && Application.isPlaying)
            {
                rainLoop = gameObject.AddComponent<AudioSource>();
                rainLoop.loop = true;
                rainLoop.spatialBlend = 0f;
                rainLoop.playOnAwake = false;
                rainLoop.volume = 0f;
            }
        }

        void UpdateRain(float amount)
        {
            if (rain == null) return;
            var em = rain.emission;
            em.rateOverTime = amount * 6500f;
            var cam = Camera.main;
            if (cam != null) rain.transform.position = cam.transform.position + Vector3.up * 7f;
            if (amount > 0.01f && !rain.isPlaying) rain.Play();
            else if (amount <= 0.01f && rain.isPlaying) rain.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            if (rainLoop != null)
            {
                if (rainLoop.clip == null && AudioManager.Instance != null) rainLoop.clip = AudioManager.Instance.RandomClip("amb_rain");
                float mul = AudioManager.Instance != null ? AudioManager.Instance.CategoryVolume(SoundCategory.Ambience) : 1f;
                float under = GameSignals.HeadUnderwater ? 0.15f : 1f;
                rainLoop.volume = Mathf.MoveTowards(rainLoop.volume, Mathf.Sqrt(amount) * 0.9f * mul * under, Time.deltaTime * 0.5f);
                if (rainLoop.clip != null && rainLoop.volume > 0.001f && !rainLoop.isPlaying) rainLoop.Play();
                else if (rainLoop.volume <= 0.001f && rainLoop.isPlaying) rainLoop.Stop();
            }
        }

        ParticleSystem CreateRain()
        {
            var go = new GameObject("Rain");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = 1f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.022f);
            main.startColor = new Color(0.72f, 0.76f, 0.82f, 0.32f);
            main.maxParticles = 9000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-2.5f, -1f);
            vel.y = new ParticleSystem.MinMaxCurve(-15f, -12f);
            vel.z = new ParticleSystem.MinMaxCurve(1f, 2.5f);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(26f, 1f, 26f);
            var em = ps.emission;
            em.rateOverTime = 0f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.045f;
            r.lengthScale = 1f;
            r.sharedMaterial = rainMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        // ---------------- 闪电 ----------------

        void UpdateLightning(Values v, float dt, bool cutscene)
        {
            if (cutscene)
            {
                // 过场里的风暴自己管闪电，这里只在刚进入过场时清掉一次
                if (flashing) { flash = 0f; Shader.SetGlobalFloat(SkyFlashId, 0f); if (lightning != null) lightning.enabled = false; flashing = false; }
                return;
            }
            if (v.lightning)
            {
                nextFlash -= dt;
                if (nextFlash <= 0f)
                {
                    nextFlash = Random.Range(v.flashInterval.x, v.flashInterval.y);
                    flash = Random.Range(0.6f, 1.1f);
                    if (lightning != null) lightning.transform.rotation = Quaternion.Euler(Random.Range(35f, 70f), Random.Range(0f, 360f), 0f);
                    StartCoroutine(Thunder(Random.Range(0.6f, 3f), Random.Range(0.6f, 1f)));
                }
            }
            flash = Mathf.MoveTowards(flash, 0f, dt * 3.5f);
            float flicker = flash > 0.6f ? (Mathf.Sin(Time.time * 90f) > 0f ? 1f : 0.3f) : 1f;
            if (flash > 0f || flashing)
            {
                Shader.SetGlobalFloat(SkyFlashId, flash * flicker * 1.4f);
                if (lightning != null) { lightning.enabled = flash > 0.01f; lightning.intensity = flash * flicker * 3f; }
                flashing = flash > 0f;
            }
        }

        IEnumerator Thunder(float delay, float volume)
        {
            yield return new WaitForSeconds(delay); // 先看到闪电，过一会儿才听到雷声
            AudioManager.Play2D("thunder", volume);
        }

        // ---------------- 地面变湿 ----------------

        readonly List<TerrainData> terrainDatas = new List<TerrainData>();
        TerrainLayer[] dryLayers, wetLayers;
        readonly List<(Material shared, Material wet, List<Renderer> renderers)> wetMaterials = new List<(Material, Material, List<Renderer>)>();
        float appliedWet = -1f;

        /// <summary>
        /// 准备可以变湿的副本：地形图层复制一份换上去（不改原资产），岩石类材质也复制一份。
        /// 退出时 RestoreWetness 换回原来的。
        /// </summary>
        void PrepareWetness()
        {
            if (dryLayers != null) return;
            // 每座岛一块地形，用的是同一组地表图层：复制一份，所有用这组图层的地形都换上
            foreach (var terrain in Terrain.activeTerrains)
            {
                if (terrain == null || terrain.terrainData == null) continue;
                var layers = terrain.terrainData.terrainLayers;
                if (dryLayers == null)
                {
                    dryLayers = layers;
                    wetLayers = new TerrainLayer[dryLayers.Length];
                    for (int i = 0; i < dryLayers.Length; i++)
                    {
                        wetLayers[i] = dryLayers[i] != null ? Instantiate(dryLayers[i]) : null;
                        if (wetLayers[i] != null) wetLayers[i].hideFlags = HideFlags.DontSave;
                    }
                }
                else if (!System.Linq.Enumerable.SequenceEqual(layers, dryLayers)) continue;
                terrainDatas.Add(terrain.terrainData);
                terrain.terrainData.terrainLayers = wetLayers;
            }

            var keys = config.wetMaterialKeywords;
            var byMat = new Dictionary<Material, List<Renderer>>();
            foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var m = r.sharedMaterial;
                if (m == null || !m.HasProperty("_BaseColor") || !m.HasProperty("_Smoothness")) continue;
                bool match = false;
                foreach (var k in keys) if (m.name.IndexOf(k, System.StringComparison.OrdinalIgnoreCase) >= 0) { match = true; break; }
                if (!match) continue;
                if (!byMat.TryGetValue(m, out var list)) byMat[m] = list = new List<Renderer>();
                list.Add(r);
            }
            foreach (var kv in byMat)
            {
                var wet = new Material(kv.Key) { name = kv.Key.name + " (wet)", hideFlags = HideFlags.DontSave };
                foreach (var r in kv.Value) r.sharedMaterial = wet;
                wetMaterials.Add((kv.Key, wet, kv.Value));
            }
            appliedWet = -1f;
        }

        void ApplyWetness(float w)
        {
            if (dryLayers == null || Mathf.Abs(w - appliedWet) < 0.01f) return;
            appliedWet = w;
            for (int i = 0; i < dryLayers.Length; i++)
            {
                var dry = dryLayers[i];
                var wet = wetLayers[i];
                if (dry == null || wet == null) continue;
                // 湿地面：颜色变深、更光滑（带遮罩贴图的图层，光滑度来自遮罩的 a 通道）
                var d = dry.diffuseRemapMax;
                float dark = Mathf.Lerp(1f, 0.6f, w);
                wet.diffuseRemapMax = new Vector4(d.x * dark, d.y * dark, d.z * dark, d.w);
                wet.smoothness = Mathf.Lerp(dry.smoothness, 0.8f, w * 0.85f);
                var mn = dry.maskMapRemapMin; var mx = dry.maskMapRemapMax;
                wet.maskMapRemapMin = new Vector4(mn.x, mn.y, mn.z, Mathf.Lerp(mn.w, 0.5f, w));
                wet.maskMapRemapMax = new Vector4(mx.x, mx.y, mx.z, Mathf.Lerp(mx.w, 1f, w));
            }
            foreach (var (shared, wet, _) in wetMaterials)
            {
                wet.SetColor("_BaseColor", shared.GetColor("_BaseColor") * Mathf.Lerp(1f, 0.62f, w));
                wet.SetFloat("_Smoothness", Mathf.Lerp(shared.GetFloat("_Smoothness"), 0.85f, w));
            }
        }

        void RestoreWetness()
        {
            if (dryLayers != null) foreach (var td in terrainDatas) if (td != null) td.terrainLayers = dryLayers;
            if (wetLayers != null) foreach (var l in wetLayers) if (l != null) DestroyImmediate(l);
            foreach (var (shared, wet, renderers) in wetMaterials)
            {
                foreach (var r in renderers) if (r != null) r.sharedMaterial = shared;
                DestroyImmediate(wet);
            }
            wetMaterials.Clear();
            dryLayers = wetLayers = null;
            terrainDatas.Clear();
            appliedWet = -1f;
        }

        // ---------------- 默认配置 ----------------

        /// <summary>没有配置资产时用的默认调度（六种天气）。</summary>
        public static WeatherConfig CreateDefaultConfig()
        {
            var c = ScriptableObject.CreateInstance<WeatherConfig>();
            c.hideFlags = HideFlags.DontSave;
            foreach (var (id, weight, night, frag) in WeatherConfig.DefaultEntries)
                c.weathers.Add(new WeatherConfig.Entry { profile = WeatherProfile.CreateDefault(id), weight = weight, nightMultiplier = night, perFragment = frag });
            c.startWeather = c.weathers[0].profile;
            return c;
        }
    }
}
