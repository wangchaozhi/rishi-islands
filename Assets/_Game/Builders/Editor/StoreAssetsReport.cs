using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Rishi.Builders
{
    /// <summary>列出商店资源包里每个预制体的尺寸、动画、材质着色器，方便接入（命令行 -executeMethod Rishi.Builders.StoreAssetsReport.Run）。</summary>
    public static class StoreAssetsReport
    {
        static readonly string[] Folders =
        {
            "Assets/Alstra Infinite", "Assets/Fishes And Underwater Environment Set", "Assets/Machete01", "Assets/PixitGames",
        };

        [MenuItem("Tools/Island/商店资源报告")]
        public static void Run()
        {
            var sb = new StringBuilder();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", Folders))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(go);
                inst.transform.position = Vector3.zero;
                var rs = inst.GetComponentsInChildren<Renderer>();
                var b = rs.Length > 0 ? rs[0].bounds : new Bounds();
                foreach (var r in rs) b.Encapsulate(r.bounds);
                var anim = inst.GetComponentInChildren<Animator>();
                var legacy = inst.GetComponentInChildren<Animation>();
                string clips = "";
                if (anim != null && anim.runtimeAnimatorController != null) clips = "animator[" + string.Join(",", anim.runtimeAnimatorController.animationClips.Select(c => c.name)) + "]";
                else if (anim != null) clips = "animator(no controller)";
                if (legacy != null) clips += " legacy[" + string.Join(",", legacy.Cast<AnimationState>().Select(s => s.name)) + "]";
                var modelClips = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(rs.FirstOrDefault()?.gameObject ?? inst) ?? go))
                    .OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).Select(c => c.name);
                var shaders = rs.SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => $"{m.name}:{m.shader.name}").Distinct();
                var scripts = inst.GetComponentsInChildren<MonoBehaviour>().Where(m => m != null).Select(m => m.GetType().Name).Distinct();
                sb.AppendLine($"{path} | size {b.size.x:0.##}x{b.size.y:0.##}x{b.size.z:0.##} center {b.center.x:0.##},{b.center.y:0.##},{b.center.z:0.##} | renderers {rs.Length} skinned {inst.GetComponentsInChildren<SkinnedMeshRenderer>().Length} | {clips} modelClips[{string.Join(",", modelClips)}] | mats {string.Join(" ", shaders)} | scripts {string.Join(",", scripts)}");
                Object.DestroyImmediate(inst);
            }
            System.IO.File.WriteAllText("Logs/store_assets_report.txt", sb.ToString());
            Debug.Log("[Store] report written");
        }
    }
}
