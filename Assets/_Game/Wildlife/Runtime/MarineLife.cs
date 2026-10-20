using System;
using System.Collections.Generic;
using UnityEngine;
using Rishi.Audio;
using Rishi.Core;

namespace Rishi.Wildlife
{
    /// <summary>
    /// 海洋生物：在玩家附近的海里生成各种鱼群。每群有一个在栖息深度里漫游的中心，鱼按队形跟随并各自抖动；
    /// 玩家靠近时整群受惊转向逃开。离玩家远了就回收。挂在场景里一个空物体上。
    /// </summary>
    public class MarineLife : MonoBehaviour
    {
        public enum Habitat { Reef, OpenWater, Seabed }

        [Serializable]
        public class Species
        {
            public string name;
            [Tooltip("商店模型（优先于 mesh/material）")]
            public GameObject prefab;
            [Tooltip("预制体缩放到目标体长用的系数")]
            public float modelScale = 1f;
            [Tooltip("没有骨骼动画的模型：整条鱼左右摆尾的角度和频率")]
            public float tailBeat = 10f;
            public float tailBeatSpeed = 7f;
            public Mesh mesh;
            public Material material;
            [Tooltip("模型需要额外旋转（鳐鱼的翅膀沿 Y 建模，转 90° 放平）")]
            public Vector3 modelEuler;
            public Habitat habitat;
            public Vector2Int schoolSize = new Vector2Int(8, 16);
            public Vector2 scale = new Vector2(0.8f, 1.2f);
            public float speed = 1.2f;
            public float fleeSpeed = 4f;
            public float fleeDistance = 5f;
            [Tooltip("队形半径（米）")]
            public float spread = 1.5f;
            [Tooltip("水面以下的深度范围（米）")]
            public Vector2 depth = new Vector2(1.5f, 12f);
            [Tooltip("离海底至少多高")]
            public float minAltitude = 0.6f;
            public int maxSchools = 3;
            [Range(0, 1)] public float nightActivity = 1f;
        }

        class Fish
        {
            public Transform t;
            public Transform model;
            public Vector3 offset;
            public Vector3 vel;
            public float phase;
        }

        class School
        {
            public Species species;
            public Vector3 center, heading, target;
            public float speed;
            public float fleeTimer;
            public float retarget;
            public readonly List<Fish> fish = new List<Fish>();
        }

        public List<Species> species = new List<Species>();
        public float spawnRadius = 45f;
        public float despawnRadius = 75f;
        [Tooltip("玩家所在位置离海多远以内才生成")]
        public float activeAboveSea = 6f;
        public float checkInterval = 0.75f;

        readonly List<School> schools = new List<School>();
        readonly Dictionary<Species, int> counts = new Dictionary<Species, int>();
        Terrain terrain;
        Transform player;
        float checkTimer;
        float nightFactor = 1f;

        void Start()
        {
            terrain = Islands.TerrainAt(transform.position);
        }

        float SeabedY(Vector3 p) => terrain != null ? terrain.SampleHeight(p) + terrain.transform.position.y : -40f;

        void Update()
        {
            if (player == null) player = GameSignals.Player;
            if (player == null) return;
            float dt = Time.deltaTime;

            checkTimer -= dt;
            if (checkTimer <= 0f)
            {
                checkTimer = checkInterval;
                terrain = Islands.TerrainAt(player.position); // 玩家附近那座岛的海床
                // 夜里珊瑚鱼躲进礁石，开阔水域的鱼照常
                var sun = RenderSettings.sun;
                nightFactor = sun != null && sun.transform.forward.y > 0.05f ? 0f : 1f;
                Recycle();
                if (player.position.y < GameSignals.SeaLevel + activeAboveSea) TrySpawn();
            }

            foreach (var s in schools) Simulate(s, dt);
        }

        void Recycle()
        {
            for (int i = schools.Count - 1; i >= 0; i--)
            {
                var s = schools[i];
                bool sleeping = nightFactor < 0.5f && s.species.nightActivity < 0.5f;
                if (Vector3.Distance(s.center, player.position) < despawnRadius && !sleeping) continue;
                foreach (var f in s.fish) if (f.t != null) Destroy(f.t.gameObject);
                counts[s.species]--;
                schools.RemoveAt(i);
            }
        }

        void TrySpawn()
        {
            foreach (var sp in species)
            {
                if (sp.prefab == null && (sp.mesh == null || sp.material == null)) continue;
                if (nightFactor < 0.5f && sp.nightActivity < 0.5f) continue;
                counts.TryGetValue(sp, out int n);
                if (n >= sp.maxSchools) continue;
                if (!FindSpot(sp, out var pos)) continue;
                schools.Add(CreateSchool(sp, pos));
                counts[sp] = n + 1;
                return; // 每次检查最多生成一群，分摊开销
            }
        }

        bool FindSpot(Species sp, out Vector3 pos)
        {
            for (int k = 0; k < 8; k++)
            {
                var r = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(spawnRadius * 0.45f, spawnRadius);
                pos = new Vector3(player.position.x + r.x, 0, player.position.z + r.y);
                if (ValidDepth(sp, ref pos)) return true;
            }
            pos = default;
            return false;
        }

        /// <summary>把位置调整到这个物种的栖息深度里；水太浅返回 false。</summary>
        bool ValidDepth(Species sp, ref Vector3 pos)
        {
            float bed = SeabedY(pos);
            float waterDepth = GameSignals.SeaLevel - bed;
            if (waterDepth < sp.depth.x + sp.minAltitude + 0.3f) return false;
            float top = GameSignals.SeaLevel - sp.depth.x;
            float bottom = Mathf.Max(GameSignals.SeaLevel - sp.depth.y, bed + sp.minAltitude);
            if (bottom > top) return false;
            pos.y = sp.habitat == Habitat.Seabed ? bottom + UnityEngine.Random.Range(0f, 0.6f)
                  : sp.habitat == Habitat.Reef ? Mathf.Lerp(bottom, top, UnityEngine.Random.Range(0f, 0.45f))
                  : UnityEngine.Random.Range(bottom, top);
            return true;
        }

        School CreateSchool(Species sp, Vector3 pos)
        {
            var s = new School { species = sp, center = pos, speed = sp.speed };
            s.heading = Quaternion.Euler(0, UnityEngine.Random.Range(0f, 360f), 0) * Vector3.forward;
            s.target = pos;
            int count = UnityEngine.Random.Range(sp.schoolSize.x, sp.schoolSize.y + 1);
            float scale = UnityEngine.Random.Range(sp.scale.x, sp.scale.y);
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject(sp.name);
                go.transform.SetParent(transform, false);
                GameObject model;
                if (sp.prefab != null)
                {
                    model = Instantiate(sp.prefab, go.transform, false);
                    model.name = "Model";
                    model.transform.localScale = sp.prefab.transform.localScale * sp.modelScale;
                    foreach (var c in model.GetComponentsInChildren<Collider>()) Destroy(c);
                    foreach (var r in model.GetComponentsInChildren<Renderer>())
                        r.shadowCastingMode = count > 6 ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
                }
                else
                {
                    model = new GameObject("Model");
                    model.transform.SetParent(go.transform, false);
                    model.AddComponent<MeshFilter>().sharedMesh = sp.mesh;
                    var mr = model.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = sp.material;
                    mr.shadowCastingMode = count > 6 ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
                }
                model.transform.localRotation = Quaternion.Euler(sp.modelEuler);
                go.transform.localScale = Vector3.one * scale * UnityEngine.Random.Range(0.85f, 1.15f);
                var offset = Vector3.Scale(UnityEngine.Random.insideUnitSphere, new Vector3(1f, 0.45f, 1.3f)) * sp.spread;
                go.transform.position = pos + offset;
                go.transform.rotation = Quaternion.LookRotation(s.heading);
                s.fish.Add(new Fish { t = go.transform, model = model.transform, offset = offset, vel = s.heading * sp.speed, phase = UnityEngine.Random.value * 10f });
            }
            return s;
        }

        void Simulate(School s, float dt)
        {
            var sp = s.species;
            Vector3 toPlayer = player.position - s.center;
            float pd = toPlayer.magnitude;

            // 受惊：玩家靠近（在水里更敏感）
            float scare = sp.fleeDistance * (GameSignals.HeadUnderwater ? 1f : 0.6f);
            if (pd < scare + sp.spread)
            {
                if (s.fleeTimer <= 0f && sp.schoolSize.y > 3) AudioManager.Play("fish_scatter", s.center, 0.5f);
                s.fleeTimer = 2.5f;
                var away = -toPlayer;
                away.y *= 0.3f;
                s.target = s.center + away.normalized * 14f + UnityEngine.Random.insideUnitSphere * 3f;
                ValidDepthClamp(sp, ref s.target);
            }

            s.retarget -= dt;
            if (s.fleeTimer <= 0f && (s.retarget <= 0f || Vector3.Distance(s.center, s.target) < 1.5f))
            {
                s.retarget = UnityEngine.Random.Range(6f, 14f);
                var p = s.center + Quaternion.Euler(0, UnityEngine.Random.Range(-80f, 80f), 0) * s.heading * UnityEngine.Random.Range(6f, 16f);
                if (!ValidDepth(sp, ref p)) p = s.center - s.heading * 6f; // 前面水太浅，掉头
                s.target = p;
            }
            s.fleeTimer -= dt;

            float wantSpeed = s.fleeTimer > 0f ? sp.fleeSpeed : sp.speed * (0.7f + 0.3f * Mathf.Sin(Time.time * 0.3f + s.fish.Count));
            s.speed = Mathf.MoveTowards(s.speed, wantSpeed, dt * (s.fleeTimer > 0f ? 8f : 1f));
            Vector3 desired = (s.target - s.center).normalized;
            s.heading = Vector3.RotateTowards(s.heading, desired, dt * (s.fleeTimer > 0f ? 3.5f : 0.8f), 0f).normalized;
            s.center += s.heading * s.speed * dt;
            // 别钻进海底、别冲出水面
            float bed = SeabedY(s.center);
            s.center.y = Mathf.Clamp(s.center.y, bed + sp.minAltitude, GameSignals.SeaLevel - 0.6f);

            var rot = Quaternion.LookRotation(new Vector3(s.heading.x, 0, s.heading.z).sqrMagnitude > 1e-4f ? new Vector3(s.heading.x, 0, s.heading.z) : Vector3.forward);
            float tighten = s.fleeTimer > 0f ? 0.6f : 1f;
            foreach (var f in s.fish)
            {
                if (f.t == null) continue;
                float t = Time.time + f.phase;
                Vector3 jitter = new Vector3(Mathf.Sin(t * 0.9f), Mathf.Sin(t * 1.3f) * 0.4f, Mathf.Cos(t * 0.7f)) * sp.spread * 0.18f;
                Vector3 goal = s.center + rot * (f.offset * tighten) + jitter;
                Vector3 steer = (goal - f.t.position) * 2.2f - f.vel * 0.9f;
                // 个体也会躲开玩家
                Vector3 fp = f.t.position - player.position;
                if (fp.sqrMagnitude < 2.5f * 2.5f) steer += fp.normalized * 14f;
                f.vel += steer * dt;
                float maxV = s.speed * 1.6f + 0.5f;
                if (f.vel.sqrMagnitude > maxV * maxV) f.vel = f.vel.normalized * maxV;
                var np = f.t.position + f.vel * dt;
                float fb = SeabedY(np) + sp.minAltitude * 0.5f;
                if (np.y < fb) { np.y = fb; f.vel.y = Mathf.Abs(f.vel.y) * 0.3f; }
                np.y = Mathf.Min(np.y, GameSignals.SeaLevel - 0.35f);
                f.t.position = np;
                Vector3 look = f.vel.sqrMagnitude > 0.01f ? f.vel : s.heading;
                look.y *= 0.5f; // 鱼很少竖着游
                f.t.rotation = Quaternion.Slerp(f.t.rotation, Quaternion.LookRotation(look), dt * 5f);
                if (sp.prefab != null && f.model != null)
                {
                    // 商店模型没有动画：游得越快摆尾越快
                    float beat = Mathf.Sin(t * sp.tailBeatSpeed * (0.6f + f.vel.magnitude / Mathf.Max(sp.speed, 0.1f) * 0.4f)) * sp.tailBeat;
                    f.model.localRotation = Quaternion.Euler(0, beat, 0) * Quaternion.Euler(sp.modelEuler);
                }
            }
        }

        void ValidDepthClamp(Species sp, ref Vector3 p)
        {
            float bed = SeabedY(p);
            p.y = Mathf.Clamp(p.y, Mathf.Max(bed + sp.minAltitude, GameSignals.SeaLevel - sp.depth.y), GameSignals.SeaLevel - sp.depth.x);
        }
    }
}
