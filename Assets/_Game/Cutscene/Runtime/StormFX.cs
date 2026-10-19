using System.Collections.Generic;
using UnityEngine;
using Rishi.Audio;
using Rishi.Core;
using Rishi.World;

namespace Rishi.Cutscene
{
    /// <summary>
    /// 海上风暴：启用期间切到深夜、加大海浪、浓雾、大雨、闪电（天空和场景瞬间照亮 + 延迟雷声），
    /// 并按时间表播放脚本化的闪电与音效（例如照亮远处的岛、撞礁）。停用时全部恢复。
    /// 由过场 Timeline 的激活轨道控制。
    /// </summary>
    [ExecuteAlways] // 在 Timeline 窗口里拖动时间轴预览时也能看到风暴
    public class StormFX : MonoBehaviour
    {
        public DayNightCycle dayNight;
        public Renderer ocean;
        public Light lightning;
        public Transform followCamera;
        public Material rainMaterial;

        [Tooltip("风暴的天气参数（Assets/Config/Weather_Storm.asset）；为空时用默认的风暴")]
        public WeatherProfile weather;

        [System.Serializable]
        public struct Cue
        {
            public float time;
            public string sound;
            public bool flash;
        }
        public List<Cue> cues = new List<Cue>();

        static readonly int SkyFlashId = Shader.PropertyToID("_IslandSkyFlash");
        static readonly int WaveScaleId = Shader.PropertyToID("_WaveScale");

        float savedTime, savedFog;
        bool savedRunning;
        bool applied; // 只有真正改过设置才恢复，避免把未保存的默认值（0 点、时间停止）写回去
        float elapsed, nextFlash, flash;
        int nextCue;
        static WeatherProfile defaultStorm;

        WeatherProfile Weather
        {
            get
            {
                if (weather != null) return weather;
                if (defaultStorm == null) { defaultStorm = WeatherProfile.CreateStorm(); defaultStorm.hideFlags = HideFlags.HideAndDontSave; }
                return defaultStorm;
            }
        }
        AudioSource loop;
        ParticleSystem rain;
        MaterialPropertyBlock oceanBlock;

        void OnEnable()
        {
            if (dayNight == null) return; // 构建场景时刚添加组件、还没接好引用
            applied = true;
            var w = Weather;
            elapsed = 0;
            nextCue = 0;
            nextFlash = Random.Range(w.flashInterval.x, w.flashInterval.y);
            if (dayNight != null)
            {
                savedTime = dayNight.timeOfDay;
                savedRunning = dayNight.running;
                dayNight.running = false;
                if (w.forceTimeOfDay >= 0f) dayNight.timeOfDay = w.forceTimeOfDay;
                dayNight.Apply(false);
            }
            if (dayNight != null) dayNight.fogOverride = w.fogDensity;
            savedFog = RenderSettings.fogDensity;
            RenderSettings.fogDensity = w.fogDensity;
            RenderSettings.fogColor = w.fogColor;
            if (ocean != null)
            {
                oceanBlock ??= new MaterialPropertyBlock();
                ocean.GetPropertyBlock(oceanBlock);
                oceanBlock.SetFloat(WaveScaleId, w.waveScale);
                ocean.SetPropertyBlock(oceanBlock);
            }
            if (Application.isPlaying)
            {
                loop = gameObject.AddComponent<AudioSource>();
                loop.clip = AudioManager.Instance != null ? AudioManager.Instance.RandomClip("amb_storm") : null;
                loop.loop = true;
                loop.spatialBlend = 0;
                loop.volume = 0.9f;
                if (loop.clip != null) loop.Play();
                if (AmbientController.Instance != null) AmbientController.Instance.silence = 1f; // 风暴盖过平时的环境声
                if (WindController.Instance != null) WindController.Instance.boost = w.windBoost;
                rain = CreateRain();
            }
        }

        void OnDisable()
        {
            if (!applied) return;
            applied = false;
            if (dayNight != null)
            {
                dayNight.timeOfDay = savedTime;
                dayNight.running = savedRunning;
                dayNight.Apply(false);
            }
            if (dayNight != null) dayNight.fogOverride = -1f;
            RenderSettings.fogDensity = savedFog;
            if (ocean != null) ocean.SetPropertyBlock(null);
            Shader.SetGlobalFloat(SkyFlashId, 0f);
            if (lightning != null) lightning.intensity = 0f;
            if (loop != null) Destroy(loop);
            if (rain != null) Destroy(rain.gameObject);
            if (AmbientController.Instance != null) AmbientController.Instance.silence = 0f;
            if (WindController.Instance != null) WindController.Instance.boost = 0f;
        }

        void Update()
        {
            if (!Application.isPlaying) return;
            elapsed += Time.deltaTime;

            while (nextCue < cues.Count && elapsed >= cues[nextCue].time)
            {
                var c = cues[nextCue++];
                if (c.flash) Flash(true);
                if (!string.IsNullOrEmpty(c.sound)) AudioManager.Play2D(c.sound);
            }

            nextFlash -= Time.deltaTime;
            if (Weather.lightning && nextFlash <= 0)
            {
                nextFlash = Random.Range(Weather.flashInterval.x, Weather.flashInterval.y);
                Flash(false);
            }

            // 闪电：两次快速闪烁后衰减
            flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 3.5f);
            float flicker = flash > 0.6f ? (Mathf.Sin(elapsed * 90f) > 0 ? 1f : 0.3f) : 1f;
            Shader.SetGlobalFloat(SkyFlashId, flash * flicker * 1.6f);
            if (lightning != null) lightning.intensity = flash * flicker * 3.5f;

            if (rain != null && followCamera != null) rain.transform.position = followCamera.position + Vector3.up * 6f;
        }

        /// <summary>闪电；big 表示剧情用的大闪电（更亮、雷声更近）。</summary>
        public void Flash(bool big)
        {
            flash = big ? 1.4f : Random.Range(0.6f, 1f);
            if (lightning != null) lightning.transform.rotation = Quaternion.Euler(Random.Range(35f, 70f), Random.Range(0f, 360f), 0);
            StartCoroutine(Thunder(big ? 0.25f : Random.Range(0.8f, 2.5f), big ? 1f : 0.7f));
        }

        System.Collections.IEnumerator Thunder(float delay, float volume)
        {
            yield return new WaitForSeconds(delay);
            AudioManager.Play2D("thunder", volume);
        }

        ParticleSystem CreateRain()
        {
            var go = new GameObject("Rain");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = 0.9f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.02f);
            main.startColor = new Color(0.7f, 0.75f, 0.8f, 0.35f);
            main.maxParticles = 6000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-4f, -2f);
            vel.y = new ParticleSystem.MinMaxCurve(-16f, -13f);
            vel.z = new ParticleSystem.MinMaxCurve(2f, 4f);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(24f, 1f, 24f);
            var em = ps.emission;
            em.rateOverTime = 5000f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.04f;
            r.lengthScale = 1f;
            r.sharedMaterial = rainMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }
    }
}
