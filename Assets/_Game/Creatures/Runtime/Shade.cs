using UnityEngine;
using UnityEngine.AI;
using Rishi.Audio;
using Rishi.Core;
using Rishi.World;

namespace Rishi.Creatures
{
    /// <summary>
    /// 影嗣：无面人形，身体像一团凝固的黑烟，只有两点暗光的「眼睛」。在 Creature 的基础上加：
    /// 潜伏（被玩家盯着且离得远时几乎不动，一移开视线就极速逼近）、被光照到时撕裂般的尖啸、
    /// 身上若有若无的低语（3D，越近越响，追击时更响）、僵直时身体抖动、死亡时烟散开。
    /// </summary>
    [RequireComponent(typeof(Creature))]
    public class Shade : MonoBehaviour
    {
        public Creature creature;
        public AudioSource whisper;
        public ParticleSystem smoke;
        public Transform body;
        [Tooltip("被盯着时还能动的速度比例")]
        [Range(0, 1)] public float watchedSpeed = 0.12f;
        [Tooltip("离玩家多近时就算被盯着也照样扑过来")]
        public float pounceDistance = 10f;

        Vector3 bodyScale;
        float dissolve = -1f;

        void Awake()
        {
            if (creature == null) creature = GetComponent<Creature>();
            if (body != null) bodyScale = body.localScale;
        }

        void OnEnable()
        {
            creature.StateChanged += OnState;
            creature.Died += OnDied;
        }

        void OnDisable()
        {
            creature.StateChanged -= OnState;
            creature.Died -= OnDied;
        }

        void Start()
        {
            if (whisper != null && whisper.clip == null && AudioManager.Instance != null) whisper.clip = AudioManager.Instance.RandomClip("shade_whisper");
            if (whisper != null && whisper.clip != null) { whisper.time = Random.Range(0f, whisper.clip.length); whisper.Play(); }
        }

        void OnState(CreatureState from, CreatureState to)
        {
            if (to == CreatureState.Stunned) AudioManager.Play("shade_scream", transform.position + Vector3.up * 1.5f, 1f);
        }

        void OnDied(Creature c)
        {
            dissolve = 0f;
            if (whisper != null) whisper.Stop();
            if (smoke != null) { var em = smoke.emission; em.rateOverTimeMultiplier *= 4f; }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dissolve >= 0f)
            {
                // 死了：身体缩小散开成烟
                dissolve += dt / 1.5f;
                if (body != null) body.localScale = bodyScale * Mathf.Max(0f, 1f - dissolve);
                return;
            }

            var cam = Camera.main;
            var p = GameSignals.Player;
            float dist = p != null ? Vector3.Distance(transform.position, p.position) : 999f;
            bool watched = cam != null && dist > pounceDistance && Watched(cam.transform.position, cam.transform.forward, transform.position + Vector3.up, cam.fieldOfView * 0.5f)
                           && Perception.Clear(cam.transform.position, transform.position + Vector3.up, p, transform);
            var s = creature.State;
            creature.speedMultiplier = s == CreatureState.Chase && watched ? watchedSpeed : 1f;

            if (whisper != null)
            {
                bool hunting = s == CreatureState.Chase || s == CreatureState.Attack;
                whisper.volume = Mathf.MoveTowards(whisper.volume, hunting ? 1f : 0.55f, dt * 0.8f);
            }

            // 僵直时身体剧烈抖动
            if (body != null)
                body.localScale = s == CreatureState.Stunned
                    ? Vector3.Scale(bodyScale, new Vector3(1f + Random.Range(-0.12f, 0.12f), 1f + Random.Range(-0.06f, 0.06f), 1f + Random.Range(-0.12f, 0.12f)))
                    : bodyScale;
        }

        /// <summary>玩家是否正看着某处：在摄像机视野（略收窄）里。</summary>
        public static bool Watched(Vector3 camPos, Vector3 camForward, Vector3 point, float halfFov) =>
            Perception.InCone(camPos, camForward, point, 80f, halfFov * 0.85f);

        /// <summary>
        /// 生成一只影嗣：半透明黑色胶囊拼的人形（头、躯干、垂着的手臂、越往下越淡的腿）、往上冒的黑烟、两点暗橙色眼睛、低语声。
        /// </summary>
        public static Shade Spawn(CreatureDef def, Vector3 pos, Quaternion rot, DayNightCycle dayNight, Material bodyMat, Material smokeMat, Material eyeMat)
        {
            var go = new GameObject("Shade");
            go.transform.SetPositionAndRotation(pos, rot);
            go.SetActive(false);
            var agent = go.AddComponent<NavMeshAgent>();
            agent.radius = 0.35f;
            agent.height = 1.9f;
            agent.angularSpeed = 540f;
            agent.acceleration = 30f;
            agent.stoppingDistance = 0.9f;
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = 0.35f;
            col.height = 1.9f;
            col.center = new Vector3(0, 0.95f, 0);

            var body = new GameObject("Body").transform;
            body.SetParent(go.transform, false);
            Part(body, new Vector3(0, 1.62f, 0), new Vector3(0.24f, 0.15f, 0.26f), bodyMat);             // 头
            Part(body, new Vector3(0, 1.18f, 0), new Vector3(0.42f, 0.36f, 0.26f), bodyMat);              // 躯干
            Part(body, new Vector3(0, 0.62f, 0), new Vector3(0.34f, 0.32f, 0.22f), bodyMat);              // 腿（并在一起，往下渐淡）
            Part(body, new Vector3(-0.27f, 1.0f, 0.04f), new Vector3(0.1f, 0.4f, 0.1f), bodyMat, -6f);     // 垂着的长手臂
            Part(body, new Vector3(0.27f, 1.0f, 0.04f), new Vector3(0.1f, 0.4f, 0.1f), bodyMat, 6f);

            var eyes = new GameObject("Eyes").transform;
            eyes.SetParent(body, false);
            eyes.localPosition = new Vector3(0, 1.64f, 0.11f);
            foreach (float x in new[] { -0.055f, 0.055f })
                Part(eyes, new Vector3(x, 0, 0), new Vector3(0.022f, 0.016f, 0.012f), eyeMat, 0f, PrimitiveType.Sphere);

            var smoke = Smoke(go.transform, smokeMat);

            var whisper = go.AddComponent<AudioSource>();
            whisper.loop = true;
            whisper.playOnAwake = false;
            whisper.spatialBlend = 1f;
            whisper.rolloffMode = AudioRolloffMode.Logarithmic;
            whisper.minDistance = 2.5f;
            whisper.maxDistance = 40f;
            whisper.dopplerLevel = 0f;
            whisper.volume = 0.55f;

            var c = go.AddComponent<Creature>();
            c.def = def;
            c.dayNight = dayNight;
            c.eyes = eyes;
            var shade = go.AddComponent<Shade>();
            shade.creature = c;
            shade.whisper = whisper;
            shade.smoke = smoke;
            shade.body = body;
            go.SetActive(true);
            return shade;
        }

        static void Part(Transform parent, Vector3 pos, Vector3 scale, Material mat, float roll = 0f, PrimitiveType type = PrimitiveType.Capsule)
        {
            var g = GameObject.CreatePrimitive(type);
            var col = g.GetComponent<Collider>();
            if (Application.isPlaying) Destroy(col); else DestroyImmediate(col);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.Euler(0, 0, roll);
            g.transform.localScale = scale;
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static ParticleSystem Smoke(Transform parent, Material mat)
        {
            var go = new GameObject("Smoke");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0, 1f, 0);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
            main.startColor = new Color(0.01f, 0.01f, 0.012f, 0.55f);
            main.gravityModifier = -0.04f;
            main.simulationSpace = ParticleSystemSimulationSpace.World; // 移动时拖出烟尾
            main.maxParticles = 120;
            var em = ps.emission;
            em.rateOverTime = 26f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.45f, 1.7f, 0.3f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.7f, 1, 1.6f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.2f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.8f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }
    }
}
