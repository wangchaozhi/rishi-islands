using System.Collections.Generic;
using UnityEngine;
using Rishi.Audio;
using Rishi.Core;
using Rishi.World;

namespace Rishi.Wildlife
{
    /// <summary>
    /// 小动物生态：在玩家周围动态生成 / 回收螃蟹、蝴蝶、鱼群、蝙蝠，以及跟随玩家的萤火虫粒子。
    /// 所有网格、贴图、材质都在运行时程序化生成，不依赖外部资源。挂在场景里一个物体上即可。
    /// 数量、范围、受惊距离等参数在 WildlifeConfig（Assets/Config/Wildlife.asset）。
    /// </summary>
    public class WildlifeDirector : MonoBehaviour
    {
        public Transform player;
        public DayNightCycle dayNight;
        public Material fireflyMaterial;
        [Tooltip("小动物参数（Assets/Config/Wildlife.asset）；为空时用默认值")]
        public WildlifeConfig config;

        Terrain terrain;
        readonly List<Critter> crabs = new List<Critter>();
        readonly List<Critter> butterflies = new List<Critter>();
        readonly List<Critter> fish = new List<Critter>();
        readonly List<Critter> bats = new List<Critter>();
        ParticleSystem fireflies;
        float tick;

        public static Material LitMat(Color c, float smooth = 0.3f)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            m.enableInstancing = true;
            return m;
        }

        public static Material CutoutMat(Texture2D tex, Color tint)
        {
            var m = LitMat(tint, 0.2f);
            m.SetTexture("_BaseMap", tex);
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", 0.5f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Cull", 0f);
            m.renderQueue = 2450;
            return m;
        }

        void Start()
        {
            if (config == null) { config = ScriptableObject.CreateInstance<WildlifeConfig>(); config.hideFlags = HideFlags.HideAndDontSave; }
            terrain = Terrain.activeTerrain;
            if (player == null) player = GameSignals.Player;
            fireflies = CreateFireflies();
        }

        void Update()
        {
            if (player == null || terrain == null) return;
            float sun = dayNight != null ? dayNight.SunElevation : 0.5f;
            bool day = sun > config.dayElevation;
            bool dusk = sun < config.duskElevation;

            // 萤火虫：夜晚、在植被里
            var em = fireflies.emission;
            string here = SurfaceTag.TerrainSurface(terrain, player.position);
            em.rateOverTime = sun < config.fireflyElevation && (here == "grass" || here == "leaves") ? config.fireflyRate : 0f;
            fireflies.transform.position = player.position;

            tick -= Time.deltaTime;
            if (tick > 0) return;
            tick = 0.5f;

            Maintain(crabs, config.maxCrabs, true, SpawnCrab);
            Maintain(butterflies, day ? config.maxButterflies : 0, day, SpawnButterfly);
            Maintain(fish, config.maxFishSchools, true, SpawnFish);
            Maintain(bats, dusk ? config.maxBats : 0, dusk, SpawnBat);
        }

        void Maintain(List<Critter> list, int max, bool allowed, System.Func<Critter> spawn)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var c = list[i];
                bool far = c == null || (c.transform.position - player.position).sqrMagnitude > config.despawnRadius * config.despawnRadius;
                if (c == null || far || c.Done || !allowed && !c.Visible(player.position))
                {
                    if (c != null) Destroy(c.gameObject);
                    list.RemoveAt(i);
                }
            }
            // 每次最多补一只，避免一下子冒出很多
            if (list.Count < max)
            {
                var c = spawn();
                if (c != null) list.Add(c);
            }
        }

        // ---------------- 生成位置 ----------------

        float Height(Vector3 p) => terrain.SampleHeight(p) + terrain.transform.position.y;

        bool FindSpot(float minR, float maxR, System.Func<Vector3, float, bool> ok, out Vector3 pos)
        {
            for (int i = 0; i < 12; i++)
            {
                Vector2 d = Random.insideUnitCircle.normalized * Random.Range(minR, maxR);
                var p = player.position + new Vector3(d.x, 0, d.y);
                float h = Height(p);
                p.y = h;
                if (ok(p, h)) { pos = p; return true; }
            }
            pos = default;
            return false;
        }

        Critter SpawnCrab()
        {
            if (!FindSpot(6f, config.spawnRadius, (p, h) => h > 0.15f && h < 2.6f && SurfaceTag.TerrainSurface(terrain, p).Contains("sand"), out var pos)) return null;
            var go = Critters.Crab();
            go.transform.position = pos;
            var c = go.AddComponent<Crab>();
            c.config = config;
            c.Init(terrain, player);
            return c;
        }

        Critter SpawnButterfly()
        {
            if (!FindSpot(4f, config.spawnRadius * 0.7f, (p, h) => h > 2.5f && SurfaceTag.TerrainSurface(terrain, p) is string s && (s == "grass" || s == "leaves"), out var pos)) return null;
            var go = Critters.Butterfly(Random.Range(0, 3));
            go.transform.position = pos + Vector3.up * 1.2f;
            var c = go.AddComponent<Butterfly>();
            c.config = config;
            c.Init(terrain, player);
            return c;
        }

        Critter SpawnFish()
        {
            if (!FindSpot(8f, config.spawnRadius + 8f, (p, h) => h < -0.7f && h > -5f, out var pos)) return null;
            var go = new GameObject("FishSchool");
            go.transform.position = new Vector3(pos.x, 0, pos.z);
            var c = go.AddComponent<FishSchool>();
            c.config = config;
            c.Init(terrain, player);
            return c;
        }

        Critter SpawnBat()
        {
            var go = Critters.Bat();
            var c = go.AddComponent<Bat>();
            c.config = config;
            c.Init(terrain, player);
            return c;
        }

        ParticleSystem CreateFireflies()
        {
            var go = new GameObject("Fireflies");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 8f);
            main.startSpeed = 0.15f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
            main.startColor = new Color(0.75f, 1f, 0.35f) * 3f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 150;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(30f, 2.5f, 30f);
            shape.position = new Vector3(0, 0.6f, 0);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 0.4f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.15f), new GradientAlphaKey(0.2f, 0.4f), new GradientAlphaKey(1, 0.6f), new GradientAlphaKey(0.1f, 0.85f), new GradientAlphaKey(0, 1) });
            col.color = g; // 一闪一闪
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = fireflyMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var em = ps.emission;
            em.rateOverTime = 0;
            ps.Play();
            return ps;
        }
    }

    /// <summary>小动物基类。</summary>
    public abstract class Critter : MonoBehaviour
    {
        /// <summary>由 WildlifeDirector 在 Init 之前设置。</summary>
        public WildlifeConfig config;
        protected Terrain terrain;
        protected Transform player;
        public bool Done { get; protected set; }

        public virtual void Init(Terrain t, Transform p)
        {
            terrain = t;
            player = p;
        }

        protected float Ground(Vector3 p) => terrain.SampleHeight(p) + terrain.transform.position.y;

        /// <summary>是否在玩家视野内（不在视野里才允许按昼夜条件回收，避免凭空消失）。</summary>
        public bool Visible(Vector3 from)
        {
            var cam = Camera.main;
            if (cam == null) return false;
            var v = cam.WorldToViewportPoint(transform.position);
            return v.z > 0 && v.x > -0.1f && v.x < 1.1f && v.y > -0.1f && v.y < 1.1f && v.z < 30f;
        }
    }
}
