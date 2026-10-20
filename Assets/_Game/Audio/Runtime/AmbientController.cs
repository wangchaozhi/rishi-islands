using System.Collections.Generic;
using UnityEngine;
using Rishi.Core;
using Rishi.World;

namespace Rishi.Audio
{
    /// <summary>
    /// 环境声分层混音：海浪（离海越近越响）、风（越高越响）、白天丛林（鸟叫，植被越多越响）、夜晚虫鸣、神殿低鸣。
    /// 各层按昼夜、所在区域平滑过渡。Silence 用于恐怖事件前「突然安静」。挂在玩家身上。
    /// </summary>
    public class AmbientController : MonoBehaviour
    {
        public static AmbientController Instance { get; private set; }

        public DayNightCycle dayNight;
        public float fadeSpeed = 0.35f;
        [Range(0, 1)] public float silence;
        [Tooltip("危险逼近时环境声安静下来（影嗣写入）")]
        [Range(0, 1)] public float threatSilence;
        [Header("天气（WeatherSystem 写入）")]
        [Tooltip("压低白天的鸟叫（下雨时）")]
        [Range(0, 1)] public float weatherBirdDamp;
        [Tooltip("风声加成")]
        public float weatherWind;

        class Layer
        {
            public string id;
            public AudioSource source;
            public float target;
        }

        readonly Dictionary<string, Layer> layers = new Dictionary<string, Layer>();
        readonly HashSet<string> zones = new HashSet<string>();
        Terrain terrain;
        float vegetation;
        float underBlend;

        void Start()
        {
            Instance = this;
            terrain = Terrain.activeTerrain;
            foreach (var id in new[] { "amb_ocean", "amb_wind", "amb_jungle_day", "amb_night", "amb_temple", "amb_underwater" })
            {
                var clip = AudioManager.Instance != null ? AudioManager.Instance.RandomClip(id) : null;
                if (clip == null) continue;
                var src = gameObject.AddComponent<AudioSource>();
                src.clip = clip;
                src.loop = true;
                src.spatialBlend = 0f;
                src.volume = 0f;
                src.playOnAwake = false;
                src.time = Random.Range(0f, clip.length); // 错开各层的起点
                src.Play();
                layers[id] = new Layer { id = id, source = src };
            }
        }

        public void EnterZone(string zone) => zones.Add(zone);
        public void ExitZone(string zone) => zones.Remove(zone);

        void Update()
        {
            float elev = dayNight != null ? dayNight.SunElevation : 0.5f;
            float day = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-0.08f, 0.2f, elev));
            float y = transform.position.y;

            if (terrain != null && Time.frameCount % 15 == 0)
            {
                string s = SurfaceTag.TerrainSurface(terrain, transform.position);
                float targetVeg = s == "grass" || s == "leaves" ? 1f : 0.15f;
                vegetation = Mathf.MoveTowards(vegetation, targetVeg, 0.25f);
            }

            bool inTemple = zones.Contains("temple");
            float indoor = inTemple ? 0.3f : 1f;

            Set("amb_ocean", Mathf.Lerp(1f, 0.12f, Mathf.InverseLerp(2f, 30f, y)) * indoor * 0.9f);
            Set("amb_wind", (0.25f + Mathf.InverseLerp(5f, 45f, y) * 0.5f + weatherWind) * indoor);
            Set("amb_jungle_day", day * Mathf.Lerp(0.25f, 1f, vegetation) * indoor * 0.8f * (1f - weatherBirdDamp));
            Set("amb_night", (1f - day) * indoor * 0.75f);
            Set("amb_temple", inTemple ? 0.9f : 0f);
            // 水下：岸上的声音几乎听不见，换成水下的低沉嗡鸣、远处的鲸歌和礁石上虾群的噼啪声
            underBlend = Mathf.MoveTowards(underBlend, GameSignals.HeadUnderwater ? 1f : 0f, Time.deltaTime * 3f);
            foreach (var l in layers.Values) if (l.id != "amb_underwater") l.target *= 1f - underBlend * 0.85f;
            Set("amb_underwater", underBlend * 0.9f);

            float mul = AudioManager.Instance != null ? AudioManager.Instance.CategoryVolume(SoundCategory.Ambience) : 1f;
            foreach (var l in layers.Values)
            {
                float t = l.target * (1f - Mathf.Max(silence, threatSilence)) * mul;
                l.source.volume = Mathf.MoveTowards(l.source.volume, t, fadeSpeed * Time.deltaTime);
            }
        }

        void Set(string id, float v)
        {
            if (layers.TryGetValue(id, out var l)) l.target = Mathf.Clamp01(v);
        }
    }
}
