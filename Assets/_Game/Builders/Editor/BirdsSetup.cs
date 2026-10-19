using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Rishi.Core;
using Rishi.World;
using Rishi.Audio;
using Rishi.Player;
using Rishi.Weapons;
using Rishi.UI;
using Rishi.Story;
using Rishi.Cutscene;
using Rishi.Wildlife;
using Rishi.Rendering;

namespace Rishi.Builders
{
    /// <summary>
    /// 接入 Living Birds（Asset Store 免费资源）：补齐它需要的标签、把材质转换到 URP、
    /// 在岛上放置鸟群控制器和落脚点（地面 / 栖息点）。没有导入该资源时自动跳过。
    /// </summary>
    public static class BirdsSetup
    {
        const string Root = "Assets/living birds";
        static readonly string[] Tags = { "lb_bird", "lb_groundTarget", "lb_perchTarget" };

        public static bool Available => AssetDatabase.IsValidFolder(Root);

        public static void EnsureTags()
        {
            var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var tags = tm.FindProperty("tags");
            foreach (var t in Tags)
            {
                bool found = false;
                for (int i = 0; i < tags.arraySize; i++) found |= tags.GetArrayElementAtIndex(i).stringValue == t;
                if (found) continue;
                tags.arraySize++;
                tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = t;
            }
            tm.ApplyModifiedProperties();
        }

        /// <summary>把内置管线材质转换成 URP Lit（贴图、颜色、法线、光滑度），羽毛粒子转成 URP 粒子着色器。</summary>
        public static void ConvertMaterials()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var particle = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            int converted = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { Root }))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (mat.shader != null && mat.shader.name.StartsWith("Universal Render Pipeline")) continue;
                Texture main = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                Texture bump = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
                Color color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
                float gloss = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0.25f;
                bool isParticle = mat.name.ToLower().Contains("particle") || (mat.shader != null && mat.shader.name.Contains("Particle"));

                mat.shader = isParticle ? particle : lit;
                mat.SetTexture("_BaseMap", main);
                mat.SetColor("_BaseColor", color);
                if (isParticle)
                {
                    mat.SetFloat("_Surface", 1f);
                    mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    mat.SetFloat("_ZWrite", 0f);
                    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    mat.renderQueue = (int)RenderQueue.Transparent;
                }
                else
                {
                    if (bump != null) { mat.SetTexture("_BumpMap", bump); mat.EnableKeyword("_NORMALMAP"); }
                    mat.SetFloat("_Smoothness", Mathf.Min(gloss, 0.4f));
                    mat.SetFloat("_Metallic", 0f);
                }
                EditorUtility.SetDirty(mat);
                converted++;
            }
            AssetDatabase.SaveAssets();
            if (converted > 0) Debug.Log($"[Birds] 转换了 {converted} 个材质到 URP");
        }

        /// <summary>放置鸟群控制器和落脚点。</summary>
        public static void Build(Terrain terrain, DayNightCycle dayNight)
        {
            var go = new GameObject("Birds");
            // Living Birds 在 Assembly-CSharp 里，模块程序集引用不到，用反射挂组件、SerializedObject 写参数
            var type = System.Type.GetType("lb_BirdController, Assembly-CSharp");
            if (type == null) { Debug.LogWarning("[Birds] 没找到 lb_BirdController，跳过鸟群"); Object.DestroyImmediate(go); return; }
            var ctrl = (MonoBehaviour)go.AddComponent(type);
            var so = new SerializedObject(ctrl);
            so.FindProperty("idealNumberOfBirds").intValue = 10;
            so.FindProperty("maximumNumberOfBirds").intValue = 16;
            so.FindProperty("unspawnDistance").floatValue = 55f;
            so.FindProperty("highQuality").boolValue = true;
            so.FindProperty("collideWithObjects").boolValue = true;
            so.FindProperty("groundLayer").intValue = 1; // Default：地形和普通物体
            so.FindProperty("birdScale").floatValue = 1f;
            so.ApplyModifiedPropertiesWithoutUndo();
            var schedule = new GameObject("BirdsSchedule").AddComponent<BirdsSchedule>(); // 不能挂在鸟群物体上，否则关掉后没法再打开
            schedule.dayNight = dayNight;
            schedule.controller = ctrl;

            var targets = new GameObject("BirdTargets").transform;
            int ground = 0, perch = 0;
            float baseY = terrain.transform.position.y;
            // 地面落脚点：沙滩、草甸、林间空地（不要放在水里）
            for (int attempts = 0; ground < 45 && attempts < 4000; attempts++)
            {
                float x = Random.Range(-250f, 250f), z = Random.Range(-250f, 250f);
                float h = terrain.SampleHeight(new Vector3(x, 0, z)) + baseY;
                if (h < 1f || h > 45f) continue;
                if (IslandRegions.At(x, z).riverDist < IslandRegions.RiverHalfWidth + 1f) continue;
                Target(targets, "lb_groundTarget", new Vector3(x, h + 0.5f, z), new Vector3(8f, 1f, 8f));
                ground++;
            }
            // 栖息点：神殿柱顶、巨石、箱子等物体的顶面
            foreach (var name in new[] { "Column", "Capital", "Cover", "Pillar", "boulder_01", "rock_face_01", "wooden_crate_02", "Cornice" })
                foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r => r.name.StartsWith(name)).Take(8))
                {
                    var b = r.bounds;
                    Target(targets, "lb_perchTarget", new Vector3(b.center.x, b.max.y + 0.15f, b.center.z), new Vector3(Mathf.Min(b.size.x, 2f), 0.3f, Mathf.Min(b.size.z, 2f)));
                    perch++;
                }
            Debug.Log($"[Birds] 鸟群控制器已放置：地面落脚点 {ground}，栖息点 {perch}");
        }

        static void Target(Transform parent, string tag, Vector3 pos, Vector3 size)
        {
            var t = new GameObject(tag);
            t.transform.SetParent(parent);
            t.transform.position = pos;
            t.tag = tag;
            t.layer = 2; // Ignore Raycast：玩家和射线都不会碰到
            var c = t.AddComponent<BoxCollider>();
            c.size = size;
            c.isTrigger = true;
        }
    }
}
