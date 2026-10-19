using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rishi.Builders
{
    /// <summary>
    /// 商店资源包的接入：材质转换到 URP、按路径取预制体、生成材质变体（海草摆动），以及一张总览截图。
    /// 资源包：Fish - PolyPack（Alstra Infinite）、Fishes And Underwater Environment Set（MARCIN'S ASSETS）、
    /// Pixit3D - Fish Free Pack（Pixit Games）、Machete01（Nikolay Fedorov）、Water Splash Pack（Rocklynn Productions）。
    /// </summary>
    public static class StoreAssets
    {
        public const string Poly = "Assets/Alstra Infinite/Fish - PolyPack";
        public const string Marcin = "Assets/Fishes And Underwater Environment Set";
        public const string Pixit = "Assets/PixitGames/Pixit3D/PixitUnderseaCollection/LowPolyFishFree";
        public const string Machete = "Assets/Machete01";
        public const string Splash = "Assets/Rocklynn Productions - Water Splash Pack";

        public static bool Available => AssetDatabase.IsValidFolder(Marcin) && AssetDatabase.IsValidFolder(Pixit);

        public static GameObject Prefab(string path)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (p == null) Debug.LogWarning("[Store] 找不到预制体 " + path);
            return p;
        }

        public static GameObject MarcinEnv(string name) => Prefab($"{Marcin}/2.prefabs/environment_objects/{name}.prefab");
        public static GameObject MarcinFish(string name) => Prefab($"{Marcin}/2.prefabs/fishes/{name}.prefab");
        public static GameObject PolyFish(string name) => Prefab($"{Poly}/Prefabs/{name}.prefab");
        public static GameObject PixitPrefab(string name) => Prefab($"{Pixit}/Prefabs/{name}_Unity.prefab");

        /// <summary>Marcin 资源的贴图集材质（已转 URP）。</summary>
        public static Material MarcinAtlas => AssetDatabase.LoadAssetAtPath<Material>($"{Marcin}/4.materials/environment_tex.mat");

        /// <summary>把内置管线的材质转成 URP Lit（保留贴图和颜色）。</summary>
        public static void ConvertMaterials()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            int n = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { Poly, Marcin, Machete, Pixit }))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (mat == null || mat.shader == lit || mat.shader.name.StartsWith("Island/")) continue;
                var tex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                var color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
                var bump = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
                var metal = mat.HasProperty("_MetallicGlossMap") ? mat.GetTexture("_MetallicGlossMap") : null;
                float gloss = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0.3f;
                float metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;
                mat.shader = lit;
                if (tex != null) mat.SetTexture("_BaseMap", tex);
                mat.SetColor("_BaseColor", color);
                mat.SetFloat("_Smoothness", Mathf.Min(gloss, 0.6f));
                mat.SetFloat("_Metallic", metallic);
                if (bump != null) { mat.SetTexture("_BumpMap", bump); mat.EnableKeyword("_NORMALMAP"); }
                if (metal != null) { mat.SetTexture("_MetallicGlossMap", metal); mat.EnableKeyword("_METALLICSPECGLOSSMAP"); }
                EditorUtility.SetDirty(mat);
                n++;
            }
            if (n > 0) { AssetDatabase.SaveAssets(); Debug.Log($"[Store] 转换了 {n} 个材质到 URP"); }
        }

        /// <summary>Marcin 的海草用的摆动材质（复用 Island/Foliage 的风动画，摆幅按高度增加）。</summary>
        public static Material SeaweedMaterial()
        {
            const string path = "Assets/Art/Generated/Island/Materials/StoreSeaweed.mat";
            var shader = Shader.Find("Island/Foliage");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            var atlas = MarcinAtlas;
            if (atlas != null) mat.SetTexture("_BaseMap", atlas.GetTexture("_BaseMap"));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.25f);
            mat.SetFloat("_Parallax", 0.35f); // 摆幅
            mat.SetFloat("_Cull", 0f);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>实例化一个商店预制体：多余的材质槽只保留一个，按目标尺寸缩放，底部贴地。</summary>
        public static GameObject Place(GameObject prefab, Transform parent, Vector3 groundPos, float yaw, float targetSize, Material overrideMat = null, bool collider = false, int layer = -1)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(groundPos, Quaternion.Euler(0, yaw, 0) * prefab.transform.localRotation);
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                var mf = r.GetComponent<MeshFilter>();
                int subs = mf != null && mf.sharedMesh != null ? mf.sharedMesh.subMeshCount : 1;
                var mats = r.sharedMaterials;
                if (overrideMat != null) mats = Enumerable.Repeat(overrideMat, subs).ToArray();
                else if (mats.Length > subs)
                {
                    // Marcin 的模型带了一个空的内嵌材质 + 贴图集材质，只留贴图集
                    var atlas = mats.FirstOrDefault(m => m != null && m.name == "environment_tex") ?? mats.Last();
                    mats = Enumerable.Repeat(atlas, subs).ToArray();
                }
                r.sharedMaterials = mats;
            }
            var b = FPSSceneBuilder.Bounds(go);
            float size = Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (size > 1e-4f) go.transform.localScale = prefab.transform.localScale * (targetSize / size);
            b = FPSSceneBuilder.Bounds(go);
            go.transform.position += Vector3.up * (groundPos.y - b.min.y);
            if (collider)
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                    if (mf.GetComponent<Collider>() == null) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            if (layer >= 0) foreach (var t in go.GetComponentsInChildren<Transform>()) t.gameObject.layer = layer;
            return go;
        }

        /// <summary>
        /// 判断鱼模型的朝向：长轴是哪根、头在哪一端（头部那段比尾柄粗），返回让鱼头朝 +Z 的旋转和体长（米）。
        /// </summary>
        public static (Vector3 euler, float length) FishOrientation(GameObject prefab)
        {
            var mf = prefab.GetComponentInChildren<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return (Vector3.zero, 1f);
            var m = Matrix4x4.TRS(Vector3.zero, mf.transform.localRotation, mf.transform.localScale);
            var verts = mf.sharedMesh.vertices.Select(v => m.MultiplyPoint3x4(v)).ToArray();
            var min = verts.Aggregate(Vector3.Min); var max = verts.Aggregate(Vector3.Max);
            var size = max - min;
            bool alongX = size.x >= size.z;
            float lo = alongX ? min.x : min.z, hi = alongX ? max.x : max.z, len = hi - lo;
            float Thick(float a, float b) => verts.Where(v => { float t = ((alongX ? v.x : v.z) - lo) / len; return t >= a && t <= b; })
                                                 .Select(v => v.y).DefaultIfEmpty(0f).Aggregate((p, q) => Mathf.Max(Mathf.Abs(p), Mathf.Abs(q)));
            // 两端 12%~28% 的那段：头部一侧更粗，尾柄一侧更细
            bool headAtHigh = Thick(0.72f, 0.88f) >= Thick(0.12f, 0.28f);
            Vector3 euler = alongX ? new Vector3(0, headAtHigh ? -90f : 90f, 0) : new Vector3(0, headAtHigh ? 0f : 180f, 0);
            return (euler, len * Mathf.Abs(prefab.transform.localScale.x));
        }

        // ================= 总览截图 =================

        [MenuItem("Tools/Island/商店资源总览截图")]
        public static void ContactSheet()
        {
            ConvertMaterials();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.transform.rotation = Quaternion.Euler(40, -30, 0);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.62f);

            var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { Poly, Marcin, Pixit, Machete })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !p.Contains("background") && !p.Contains("splitted"))
                .OrderBy(p => p).ToList();
            var labels = new List<string>();
            const int cols = 9;
            for (int i = 0; i < prefabs.Count; i++)
            {
                var go = Place(AssetDatabase.LoadAssetAtPath<GameObject>(prefabs[i]), null, new Vector3((i % cols) * 1.4f, 0, -(i / cols) * 1.6f), 150f, 1f);
                labels.Add($"{i:00} {Path.GetFileNameWithoutExtension(prefabs[i])}");
            }
            int rows = Mathf.CeilToInt(prefabs.Count / (float)cols);
            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = rows * 0.85f + 0.4f;
            cam.transform.position = new Vector3((cols - 1) * 0.7f, 6f, -(rows - 1) * 0.8f - 6f);
            cam.transform.rotation = Quaternion.Euler(30, 0, 0);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.25f, 0.3f, 0.35f);
            const int w = 2400, h = 1700;
            var rt = new RenderTexture(w, h, 24);
            cam.targetTexture = rt;
            cam.aspect = w / (float)h;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            Directory.CreateDirectory("Screenshots/Store");
            File.WriteAllBytes("Screenshots/Store/contact.png", tex.EncodeToPNG());
            File.WriteAllLines("Screenshots/Store/contact.txt", labels);
            RenderTexture.active = null;
            Debug.Log("[Store] 总览截图已写入 Screenshots/Store/");
        }
    }
}
