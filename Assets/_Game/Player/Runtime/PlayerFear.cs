using UnityEngine;
using Rishi.Audio;
using Rishi.Core;
using Rishi.World;

namespace Rishi.Player
{
    /// <summary>
    /// 恐惧值：待在黑暗里慢慢上升，靠近光（白天、手电筒、点燃的篝火）就下降，篝火旁降得最快。
    /// 恐惧越高：画面四周越暗（HUD 读 GameSignals.Fear）、摄像机轻微发抖、心跳和急促的呼吸越响。
    /// 以后的影嗣会让恐惧涨得更快，也会被高恐惧吸引。挂在玩家身上。
    /// </summary>
    public class PlayerFear : MonoBehaviour
    {
        public DayNightCycle dayNight;
        [Tooltip("主摄像机：恐惧高时在它自己的局部旋转上叠加轻微抖动")]
        public Transform shakeTarget;

        [Header("变化速度")]
        [Tooltip("完全黑暗时从 0 涨到 1 需要的秒数")]
        public float riseSeconds = 90f;
        [Tooltip("在光里从 1 降到 0 需要的秒数")]
        public float fallSeconds = 25f;
        [Tooltip("篝火旁下降速度倍数")]
        public float safeZoneFallMultiplier = 4f;
        [Tooltip("亮度低于这个值才算在黑暗里")]
        [Range(0, 1)] public float darkThreshold = 0.35f;

        [Header("表现")]
        [Tooltip("恐惧满时摄像机抖动的角度（度）")]
        public float maxShake = 0.9f;

        public float Fear { get; private set; }
        /// <summary>当前估算的亮度 0~1。</summary>
        public float LightLevel { get; private set; } = 1f;

        AudioSource heartbeat, breath;
        float shadeTimer, shade = 1f, seed;

        void Start()
        {
            seed = Random.value * 100f;
            heartbeat = Loop("heartbeat");
            breath = Loop("breath_hurt");
        }

        AudioSource Loop(string id)
        {
            var clip = AudioManager.Instance != null ? AudioManager.Instance.RandomClip(id) : null;
            if (clip == null) return null;
            var src = gameObject.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.spatialBlend = 0f;
            src.volume = 0f;
            src.playOnAwake = false;
            src.Play();
            return src;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (GameSignals.CutsceneActive) { Apply(0f, dt); return; }

            // 白天在树荫下会暗一些：每隔一会儿朝太阳打一条射线
            shadeTimer -= dt;
            if (shadeTimer <= 0f)
            {
                shadeTimer = 0.3f;
                shade = 1f;
                var sun = RenderSettings.sun;
                if (sun != null && sun.enabled && shakeTarget != null &&
                    Physics.Raycast(shakeTarget.position, -sun.transform.forward, 80f, ~0, QueryTriggerInteraction.Ignore))
                    shade = 0.55f;
            }

            float day = dayNight != null ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.08f, 0.15f, dayNight.SunElevation)) * dayNight.weatherSun : 1f;
            float flashlight = GameSignals.FlashlightOn ? 0.75f * Mathf.Clamp01(GameSignals.FlashlightCharge * 4f) : 0f;
            float fire = 0f;
            foreach (var c in Campfire.All)
            {
                if (c == null || !c.lit) continue;
                float d = Vector3.Distance(c.transform.position, transform.position);
                fire = Mathf.Max(fire, Mathf.Clamp01(1.2f - d / (c.safeRadius * 1.6f)));
            }
            LightLevel = EstimateLight(day * shade, flashlight, fire, GameSignals.HeadUnderwater);
            Fear = Step(Fear, LightLevel, dt, darkThreshold, riseSeconds, fallSeconds * (GameSignals.InSafeZone ? 1f / safeZoneFallMultiplier : 1f));
            Apply(Fear, dt);
        }

        /// <summary>综合亮度：取天光、手电、篝火里最亮的一个；水下天光打折。</summary>
        public static float EstimateLight(float daylight, float flashlight, float fire, bool underwater)
        {
            float sky = underwater ? daylight * 0.6f : daylight;
            return Mathf.Clamp01(Mathf.Max(sky, Mathf.Max(flashlight, fire)));
        }

        /// <summary>恐惧的一步：亮度低于阈值时按黑暗程度上升，否则按亮度下降。</summary>
        public static float Step(float fear, float light, float dt, float darkThreshold, float riseSeconds, float fallSeconds)
        {
            if (light < darkThreshold)
            {
                float dark = 1f - light / Mathf.Max(darkThreshold, 1e-3f);
                return Mathf.Min(1f, fear + dt / Mathf.Max(riseSeconds, 0.1f) * Mathf.Lerp(0.3f, 1f, dark));
            }
            return Mathf.Max(0f, fear - dt / Mathf.Max(fallSeconds, 0.1f) * Mathf.Lerp(0.5f, 1f, light));
        }

        void Apply(float fear, float dt)
        {
            GameSignals.Fear = fear;
            float f = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 1f, fear)); // 低恐惧时几乎没有表现
            if (heartbeat != null)
            {
                heartbeat.volume = Mathf.MoveTowards(heartbeat.volume, f * 0.75f, dt * 0.5f);
                heartbeat.pitch = Mathf.Lerp(0.9f, 1.35f, f);
            }
            if (breath != null)
            {
                breath.volume = Mathf.MoveTowards(breath.volume, Mathf.InverseLerp(0.55f, 1f, fear) * 0.45f, dt * 0.4f);
                breath.pitch = Mathf.Lerp(1f, 1.25f, f);
            }
            if (shakeTarget != null)
            {
                float t = Time.time * 1.7f + seed;
                float a = maxShake * f * f;
                shakeTarget.localRotation = Quaternion.Euler(
                    (Mathf.PerlinNoise(t, 0.1f) - 0.5f) * 2f * a,
                    (Mathf.PerlinNoise(0.7f, t) - 0.5f) * 2f * a,
                    (Mathf.PerlinNoise(t * 0.6f, 3.3f) - 0.5f) * a);
            }
        }

        /// <summary>强行设定恐惧值（剧情事件、以后的怪物用）。</summary>
        public void Add(float amount) => Fear = Mathf.Clamp01(Fear + amount);
    }
}
