using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Rishi.Core;

namespace Rishi.World
{
    /// <summary>
    /// 水下画面：摄像机入水后切到蓝绿色深度雾（越深越暗）、水下后处理、太阳光透过水面的焦散光斑（动画光照 cookie）、
    /// 悬浮颗粒、浓重的水下光柱。挂在主摄像机上。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class UnderwaterFX : MonoBehaviour
    {
        public DayNightCycle dayNight;
        [Tooltip("水下后处理（色调、暗角、色差），权重随入水程度变化")]
        public Volume volume;
        [Tooltip("悬浮颗粒，水下时打开")]
        public ParticleSystem particles;

        [Header("焦散")]
        public Texture2D[] causticFrames;
        public float causticFps = 14f;
        public Vector2 causticSize = new Vector2(9f, 9f);

        [Header("水色")]
        public Color shallowWater = new Color(0.12f, 0.5f, 0.58f);
        public Color deepWater = new Color(0.02f, 0.12f, 0.2f);

        [Header("性能")]
        [Tooltip("海底装饰（珊瑚、海草……）所在的层，超过距离不渲染")]
        public int marineLayer = 9;
        public float marineCullDistance = 110f;
        [Tooltip("多深时颜色变成 deepWater")]
        public float deepAt = 28f;

        static readonly int UnderwaterId = Shader.PropertyToID("_IslandUnderwater");

        Camera cam;
        UniversalAdditionalCameraData camData;
        float blend;
        float frameTimer;
        int frame;
        Light cookieLight;
        bool cookieOn;

        void Awake()
        {
            cam = GetComponent<Camera>();
            camData = cam.GetUniversalAdditionalCameraData();
            if (particles != null) particles.Stop();
            if (marineLayer >= 0 && marineLayer < 32)
            {
                var d = new float[32];
                d[marineLayer] = marineCullDistance;
                cam.layerCullDistances = d;
            }
        }

        void OnDisable()
        {
            Shader.SetGlobalFloat(UnderwaterId, 0f);
            SetCookie(null);
            if (dayNight != null) dayNight.underwater = 0f;
        }

        void LateUpdate() => Apply(false, Time.deltaTime);

        /// <summary>立即按摄像机当前位置套用水下效果（离线截图用，编辑器里没有 LateUpdate）。</summary>
        public void ApplyNow()
        {
            if (cam == null) Awake();
            Apply(true, 0f);
        }

        void Apply(bool instant, float dt)
        {
            bool under = cam.transform.position.y < GameSignals.SeaLevel - 0.02f;
            // 入水几乎瞬间切换（水面那一下就是分界线），出水稍慢一点模拟眼睛里的水
            blend = instant ? (under ? 1f : 0f) : Mathf.MoveTowards(blend, under ? 1f : 0f, dt * (under ? 12f : 4f));
            float depth = Mathf.Max(0f, GameSignals.SeaLevel - cam.transform.position.y);
            float day = dayNight != null ? Mathf.Clamp01(Mathf.InverseLerp(-0.1f, 0.35f, dayNight.SunElevation)) : 1f;

            Color water = Color.Lerp(shallowWater, deepWater, Mathf.Clamp01(depth / deepAt)) * Mathf.Lerp(0.06f, 1f, day);
            if (dayNight != null)
            {
                dayNight.underwater = blend;
                dayNight.underwaterColor = water;
                dayNight.underwaterDensity = Mathf.Lerp(0.03f, 0.055f, Mathf.Clamp01(depth / deepAt));
            }
            Shader.SetGlobalFloat(UnderwaterId, blend);
            if (volume != null) volume.weight = blend;

            // 水下看不到天空：背景换成水色，否则视线尽头会露出明亮的天空盒
            if (blend > 0.5f)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = water;
            }
            else cam.clearFlags = CameraClearFlags.Skybox;

            if (particles != null)
            {
                if (blend > 0.5f && !particles.isPlaying) particles.Play();
                else if (blend <= 0.5f && particles.isPlaying) particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            // 焦散：只在水下时给太阳加动画 cookie
            var sun = RenderSettings.sun;
            if (blend > 0.01f && causticFrames != null && causticFrames.Length > 0 && sun != null && day > 0.05f)
            {
                frameTimer += dt * causticFps;
                if (frameTimer >= 1f)
                {
                    frameTimer -= Mathf.Floor(frameTimer);
                    frame = (frame + 1) % causticFrames.Length;
                }
                if (cookieLight != sun) SetCookie(null);
                cookieLight = sun;
                SetCookie(causticFrames[frame]);
            }
            else SetCookie(null);
        }

        void SetCookie(Texture2D tex)
        {
            if (cookieLight == null) return;
            if (tex == null && !cookieOn) return;
            cookieLight.cookie = tex;
            if (tex != null)
            {
                var data = cookieLight.GetUniversalAdditionalLightData();
                data.lightCookieSize = causticSize;
            }
            cookieOn = tex != null;
            if (!cookieOn) cookieLight = null;
        }
    }
}
