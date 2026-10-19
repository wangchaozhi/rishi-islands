using System.Collections.Generic;
using System.IO;
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
    /// 程序化热带植物：椰子树（弯曲树干 + 下垂羽状叶 + 椰子）、芭蕉、海芋。
    /// 生成的网格和预制体放在 Assets/Art/Generated/Jungle。
    /// </summary>
    public static class JunglePlants
    {
        const string Gen = "Assets/Art/Generated/Jungle";
        const string Tex = "Assets/Art/Jungle";

        // ================= 材质 =================

        static Material Mat(string name, string albedo, string normal, bool cutout, float smooth, Color tint)
        {
            Directory.CreateDirectory(Gen);
            string path = $"{Gen}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Tex}/{albedo}.png"));
            mat.SetColor("_BaseColor", tint);
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Tex}/{normal}.png"));
                mat.EnableKeyword("_NORMALMAP");
            }
            mat.SetFloat("_Smoothness", smooth);
            mat.SetFloat("_Metallic", 0f);
            if (cutout)
            {
                mat.SetFloat("_AlphaClip", 1f);
                mat.SetFloat("_Cutoff", 0.4f);
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.SetFloat("_Cull", 0f);
                mat.SetOverrideTag("RenderType", "TransparentCutout");
                mat.renderQueue = (int)RenderQueue.AlphaTest;
            }
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static void ConfigureTextures()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Tex }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                bool normal = path.EndsWith("_normal.png");
                bool alpha = !normal && ti.DoesSourceTextureHaveAlpha();
                if ((normal && ti.textureType == TextureImporterType.NormalMap) || (!normal && (!alpha || ti.mipMapsPreserveCoverage))) continue;
                if (normal) ti.textureType = TextureImporterType.NormalMap;
                if (alpha)
                {
                    ti.alphaIsTransparency = true;
                    ti.mipMapsPreserveCoverage = true;
                    ti.alphaTestReferenceValue = 0.4f;
                }
                ti.SaveAndReimport();
            }
        }

        public static Material BarkMaterial() => Mat("PalmBark", "palm_bark_albedo", "palm_bark_normal", false, 0.15f, Color.white);

        /// <summary>红树林气根：从树干 1.4 米高处向四周拱形伸进泥里的一圈根（本地原点在地面）。</summary>
        public static Mesh MangroveRoots(int variant)
        {
            string path = $"{Gen}/MangroveRoots_{variant}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;
            var rnd = new System.Random(700 + variant);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var tris = new List<int>();
            int roots = 9 + rnd.Next(5);
            for (int k = 0; k < roots; k++)
            {
                float ang = k / (float)roots * Mathf.PI * 2 + R(-0.2f, 0.2f);
                var outDir = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
                float reach = R(1.3f, 2.4f), top = R(1.2f, 1.6f), arch = R(0.3f, 0.7f), rad = R(0.045f, 0.08f);
                const int segs = 10, sides = 6;
                int start = v.Count;
                for (int s = 0; s <= segs; s++)
                {
                    float t = s / (float)segs;
                    // 先向外拱起再落地
                    Vector3 c = outDir * reach * t + Vector3.up * (Mathf.Lerp(top, -0.15f, t * t) + Mathf.Sin(t * Mathf.PI) * arch);
                    Vector3 tan = (outDir * reach + Vector3.up * (-2f * t * (top + 0.15f) + Mathf.Cos(t * Mathf.PI) * Mathf.PI * arch)).normalized;
                    Vector3 a = Vector3.Cross(tan, Vector3.up).normalized;
                    Vector3 b = Vector3.Cross(a, tan).normalized;
                    float r = rad * Mathf.Lerp(1.3f, 0.7f, t);
                    for (int j = 0; j <= sides; j++)
                    {
                        float an = j / (float)sides * Mathf.PI * 2;
                        v.Add(c + (a * Mathf.Cos(an) + b * Mathf.Sin(an)) * r);
                        uv.Add(new Vector2(j / (float)sides, t * 2f));
                    }
                }
                for (int s = 0; s < segs; s++)
                    for (int j = 0; j < sides; j++)
                    {
                        int i0 = start + s * (sides + 1) + j, i1 = i0 + 1, i2 = i0 + sides + 1, i3 = i2 + 1;
                        tris.AddRange(new[] { i0, i2, i1, i1, i2, i3 });
                    }
            }
            var mesh = new Mesh { name = $"MangroveRoots_{variant}" };
            mesh.SetVertices(v);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            Sanitize(mesh);
            mesh.RecalculateBounds();
            Directory.CreateDirectory(Gen);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        // ================= 雨林大树（轻量，可以成片铺满） =================

        /// <summary>
        /// 程序化雨林大树：带板根的高直树干、几根斜伸的枝干、由叶团面片组成的树冠（法线朝外，光照柔和）。
        /// 约 2~3 千个三角面，可以上千棵铺满全岛。子网格 0 = 树皮，1 = 叶片。
        /// </summary>
        public static GameObject CanopyTree(int variant)
        {
            string prefabPath = $"{Gen}/Canopy_{variant}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing != null) return existing;

            var rnd = new System.Random(300 + variant * 17);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            float height = R(12f, 21f), baseR = R(0.35f, 0.55f);
            Vector3 lean = new Vector3(R(-1f, 1f), 0, R(-1f, 1f)) * R(0f, 1.2f);
            int fins = 4 + rnd.Next(2);
            float finPhase = R(0, 6.28f);

            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var bark = new List<int>();
            var leaves = new List<int>();

            // ---- 树干：底部板根（星形外扩） ----
            Vector3 Spine(float t) => new Vector3(lean.x * t * t, height * t, lean.z * t * t);
            const int rings = 22, sides = 14;
            for (int i = 0; i <= rings; i++)
            {
                float t = i / (float)rings;
                Vector3 c = Spine(t);
                float r = Mathf.Lerp(baseR, baseR * 0.45f, t);
                float flare = Mathf.Exp(-t * 14f);
                for (int j = 0; j <= sides; j++)
                {
                    float a = j / (float)sides * Mathf.PI * 2;
                    float fin = Mathf.Pow(Mathf.Max(0, Mathf.Cos(a * fins + finPhase)), 6f) * 2.6f; // 板根
                    float rr = r * (1f + flare * (0.6f + fin));
                    var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    v.Add(c + dir * rr - Vector3.up * (t == 0 ? 0.3f : 0));
                    n.Add(dir);
                    uv.Add(new Vector2(j / (float)sides * 2f, t * height / 4f));
                }
            }
            for (int i = 0; i < rings; i++)
                for (int j = 0; j < sides; j++)
                {
                    int a = i * (sides + 1) + j, b = a + 1, c2 = a + sides + 1, d = c2 + 1;
                    bark.AddRange(new[] { a, c2, b, b, c2, d });
                }

            // ---- 枝干与叶团 ----
            var puffs = new List<(Vector3 c, float r)>();
            Vector3 top = Spine(1f);
            puffs.Add((top + Vector3.up * 1.2f, R(3.2f, 4.2f)));
            int branches = 3 + rnd.Next(3);
            for (int k = 0; k < branches; k++)
            {
                float t0 = R(0.68f, 0.9f);
                Vector3 start = Spine(t0);
                float ang = k / (float)branches * Mathf.PI * 2 + R(-0.4f, 0.4f);
                Vector3 dir = (new Vector3(Mathf.Cos(ang), R(0.55f, 0.95f), Mathf.Sin(ang))).normalized;
                float len = R(3f, 6f);
                Vector3 end = start + dir * len;
                Tube(v, n, uv, bark, start, end, baseR * 0.4f, baseR * 0.15f);
                puffs.Add((end + Vector3.up * 0.6f, R(2.6f, 3.6f)));
                puffs.Add((Vector3.Lerp(start, end, 0.6f) + Vector3.up * 1.2f, R(1.8f, 2.6f)));
            }
            Vector3 crownCenter = Vector3.zero;
            foreach (var p in puffs) crownCenter += p.c;
            crownCenter /= puffs.Count;

            foreach (var (c, r) in puffs)
            {
                // 每个叶团：三张互相交叉的面片 + 一张水平面片，法线从树冠中心向外
                for (int q = 0; q < 4; q++)
                {
                    Quaternion rot = q < 3 ? Quaternion.Euler(R(-20, 20), q * 60f + R(-15, 15), 0) : Quaternion.Euler(90, R(0, 360), 0);
                    Vector3 ax = rot * Vector3.right * r, ay = rot * Vector3.up * r;
                    int s0 = v.Count;
                    Vector3[] corners = { c - ax - ay, c + ax - ay, c + ax + ay, c - ax + ay };
                    Vector2[] cuv = { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
                    for (int k = 0; k < 4; k++)
                    {
                        v.Add(corners[k]);
                        n.Add(((corners[k] - crownCenter).normalized + Vector3.up * 0.6f).normalized);
                        uv.Add(cuv[k]);
                    }
                    leaves.AddRange(new[] { s0, s0 + 2, s0 + 1, s0, s0 + 3, s0 + 2 });
                }
            }

            var mesh = new Mesh { name = $"Canopy_{variant}", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(bark, 0);
            mesh.SetTriangles(leaves, 1);
            mesh.RecalculateTangents();
            Sanitize(mesh);
            mesh.RecalculateBounds();
            Directory.CreateDirectory(Gen);
            AssetDatabase.CreateAsset(mesh, $"{Gen}/Canopy_{variant}.asset");

            var go = new GameObject($"Canopy_{variant}");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = new[]
            {
                Mat("CanopyBark", "canopy_bark_albedo", null, false, 0.1f, Color.white),
                Mat("CanopyLeaves", "canopy_leaves_albedo", null, true, 0.3f, new Color(0.85f, 1f, 0.8f)),
            };
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = baseR * 1.2f;
            col.height = 5f;
            col.center = new Vector3(0, 2.5f, 0);
            go.AddComponent<SurfaceTag>().surface = "wood";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        /// <summary>
        /// 高大的林下灌丛（1.5~3 米）：从地面一直堆到顶部的叶团，单材质，给地形细节系统成片铺设。
        /// 本地原点在地面中心。
        /// </summary>
        public static (Mesh mesh, Material mat) TallShrub(int variant)
        {
            var mat = Mat("ShrubLeaves", "canopy_leaves_albedo", null, true, 0.25f, new Color(0.8f, 1f, 0.72f));
            string path = $"{Gen}/TallShrub_{variant}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return (existing, mat);

            var rnd = new System.Random(1100 + variant * 7);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            float height = R(1.5f, 3f);
            var center = new Vector3(0, height * 0.5f, 0);
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var tris = new List<int>();
            int puffs = 9 + rnd.Next(6);
            for (int p = 0; p < puffs; p++)
            {
                float t = p / (float)(puffs - 1);
                float r = Mathf.Lerp(0.55f, 0.95f, Mathf.Sin(t * Mathf.PI)) * R(0.8f, 1.15f);
                float spread = Mathf.Lerp(0.3f, 0.8f, Mathf.Sin(t * Mathf.PI));
                float a = R(0, Mathf.PI * 2);
                var c = new Vector3(Mathf.Cos(a) * spread, Mathf.Lerp(0.35f, height - 0.3f, t), Mathf.Sin(a) * spread);
                for (int q = 0; q < 4; q++)
                {
                    Quaternion rot = q < 3 ? Quaternion.Euler(R(-15, 15), q * 60f + R(-20, 20), 0) : Quaternion.Euler(90, R(0, 360), 0);
                    Vector3 ax = rot * Vector3.right * r, ay = rot * Vector3.up * r;
                    int s0 = v.Count;
                    Vector3[] corners = { c - ax - ay, c + ax - ay, c + ax + ay, c - ax + ay };
                    Vector2[] cuv = { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
                    for (int k = 0; k < 4; k++)
                    {
                        var pos = corners[k];
                        pos.y = Mathf.Max(pos.y, 0.02f); // 不要扎进地里太深
                        v.Add(pos);
                        n.Add(((pos - center).normalized + Vector3.up * 0.5f).normalized);
                        uv.Add(cuv[k]);
                    }
                    tris.AddRange(new[] { s0, s0 + 2, s0 + 1, s0, s0 + 3, s0 + 2 });
                }
            }
            var mesh = new Mesh { name = $"TallShrub_{variant}" };
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateTangents();
            Sanitize(mesh);
            mesh.RecalculateBounds();
            Directory.CreateDirectory(Gen);
            AssetDatabase.CreateAsset(mesh, path);
            return (mesh, mat);
        }

        static void Tube(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> tris, Vector3 a, Vector3 b, float ra, float rb)
        {
            const int sides = 6, segs = 3;
            Vector3 dir = (b - a).normalized;
            Vector3 s1 = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.95f ? Vector3.up : Vector3.right).normalized;
            Vector3 s2 = Vector3.Cross(dir, s1);
            int start = v.Count;
            for (int i = 0; i <= segs; i++)
            {
                float t = i / (float)segs;
                Vector3 c = Vector3.Lerp(a, b, t);
                float r = Mathf.Lerp(ra, rb, t);
                for (int j = 0; j <= sides; j++)
                {
                    float an = j / (float)sides * Mathf.PI * 2;
                    Vector3 d = s1 * Mathf.Cos(an) + s2 * Mathf.Sin(an);
                    v.Add(c + d * r);
                    n.Add(d);
                    uv.Add(new Vector2(j / (float)sides, t * 2f));
                }
            }
            for (int i = 0; i < segs; i++)
                for (int j = 0; j < sides; j++)
                {
                    int i0 = start + i * (sides + 1) + j, i1 = i0 + 1, i2 = i0 + sides + 1, i3 = i2 + 1;
                    tris.AddRange(new[] { i0, i2, i1, i1, i2, i3 });
                }
        }

        // ================= 椰子树 =================

        /// <summary>生成第 variant 种椰子树预制体（树干沿本地 +Z 方向倾斜）。</summary>
        public static GameObject Palm(int variant)
        {
            string prefabPath = $"{Gen}/Palm_{variant}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing != null) return existing;

            var rnd = new System.Random(100 + variant * 31);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            float height = R(7.5f, 12f);
            float lean = R(0.8f, 3.2f);

            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var trunkTris = new List<int>();
            var frondTris = new List<int>();
            var nutTris = new List<int>();

            // ---- 树干 ----
            Vector3 Spine(float t) => new Vector3(Mathf.Sin(t * 3.1f + variant) * 0.12f * t, height * t, lean * t * t);
            const int rings = 30, sides = 12;
            for (int i = 0; i <= rings; i++)
            {
                float t = i / (float)rings;
                Vector3 c = Spine(t);
                Vector3 tan = (Spine(Mathf.Min(1, t + 0.01f)) - Spine(Mathf.Max(0, t - 0.01f))).normalized;
                Vector3 side = Vector3.Cross(tan, Vector3.right).normalized;
                Vector3 bin = Vector3.Cross(side, tan).normalized;
                float r = Mathf.Lerp(0.25f, 0.15f, t) * (1f + 0.05f * Mathf.Sin(t * height * Mathf.PI * 2.4f)) + 0.16f * Mathf.Exp(-t * 14f);
                for (int j = 0; j <= sides; j++)
                {
                    float a = j / (float)sides * Mathf.PI * 2;
                    Vector3 n = side * Mathf.Cos(a) + bin * Mathf.Sin(a);
                    verts.Add(c + n * r);
                    normals.Add(n);
                    uvs.Add(new Vector2(j / (float)sides, t * height / 3f));
                }
            }
            for (int i = 0; i < rings; i++)
                for (int j = 0; j < sides; j++)
                {
                    int a = i * (sides + 1) + j, b = a + 1, c2 = a + sides + 1, d = c2 + 1;
                    trunkTris.AddRange(new[] { a, c2, b, b, c2, d });
                }

            // ---- 叶冠 ----
            Vector3 crown = Spine(1f);
            int fronds = 14 + rnd.Next(5);
            for (int f = 0; f < fronds; f++)
            {
                float yaw = f * 137.5f + R(-12f, 12f);
                float elev = Mathf.Lerp(55f, -15f, f / (float)fronds) + R(-8f, 8f);
                float len = R(3.6f, 5.2f) * height / 10f;
                float maxW = R(0.9f, 1.25f) * len / 4.4f;
                Vector3 dirXZ = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
                Vector3 dir = (dirXZ * Mathf.Cos(elev * Mathf.Deg2Rad) + Vector3.up * Mathf.Sin(elev * Mathf.Deg2Rad)).normalized;
                Vector3 p = crown;
                const int segs = 12;
                float ds = len / segs;
                float droop = R(0.08f, 0.16f);
                int baseIndex = verts.Count;
                for (int s = 0; s <= segs; s++)
                {
                    float t = s / (float)segs;
                    Vector3 across = Vector3.Cross(dir, Vector3.up);
                    if (across.sqrMagnitude < 1e-4f) across = Vector3.Cross(dir, dirXZ);
                    across.Normalize();
                    Vector3 up = Vector3.Cross(across, dir).normalized;
                    float w = Mathf.Max(maxW * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Clamp01(t * 0.9f + 0.05f)), 0.8f) * 0.5f, 0.03f); // 叶尖留最小宽度，避免零面积三角形
                    // V 形折叠：两侧略低于中脉
                    Vector3 fold = -up * w * 0.28f;
                    verts.Add(p - across * w + fold); normals.Add(up); uvs.Add(new Vector2(0f, t));
                    verts.Add(p);                     normals.Add(up); uvs.Add(new Vector2(0.5f, t));
                    verts.Add(p + across * w + fold); normals.Add(up); uvs.Add(new Vector2(1f, t));
                    p += dir * ds;
                    dir = (dir + Vector3.down * droop * (0.4f + t * 1.6f)).normalized;
                }
                for (int s = 0; s < segs; s++)
                {
                    int r0 = baseIndex + s * 3, r1 = r0 + 3;
                    frondTris.AddRange(new[] { r0, r1, r0 + 1, r0 + 1, r1, r1 + 1, r0 + 1, r1 + 1, r0 + 2, r0 + 2, r1 + 1, r1 + 2 });
                }
            }

            // ---- 椰子 ----
            int nuts = 3 + rnd.Next(4);
            for (int k = 0; k < nuts; k++)
            {
                Vector3 c = crown + Quaternion.Euler(0, k * 360f / nuts + R(-20, 20), 0) * Vector3.forward * 0.22f + Vector3.down * R(0.15f, 0.35f);
                AddSphere(verts, normals, uvs, nutTris, c, R(0.11f, 0.14f));
            }

            var mesh = new Mesh { name = $"Palm_{variant}", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 3;
            mesh.SetTriangles(trunkTris, 0);
            mesh.SetTriangles(frondTris, 1);
            mesh.SetTriangles(nutTris, 2);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            Sanitize(mesh);
            mesh.RecalculateBounds();
            Directory.CreateDirectory(Gen);
            AssetDatabase.CreateAsset(mesh, $"{Gen}/Palm_{variant}.asset");

            var go = new GameObject($"Palm_{variant}");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[]
            {
                Mat("PalmBark", "palm_bark_albedo", "palm_bark_normal", false, 0.15f, Color.white),
                Mat("PalmFrond", "palm_frond_albedo", "palm_frond_normal", true, 0.35f, Color.white),
                FPSSceneBuilder.Painted("Coconut", new Color(0.32f, 0.36f, 0.12f), 0f, 0.4f),
            };
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = 0.25f;
            col.height = Mathf.Min(height * 0.5f, 4f);
            col.center = new Vector3(0, col.height / 2, 0);
            go.AddComponent<SurfaceTag>().surface = "wood";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static void AddSphere(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> tris, Vector3 c, float r)
        {
            const int lat = 6, lon = 8;
            int start = v.Count;
            for (int i = 0; i <= lat; i++)
                for (int j = 0; j <= lon; j++)
                {
                    float th = i / (float)lat * Mathf.PI, ph = j / (float)lon * Mathf.PI * 2;
                    var d = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                    v.Add(c + d * r);
                    n.Add(d);
                    uv.Add(new Vector2(j / (float)lon, i / (float)lat));
                }
            for (int i = 0; i < lat; i++)
                for (int j = 0; j < lon; j++)
                {
                    int a = start + i * (lon + 1) + j, b = a + 1, c2 = a + lon + 1, d = c2 + 1;
                    tris.AddRange(new[] { a, b, c2, b, d, c2 });
                }
        }

        // ================= 林下大叶植物（地形细节用，单材质） =================

        /// <summary>芭蕉：一根假茎 + 6~8 片向外下垂的长叶。</summary>
        public static (Mesh mesh, Material mat) Banana(int variant)
        {
            var rnd = new System.Random(500 + variant);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            var mat = Mat("BananaLeaf", "banana_leaf_albedo", "banana_leaf_normal", true, 0.45f, Color.white);
            return (LeafPlant($"Banana_{variant}", rnd, stemHeight: R(1.0f, 1.8f), leaves: 6 + rnd.Next(3),
                              leafLen: R(1.5f, 2.1f), leafWidth: R(0.5f, 0.62f), elevMin: 25f, elevMax: 70f, droop: 0.32f, stalk: 0f), mat);
        }

        /// <summary>海芋（象耳）：从地面伸出的叶柄托着宽大的心形叶。</summary>
        public static (Mesh mesh, Material mat) Taro(int variant)
        {
            var rnd = new System.Random(900 + variant);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            var mat = Mat("TaroLeaf", "taro_leaf_albedo", "taro_leaf_normal", true, 0.55f, Color.white);
            return (LeafPlant($"Taro_{variant}", rnd, stemHeight: 0f, leaves: 4 + rnd.Next(3),
                              leafLen: R(0.8f, 1.1f), leafWidth: R(0.6f, 0.8f), elevMin: -25f, elevMax: 5f, droop: 0.15f, stalk: R(0.6f, 1.1f)), mat);
        }

        static Mesh LeafPlant(string name, System.Random rnd, float stemHeight, int leaves, float leafLen, float leafWidth,
                              float elevMin, float elevMax, float droop, float stalk)
        {
            string path = $"{Gen}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);

            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var tris = new List<int>();

            // 茎 / 叶柄用叶片贴图中脉那一条（u = 0.5）着色
            void Stem(Vector3 a, Vector3 b, float r)
            {
                const int sides = 5;
                int s0 = v.Count;
                for (int k = 0; k <= 1; k++)
                    for (int j = 0; j <= sides; j++)
                    {
                        float ang = j / (float)sides * Mathf.PI * 2;
                        Vector3 off = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * r * (k == 0 ? 1f : 0.7f);
                        v.Add((k == 0 ? a : b) + off);
                        uv.Add(new Vector2(0.5f, 0.02f + k * 0.05f));
                    }
                for (int j = 0; j < sides; j++)
                {
                    int a0 = s0 + j, b0 = a0 + 1, c0 = a0 + sides + 1, d0 = c0 + 1;
                    tris.AddRange(new[] { a0, c0, b0, b0, c0, d0 });
                }
            }

            Vector3 top = Vector3.up * stemHeight;
            if (stemHeight > 0) Stem(Vector3.zero, top, 0.11f);
            for (int l = 0; l < leaves; l++)
            {
                float yaw = l * 137.5f + R(-15, 15);
                float elev = R(elevMin, elevMax);
                Vector3 dirXZ = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
                Vector3 origin = top;
                if (stalk > 0)
                {
                    // 叶柄：从地面斜向上伸出
                    Vector3 stalkEnd = dirXZ * stalk * 0.35f + Vector3.up * stalk * R(0.75f, 1.05f);
                    Stem(Vector3.zero, stalkEnd, 0.025f);
                    origin = stalkEnd;
                }
                Vector3 dir = (dirXZ * Mathf.Cos(elev * Mathf.Deg2Rad) + Vector3.up * Mathf.Sin(elev * Mathf.Deg2Rad)).normalized;
                const int segs = 8;
                float ds = leafLen / segs;
                Vector3 p = origin;
                int b = v.Count;
                for (int s = 0; s <= segs; s++)
                {
                    float t = s / (float)segs;
                    Vector3 across = Vector3.Cross(dir, Vector3.up).normalized;
                    Vector3 up = Vector3.Cross(across, dir).normalized;
                    float w = leafWidth * 0.5f;
                    Vector3 curl = -up * w * 0.35f; // 叶缘下卷
                    v.Add(p - across * w + curl); uv.Add(new Vector2(0, t));
                    v.Add(p);                     uv.Add(new Vector2(0.5f, t));
                    v.Add(p + across * w + curl); uv.Add(new Vector2(1, t));
                    p += dir * ds;
                    dir = (dir + Vector3.down * droop * (0.3f + t)).normalized;
                }
                for (int s = 0; s < segs; s++)
                {
                    int r0 = b + s * 3, r1 = r0 + 3;
                    tris.AddRange(new[] { r0, r1, r0 + 1, r0 + 1, r1, r1 + 1, r0 + 1, r1 + 1, r0 + 2, r0 + 2, r1 + 1, r1 + 2 });
                }
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(v);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            Sanitize(mesh);
            mesh.RecalculateBounds();
            Directory.CreateDirectory(Gen);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        /// <summary>
        /// 清洗法线和切线：退化三角形（零面积、UV 重合）会算出 NaN，带法线贴图的材质着色后变成 NaN 像素，
        /// 再被泛光放大成一大团白雾。这里把无效值换成合理的默认值，并统计修了多少个。
        /// </summary>
        static void Sanitize(Mesh mesh)
        {
            static bool Bad(Vector3 v) => float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z) || v.sqrMagnitude < 1e-8f;
            var n = mesh.normals;
            var t = mesh.tangents;
            int fixedN = 0, fixedT = 0;
            for (int i = 0; i < n.Length; i++)
            {
                if (Bad(n[i])) { n[i] = Vector3.up; fixedN++; }
                var tv = new Vector3(t[i].x, t[i].y, t[i].z);
                if (Bad(tv) || float.IsNaN(t[i].w))
                {
                    var perp = Vector3.Cross(n[i], Mathf.Abs(n[i].y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                    t[i] = new Vector4(perp.x, perp.y, perp.z, 1f);
                    fixedT++;
                }
            }
            mesh.normals = n;
            mesh.tangents = t;
            if (fixedN + fixedT > 0) Debug.Log($"[Jungle] {mesh.name}: 修复了 {fixedN} 个无效法线、{fixedT} 个无效切线");
        }

        /// <summary>重新生成前清掉旧的程序化植物资源。</summary>
        public static void Clean()
        {
            if (AssetDatabase.IsValidFolder(Gen))
                foreach (var guid in AssetDatabase.FindAssets("t:Mesh t:Prefab", new[] { Gen }))
                    AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
        }
    }
}
