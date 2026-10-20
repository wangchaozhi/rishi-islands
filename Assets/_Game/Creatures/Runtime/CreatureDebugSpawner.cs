using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using Rishi.Core;
using Rishi.World;

namespace Rishi.Creatures
{
    /// <summary>
    /// 调试：按 F10 在玩家前方 12 米生成一只测试生物（黑色人形 + 两点发光的眼睛），用来试生物框架的状态机和感知。
    /// 正式的怪物（影嗣、野猪……）有自己的外观和生成规则。
    /// </summary>
    public class CreatureDebugSpawner : MonoBehaviour
    {
        public CreatureDef testCreature;
        public DayNightCycle dayNight;

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !kb.f10Key.wasPressedThisFrame || GameSignals.Player == null || testCreature == null) return;
            var p = GameSignals.Player;
            var fwd = Vector3.ProjectOnPlane(Camera.main != null ? Camera.main.transform.forward : p.forward, Vector3.up).normalized;
            var pos = p.position + fwd * 12f;
            if (NavMesh.SamplePosition(pos, out var hit, 6f, NavMesh.AllAreas)) pos = hit.position;
            var c = Spawn(testCreature, pos, Quaternion.LookRotation(-fwd), dayNight);
            GameSignals.Toast($"生成了 {testCreature.displayName}（调试）");
            c.name = "TestCreature";
        }

        /// <summary>生成一只带占位外观的生物。</summary>
        public static Creature Spawn(CreatureDef def, Vector3 pos, Quaternion rot, DayNightCycle dayNight)
        {
            var go = new GameObject(def.displayName);
            go.transform.SetPositionAndRotation(pos, rot);
            go.SetActive(false); // 先配好再启用，避免 Awake 时缺参数
            var agent = go.AddComponent<NavMeshAgent>();
            agent.radius = 0.4f;
            agent.height = 1.9f;
            agent.angularSpeed = 300f;
            agent.acceleration = 12f;
            agent.stoppingDistance = 0.8f;
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = 0.35f;
            col.height = 1.9f;
            col.center = new Vector3(0, 0.95f, 0);

            var body = Part(go.transform, PrimitiveType.Capsule, new Vector3(0, 0.95f, 0), new Vector3(0.55f, 0.95f, 0.45f), Mat(new Color(0.02f, 0.02f, 0.025f), Color.black));
            Kill(body.GetComponent<Collider>());
            var eyes = new GameObject("Eyes").transform;
            eyes.SetParent(go.transform, false);
            eyes.localPosition = new Vector3(0, 1.62f, 0.2f);
            var glow = Mat(Color.black, new Color(3f, 0.6f, 0.25f));
            foreach (float x in new[] { -0.08f, 0.08f })
            {
                var e = Part(eyes, PrimitiveType.Sphere, new Vector3(x, 0, 0.02f), Vector3.one * 0.045f, glow);
                Kill(e.GetComponent<Collider>());
            }

            var c = go.AddComponent<Creature>();
            c.def = def;
            c.dayNight = dayNight;
            c.eyes = eyes;
            go.SetActive(true);
            return c;
        }

        static void Kill(Object o) { if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o); }

        static GameObject Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
        {
            var g = GameObject.CreatePrimitive(type);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = mat;
            return g;
        }

        static Material Mat(Color baseColor, Color emission)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", baseColor);
            m.SetFloat("_Smoothness", 0.1f);
            if (emission.maxColorComponent > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission);
            }
            return m;
        }
    }
}
