using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Rishi.Player;

namespace Rishi.Builders
{
    /// <summary>
    /// 生成篝火：石圈与焦木（可选）、火焰 / 火星 / 烟三层粒子、摇曳的火光、噼啪声和互动用的碰撞体。
    /// 默认熄灭，玩家按 E 点燃（见 Rishi.Player.Campfire）。
    /// </summary>
    public static class CampfireBuilder
    {
        const string MatDir = "Assets/Art/Generated/Materials";

        public static Campfire Build(Transform parent, Vector3 groundPos, float yaw, bool ring, string name = "Campfire")
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, true);
            root.transform.SetPositionAndRotation(groundPos, Quaternion.Euler(0, yaw, 0));

            if (ring)
            {
                var stone = FPSSceneBuilder.Painted("FireRingStone", new Color(0.34f, 0.33f, 0.31f), 0f, 0.2f);
                var charcoal = FPSSceneBuilder.Painted("FireCharcoal", new Color(0.07f, 0.06f, 0.05f), 0f, 0.15f);
                for (int i = 0; i < 9; i++)
                {
                    float a = i / 9f * Mathf.PI * 2;
                    Box(root.transform, "FireStone", new Vector3(Mathf.Cos(a) * 0.55f, 0.08f, Mathf.Sin(a) * 0.55f),
                        new Vector3(0.22f, 0.16f, 0.18f), stone, Quaternion.Euler(Random.Range(-10f, 10f), a * Mathf.Rad2Deg, 0));
                }
                Box(root.transform, "Log1", new Vector3(0, 0.06f, 0), new Vector3(0.12f, 0.1f, 0.7f), charcoal, Quaternion.Euler(0, 35f, 0));
                Box(root.transform, "Log2", new Vector3(0, 0.08f, 0), new Vector3(0.12f, 0.1f, 0.7f), charcoal, Quaternion.Euler(0, -40f, 0));
            }

            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.25f, 0);
            col.size = new Vector3(1.3f, 0.5f, 1.3f);

            var fx = new GameObject("Fire").transform;
            fx.SetParent(root.transform, false);
            fx.localPosition = new Vector3(0, 0.1f, 0);

            var light = new GameObject("FireLight").AddComponent<Light>();
            light.transform.SetParent(fx, false);
            light.transform.localPosition = new Vector3(0, 0.6f, 0);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.55f, 0.22f);
            light.range = 11f;
            light.intensity = 5f;
            light.shadows = LightShadows.None;
            light.enabled = false;

            var flames = Particles(fx, "Flames", Mat("FireFlame", additive: true), main =>
            {
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.28f, 0.55f);
                main.startColor = new Color(2.4f, 1.1f, 0.35f, 1f);
                main.maxParticles = 120;
            }, rate: 45f, radius: 0.25f, gravity: -0.15f, grow: AnimationCurve.EaseInOut(0, 1f, 1, 0.2f),
               fade: Gradient(new Color(1f, 0.9f, 0.6f), new Color(1f, 0.35f, 0.08f), 0.9f));
            var embers = Particles(fx, "Embers", Mat("FireFlame", additive: true), main =>
            {
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.05f);
                main.startColor = new Color(3f, 1.2f, 0.3f, 1f);
                main.maxParticles = 60;
            }, rate: 7f, radius: 0.2f, gravity: -0.12f, grow: AnimationCurve.Linear(0, 1f, 1, 0.4f),
               fade: Gradient(new Color(1f, 0.7f, 0.3f), new Color(1f, 0.25f, 0.05f), 1f), noise: 0.6f);
            var smoke = Particles(fx, "Smoke", Mat("FireSmoke", additive: false), main =>
            {
                main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 6.5f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
                main.startColor = new Color(0.22f, 0.21f, 0.2f, 0.35f);
                main.maxParticles = 80;
            }, rate: 6f, radius: 0.2f, gravity: -0.03f, grow: AnimationCurve.Linear(0, 1f, 1, 4f),
               fade: Gradient(new Color(0.3f, 0.29f, 0.28f), new Color(0.5f, 0.5f, 0.5f), 0.5f), noise: 0.3f, startDelayY: 0.7f);

            var audio = root.AddComponent<AudioSource>();
            audio.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Final/fire_crackle.wav") ?? AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Placeholder/fire_crackle.wav");
            audio.loop = true;
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            audio.rolloffMode = AudioRolloffMode.Logarithmic;
            audio.minDistance = 1.5f;
            audio.maxDistance = 20f;
            audio.volume = 0.8f;
            audio.dopplerLevel = 0f;

            var fire = root.AddComponent<Campfire>();
            fire.fireLight = light;
            fire.effects = new[] { flames, embers, smoke };
            fire.crackle = audio;
            foreach (var ps in fire.effects) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return fire;
        }

        static void Box(Transform parent, string name, Vector3 localPos, Vector3 size, Material mat, Quaternion rot)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rot;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        static ParticleSystem Particles(Transform parent, string name, Material mat, System.Action<ParticleSystem.MainModule> init,
                                        float rate, float radius, float gravity, AnimationCurve grow, Gradient fade, float noise = 0f, float startDelayY = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0, startDelayY, 0);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity;
            init(main);
            var em = ps.emission;
            em.rateOverTime = rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = radius;
            shape.rotation = new Vector3(-90f, 0, 0); // 朝上喷
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, grow);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = fade;
            if (noise > 0f)
            {
                var n = ps.noise;
                n.enabled = true;
                n.strength = noise;
                n.frequency = 0.6f;
            }
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        static Gradient Gradient(Color a, Color b, float alpha)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(a, 0), new GradientColorKey(b, 1) },
                      new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(alpha, 0.12f), new GradientAlphaKey(0, 1) });
            return g;
        }

        /// <summary>粒子材质：火焰用加法混合（HDR 发光），烟用普通透明混合。</summary>
        static Material Mat(string name, bool additive)
        {
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Generated/Textures/soft_dot.png"));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", additive ? 2f : 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
