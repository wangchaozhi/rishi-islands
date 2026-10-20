using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Rishi.Creatures;
using Rishi.World;

namespace Rishi.Builders
{
    /// <summary>影嗣的配置、材质和夜间生成器。</summary>
    public static class ShadeBuilder
    {
        const string MatDir = "Assets/Art/Generated/Materials";

        /// <summary>影嗣的参数：夜行、怕光（僵直后退）、极速逼近、撕咬；声音组用 shade_*。</summary>
        public static CreatureDef Def => ConfigAssets.LoadOrCreate("Creature_Shade", () =>
        {
            var d = ScriptableObject.CreateInstance<CreatureDef>();
            d.displayName = "影嗣";
            d.maxHealth = 60f;
            d.walkSpeed = 1.3f;
            d.runSpeed = 8f;
            d.wanderRadius = 25f;
            d.sightRange = 38f;
            d.sightAngle = 160f;
            d.flashlightSightBonus = 1.5f;
            d.memorySeconds = 10f;
            d.hearing = 1.4f;
            d.lightReaction = LightReaction.Flinch;
            d.stunSeconds = 1.2f;
            d.retreatSeconds = 3.5f;
            d.attackDamage = 22f;
            d.attackRange = 1.6f;
            d.attackCooldown = 1.3f;
            d.attackWindup = 0.3f;
            d.activeAtDay = false;
            d.activeAtNight = true;
            d.fearPerSecond = 0.09f;
            d.idleSound = "";
            d.alertSound = "shade_alert";
            d.attackSound = "shade_bite";
            d.hurtSound = "shade_hurt";
            d.deathSound = "shade_death";
            return d;
        });

        public static ShadeSpawner CreateSpawner(DayNightCycle dayNight)
        {
            var spawner = new GameObject("ShadeSpawner").AddComponent<ShadeSpawner>();
            spawner.shadeDef = Def;
            spawner.dayNight = dayNight;
            spawner.bodyMaterial = BodyMaterial();
            spawner.smokeMaterial = ParticleMat("ShadeSmoke");
            spawner.eyeMaterial = EyeMaterial();
            return spawner;
        }

        /// <summary>身体：不受光照的半透明近黑色，像一团凝固的烟。</summary>
        static Material BodyMaterial() => Load("ShadeBody", () =>
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", new Color(0.004f, 0.004f, 0.006f, 0.88f));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        });

        /// <summary>眼睛：暗暗的橙色微光（不是亮红），只在近处看得清。</summary>
        static Material EyeMaterial() => Load("ShadeEyes", () =>
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", Color.black);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", new Color(1.4f, 0.55f, 0.18f));
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            return m;
        });

        static Material ParticleMat(string name) => Load(name, () =>
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Generated/Textures/soft_dot.png"));
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        });

        static Material Load(string name, System.Func<Material> create)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = create();
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
    }
}
