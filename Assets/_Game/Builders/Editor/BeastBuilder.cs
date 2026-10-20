using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Rishi.Creatures;
using Rishi.World;

namespace Rishi.Builders
{
    /// <summary>
    /// 野猪和椰子蟹（程序化占位外观，正式模型以后再换）：白天 / 蚀化两套参数、材质、四肢摆动，
    /// 野猪会冲撞。野猪放在林间草地，椰子蟹放在离出生点有一段距离的海滩椰子树下。
    /// </summary>
    public static class BeastBuilder
    {
        const string MatDir = "Assets/Art/Generated/Materials";

        // ---------------- 配置 ----------------

        static CreatureDef BoarDef(bool night) => ConfigAssets.LoadOrCreate(night ? "Creature_Boar_Eclipse" : "Creature_Boar", () =>
        {
            var d = ScriptableObject.CreateInstance<CreatureDef>();
            d.displayName = night ? "蚀化野猪" : "野猪";
            d.maxHealth = night ? 110f : 80f;
            d.walkSpeed = 1.5f;
            d.runSpeed = night ? 6.5f : 5.5f;
            d.wanderRadius = 20f;
            d.sightRange = night ? 30f : 22f;
            d.sightAngle = 140f;
            d.aggroRange = night ? 999f : 9f; // 白天只在你靠得太近或打了它时才冲你，夜里看见就冲
            d.hearing = 1.2f;
            d.lightReaction = LightReaction.Ignore;
            d.attackDamage = night ? 18f : 12f;
            d.attackRange = 1.7f;
            d.attackCooldown = 1.4f;
            d.attackWindup = 0.25f;
            d.fearPerSecond = night ? 0.06f : 0.02f;
            d.idleSound = "boar_idle";
            d.alertSound = "boar_alert";
            d.attackSound = "boar_hit";
            d.hurtSound = "boar_hurt";
            d.deathSound = "boar_death";
            return d;
        });

        static CreatureDef CrabDef(bool night) => ConfigAssets.LoadOrCreate(night ? "Creature_CoconutCrab_Eclipse" : "Creature_CoconutCrab", () =>
        {
            var d = ScriptableObject.CreateInstance<CreatureDef>();
            d.displayName = night ? "蚀化椰子蟹" : "椰子蟹";
            d.maxHealth = night ? 70f : 50f;
            d.damageTaken = 0.6f; // 硬壳
            d.walkSpeed = 0.7f;
            d.runSpeed = night ? 3f : 2.2f;
            d.wanderRadius = 8f;
            d.sightRange = night ? 14f : 8f;
            d.sightAngle = 200f;
            d.aggroRange = night ? 10f : 4f;
            d.hearing = 0.8f;
            d.lightReaction = LightReaction.Ignore;
            d.attackDamage = night ? 30f : 15f; // 钳击
            d.attackRange = 1.3f;
            d.attackCooldown = 1.8f;
            d.attackWindup = 0.45f;
            d.fearPerSecond = night ? 0.05f : 0.01f;
            d.idleSound = "ccrab_idle";
            d.alertSound = "ccrab_alert";
            d.attackSound = "ccrab_attack";
            d.hurtSound = "ccrab_hurt";
            d.deathSound = "ccrab_death";
            return d;
        });

        // ---------------- 材质 ----------------

        static Material Lit(string name, Color c, float smooth, Color emission = default)
        {
            var m = FPSSceneBuilder.Painted(name, c, 0f, smooth);
            if (emission.maxColorComponent > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                EditorUtility.SetDirty(m);
            }
            return m;
        }

        static Material Ember => ConfigAssets.Glow("BeastEclipseGlow", new Color(4f, 0.35f, 0.08f));
        static Material Eclipsed => Lit("BeastEclipseSkin", new Color(0.035f, 0.03f, 0.035f), 0.35f);

        // ---------------- 放到岛上 ----------------

        public static void Place(Terrain terrain, Vector3 spawn, DayNightCycle cycle)
        {
            var root = new GameObject("Beasts").transform;
            var rng = new System.Random(4321);
            float R() => (float)rng.NextDouble();
            float H(Vector3 p) => terrain.SampleHeight(p) + terrain.transform.position.y;
            int boars = 0, crabs = 0;
            for (int i = 0; i < 4000 && (boars < 3 || crabs < 4); i++)
            {
                var p = new Vector3((R() - 0.5f) * 520f, 0, (R() - 0.5f) * 520f);
                p.y = H(p);
                float fromSpawn = Vector3.Distance(new Vector3(p.x, 0, p.z), new Vector3(spawn.x, 0, spawn.z));
                if (fromSpawn < 40f || IslandStoryBuilder.InClearZone(p.x, p.z, 4f)) continue;
                if (!NavMesh.SamplePosition(p, out var hit, 2f, NavMesh.AllAreas)) continue;
                string surface = Rishi.Core.SurfaceTag.TerrainSurface(terrain, hit.position);
                if (boars < 3 && hit.position.y > 4f && hit.position.y < 16f && (surface == "grass" || surface == "leaves"))
                {
                    Boar(root, hit.position, R() * 360f, cycle);
                    boars++;
                }
                else if (crabs < 4 && hit.position.y > 0.6f && hit.position.y < 3.2f && surface.Contains("sand") && fromSpawn > 60f)
                {
                    Crab(root, hit.position, R() * 360f, cycle);
                    crabs++;
                }
            }
            Debug.Log($"[Beasts] 野猪 {boars} 只，椰子蟹 {crabs} 只");
        }

        // ---------------- 野猪 ----------------

        public static Beast Boar(Transform parent, Vector3 pos, float yaw, DayNightCycle cycle)
        {
            var go = Root(parent, "Boar", pos, yaw, 0.45f, 0.9f, new Vector3(0, 0.5f, 0), new Vector3(0.6f, 0.8f, 1.3f));
            var hide = Lit("BoarHide", new Color(0.34f, 0.24f, 0.16f), 0.15f);
            var dark = Lit("BoarDark", new Color(0.08f, 0.06f, 0.05f), 0.2f);
            var ivory = Lit("BoarTusk", new Color(0.85f, 0.8f, 0.68f), 0.5f);
            var skin = new List<Renderer>();
            var body = new GameObject("Body").transform;
            body.SetParent(go.transform, false);
            skin.Add(Part(body, PrimitiveType.Capsule, new Vector3(0, 0.55f, -0.05f), new Vector3(0.52f, 0.6f, 0.5f), hide, Quaternion.Euler(90, 0, 0)));   // 躯干
            skin.Add(Part(body, PrimitiveType.Capsule, new Vector3(0, 0.68f, 0.15f), new Vector3(0.42f, 0.32f, 0.42f), hide, Quaternion.Euler(80, 0, 0)));  // 隆起的肩背
            skin.Add(Part(body, PrimitiveType.Sphere, new Vector3(0, 0.55f, 0.62f), new Vector3(0.34f, 0.32f, 0.4f), hide, Quaternion.identity));          // 头
            Part(body, PrimitiveType.Cylinder, new Vector3(0, 0.46f, 0.86f), new Vector3(0.14f, 0.08f, 0.14f), dark, Quaternion.Euler(90, 0, 0));         // 鼻子
            foreach (float x in new[] { -0.08f, 0.08f })
            {
                Part(body, PrimitiveType.Capsule, new Vector3(x, 0.48f, 0.8f), new Vector3(0.025f, 0.07f, 0.025f), ivory, Quaternion.Euler(-30, 0, x * -150f)); // 獠牙
                skin.Add(Part(body, PrimitiveType.Cube, new Vector3(x * 1.6f, 0.74f, 0.55f), new Vector3(0.06f, 0.12f, 0.04f), hide, Quaternion.Euler(-20, 0, x * 200f))); // 耳朵
            }
            Part(body, PrimitiveType.Cylinder, new Vector3(0, 0.62f, -0.62f), new Vector3(0.03f, 0.1f, 0.03f), dark, Quaternion.Euler(40, 0, 0));           // 尾巴
            var dayEyes = Eyes(body, new Vector3(0, 0.6f, 0.76f), 0.1f, 0.025f, dark, "Eyes");
            var nightEyes = Eyes(body, new Vector3(0, 0.6f, 0.78f), 0.12f, 0.045f, Ember, "EclipseEyes");
            var cracks = Cracks(body, new[] { (new Vector3(0, 0.83f, 0.05f), 0.5f, 0f), (new Vector3(0.18f, 0.7f, -0.2f), 0.35f, 30f), (new Vector3(-0.17f, 0.72f, 0.2f), 0.3f, -25f) });

            var legs = new List<Transform>();
            foreach (var (x, z) in new[] { (-0.17f, 0.35f), (0.17f, 0.35f), (-0.17f, -0.38f), (0.17f, -0.38f) })
                legs.Add(Leg(body, new Vector3(x, 0.45f, z), 0.45f, 0.055f, dark, skin));

            var beast = Finish(go, body, skin, legs, new[] { 0f, 0.5f, 0.5f, 0f }, BoarDef(false), BoarDef(true), cycle,
                               hide, new[] { nightEyes, cracks }, new[] { dayEyes }, 1.0f);
            var charge = go.AddComponent<BoarCharge>();
            charge.creature = go.GetComponent<Creature>();
            charge.beast = beast;
            return beast;
        }

        // ---------------- 椰子蟹 ----------------

        public static Beast Crab(Transform parent, Vector3 pos, float yaw, DayNightCycle cycle)
        {
            var go = Root(parent, "CoconutCrab", pos, yaw, 0.42f, 0.5f, new Vector3(0, 0.25f, 0), new Vector3(0.9f, 0.45f, 0.8f));
            var shell = Lit("CrabShell", new Color(0.12f, 0.16f, 0.34f), 0.55f);   // 深蓝的壳
            var legsMat = Lit("CrabLegs", new Color(0.55f, 0.25f, 0.12f), 0.45f); // 橙红的腿
            var dark = Lit("BoarDark", new Color(0.08f, 0.06f, 0.05f), 0.2f);
            var skin = new List<Renderer>();
            var body = new GameObject("Body").transform;
            body.SetParent(go.transform, false);
            skin.Add(Part(body, PrimitiveType.Sphere, new Vector3(0, 0.27f, 0.05f), new Vector3(0.55f, 0.22f, 0.45f), shell, Quaternion.identity)); // 头胸甲
            skin.Add(Part(body, PrimitiveType.Sphere, new Vector3(0, 0.24f, -0.3f), new Vector3(0.4f, 0.24f, 0.36f), shell, Quaternion.identity)); // 圆鼓的腹部
            // 两只钳子：左边那只大
            foreach (var (x, s) in new[] { (-0.24f, 1.3f), (0.24f, 1f) })
            {
                var arm = new GameObject(x < 0 ? "ClawL" : "ClawR").transform;
                arm.SetParent(body, false);
                arm.localPosition = new Vector3(x, 0.25f, 0.25f);
                arm.localRotation = Quaternion.Euler(0, x < 0 ? -25f : 25f, 0);
                skin.Add(Part(arm, PrimitiveType.Capsule, new Vector3(0, 0, 0.1f), new Vector3(0.07f, 0.12f, 0.07f) * s, legsMat, Quaternion.Euler(90, 0, 0)));
                skin.Add(Part(arm, PrimitiveType.Cube, new Vector3(0, 0.02f, 0.26f * s), new Vector3(0.1f, 0.07f, 0.16f) * s, legsMat, Quaternion.Euler(-8, 0, 0)));
                skin.Add(Part(arm, PrimitiveType.Cube, new Vector3(0, -0.04f, 0.25f * s), new Vector3(0.06f, 0.04f, 0.14f) * s, legsMat, Quaternion.Euler(10, 0, 0)));
            }
            var dayEyes = Eyes(body, new Vector3(0, 0.42f, 0.22f), 0.09f, 0.02f, dark, "Eyes");
            var nightEyes = Eyes(body, new Vector3(0, 0.42f, 0.23f), 0.1f, 0.04f, Ember, "EclipseEyes");
            var cracks = Cracks(body, new[] { (new Vector3(0, 0.39f, 0.05f), 0.4f, 10f), (new Vector3(0.1f, 0.37f, -0.28f), 0.3f, -40f), (new Vector3(-0.12f, 0.37f, -0.25f), 0.25f, 50f) });

            var legs = new List<Transform>();
            for (int i = 0; i < 3; i++)
                foreach (float side in new[] { -1f, 1f })
                {
                    var hip = Leg(body, new Vector3(side * 0.24f, 0.24f, 0.05f - i * 0.14f), 0.32f, 0.03f, legsMat, skin);
                    hip.localRotation = Quaternion.Euler(0, 0, side * 55f); // 腿往两边张开
                    legs.Add(hip);
                }
            return Finish(go, body, skin, legs, new[] { 0f, 0.5f, 0.33f, 0.83f, 0.66f, 0.16f }, CrabDef(false), CrabDef(true), cycle,
                          shell, new[] { nightEyes, cracks }, new[] { dayEyes }, 0.5f);
        }

        // ---------------- 公用 ----------------

        static GameObject Root(Transform parent, string name, Vector3 pos, float yaw, float radius, float height, Vector3 colCenter, Vector3 colSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            var agent = go.AddComponent<NavMeshAgent>();
            agent.radius = radius;
            agent.height = height;
            agent.angularSpeed = 260f;
            agent.acceleration = 10f;
            agent.stoppingDistance = 0.9f;
            var col = go.AddComponent<BoxCollider>();
            col.center = colCenter;
            col.size = colSize;
            return go;
        }

        static Beast Finish(GameObject go, Transform body, List<Renderer> skin, List<Transform> legs, float[] phases,
                            CreatureDef day, CreatureDef night, DayNightCycle cycle, Material dayMat, GameObject[] eclipseOnly, GameObject[] dayOnly, float stride)
        {
            var eyes = new GameObject("EyePoint").transform;
            eyes.SetParent(body, false);
            eyes.localPosition = new Vector3(0, 0.6f, 0.6f);
            var c = go.AddComponent<Creature>();
            c.def = day;
            c.dayNight = cycle;
            c.eyes = eyes;
            var b = go.AddComponent<Beast>();
            b.creature = c;
            b.dayNight = cycle;
            b.dayDef = day;
            b.nightDef = night;
            b.body = body;
            b.legs = legs.ToArray();
            b.legPhase = phases;
            b.strideLength = stride;
            b.skin = skin.ToArray();
            b.nightMaterial = Eclipsed;
            b.eclipseOnly = eclipseOnly;
            b.dayOnly = dayOnly;
            foreach (var g in eclipseOnly) g.SetActive(false);
            return b;
        }

        static Transform Leg(Transform parent, Vector3 hip, float length, float radius, Material mat, List<Renderer> skin)
        {
            var pivot = new GameObject("Leg").transform; // 髋部做支点，腿绕它前后摆
            pivot.SetParent(parent, false);
            pivot.localPosition = hip;
            skin.Add(Part(pivot, PrimitiveType.Capsule, new Vector3(0, -length * 0.5f, 0), new Vector3(radius * 2f, length * 0.5f, radius * 2f), mat, Quaternion.identity));
            return pivot;
        }

        static GameObject Eyes(Transform parent, Vector3 center, float spacing, float size, Material mat, string name)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = center;
            foreach (float x in new[] { -spacing * 0.5f, spacing * 0.5f })
                Part(g.transform, PrimitiveType.Sphere, new Vector3(x, 0, 0), Vector3.one * size, mat, Quaternion.identity);
            return g;
        }

        /// <summary>蚀化的裂纹：几道贴在背上的发光细条。</summary>
        static GameObject Cracks(Transform parent, (Vector3 pos, float length, float yaw)[] lines)
        {
            var g = new GameObject("EclipseCracks");
            g.transform.SetParent(parent, false);
            foreach (var (pos, length, yaw) in lines)
                Part(g.transform, PrimitiveType.Cube, pos, new Vector3(0.035f, 0.02f, length), Ember, Quaternion.Euler(0, yaw, 0));
            return g;
        }

        static Renderer Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat, Quaternion rot)
        {
            var g = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = rot;
            g.transform.localScale = scale;
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            return r;
        }
    }
}
