using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Rishi.Core;
using Rishi.Inventory;
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
    /// 多地貌的场景元素：溪流水面与水声、红树林（气根）与蛙鸣、北岸海崖、巨石与岩壁。
    /// 由 IslandBuilder 在地形生成之后、植被之前调用（会登记避让区）。
    /// </summary>
    public static class IslandFeatures
    {
        const string Gen = "Assets/Art/Generated/Island";

        static float H(Terrain t, float x, float z) => t.SampleHeight(new Vector3(x, 0, z)) + t.transform.position.y;

        // ================= 溪流 =================

        public static void BuildRiver(Terrain terrain)
        {
            // 沿路线每 2 米取一个点
            var pts = new List<Vector2>();
            var path = IslandRegions.RiverPath;
            for (int i = 0; i < path.Length - 1; i++)
            {
                int n = Mathf.CeilToInt(Vector2.Distance(path[i], path[i + 1]) / 2f);
                for (int k = 0; k < n; k++) pts.Add(Vector2.Lerp(path[i], path[i + 1], k / (float)n));
            }
            pts.Add(path[path.Length - 1]);

            // 水面高度 = 河床 + 0.45 米，沿水流只降不升，入海口贴近海平面
            var level = new float[pts.Count];
            for (int i = 0; i < pts.Count; i++)
            {
                level[i] = H(terrain, pts[i].x, pts[i].y) + 0.45f;
                if (i > 0) level[i] = Mathf.Min(level[i], level[i - 1]);
                level[i] = Mathf.Max(level[i], 0.05f);
            }

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            float dist = 0;
            float half = IslandRegions.RiverHalfWidth + 0.6f;
            for (int i = 0; i < pts.Count; i++)
            {
                Vector2 dir = (pts[Mathf.Min(i + 1, pts.Count - 1)] - pts[Mathf.Max(i - 1, 0)]).normalized;
                Vector2 side = new Vector2(-dir.y, dir.x);
                if (i > 0) dist += Vector2.Distance(pts[i], pts[i - 1]);
                for (int s = 0; s <= 2; s++)
                {
                    float k = s / 2f;
                    Vector2 p = pts[i] + side * Mathf.Lerp(-half, half, k);
                    verts.Add(new Vector3(p.x, level[i], p.y));
                    uvs.Add(new Vector2(k, dist));
                }
                if (i > 0)
                {
                    int a = (i - 1) * 3, b = i * 3;
                    tris.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1, a + 1, b + 1, a + 2, a + 2, b + 1, b + 2 });
                }
            }
            var mesh = new Mesh { name = "River", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            string meshPath = $"{Gen}/Meshes/River.asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);

            var mat = Mat("River", "Island/River");
            mat.SetTexture("_NormalMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Water/water_normal.png"));
            var go = new GameObject("River");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            go.AddComponent<SurfaceTag>().surface = "water";

            // 水声：沿河每 30 米一个 3D 循环音源
            var clip = AudioClip("amb_river");
            for (int i = 0; i < pts.Count; i += 15)
                Loop(go.transform, new Vector3(pts[i].x, level[i] + 0.3f, pts[i].y), clip, 0.7f, 3f, 22f);
            Debug.Log($"[Features] river: {pts.Count} samples, {dist:0} m, source level {level[0]:0.0} m");
        }

        // ================= 红树林 =================

        public static void BuildMangroves(Terrain terrain, GameObject treePrefab)
        {
            var parent = new GameObject("Mangroves").transform;
            var placed = new List<Vector3>();
            var rootMeshes = Enumerable.Range(0, 3).Select(JunglePlants.MangroveRoots).ToArray();
            var bark = JunglePlants.BarkMaterial();
            int attempts = 0;
            while (placed.Count < 45 && attempts++ < 20000)
            {
                float x = Random.Range(60f, 290f), z = Random.Range(-140f, 140f);
                var region = IslandRegions.At(x, z);
                if (region.swamp < 0.5f) continue;
                float h = H(terrain, x, z);
                if (h < -0.7f || h > 2.4f) continue;
                var p = new Vector3(x, h, z);
                if (placed.Any(q => (q - p).sqrMagnitude < 7f * 7f)) continue;
                placed.Add(p);

                float s = Random.Range(1.1f, 1.6f);
                var tree = new GameObject("Mangrove").transform;
                tree.SetParent(parent);
                tree.SetPositionAndRotation(p, Quaternion.Euler(0, Random.Range(0, 360f), 0));
                var crown = (GameObject)PrefabUtility.InstantiatePrefab(treePrefab, tree);
                crown.transform.localPosition = Vector3.up * 1.4f * s;   // 树干立在气根上
                crown.transform.localScale = Vector3.one * s;
                var roots = new GameObject("Roots");
                roots.transform.SetParent(tree, false);
                roots.transform.localScale = Vector3.one * s;
                roots.AddComponent<MeshFilter>().sharedMesh = rootMeshes[placed.Count % rootMeshes.Length];
                roots.AddComponent<MeshRenderer>().sharedMaterial = bark;
                var col = tree.gameObject.AddComponent<CapsuleCollider>();
                col.radius = 0.9f * s;
                col.height = 2.4f * s;
                col.center = Vector3.up * 1.2f * s;
                tree.gameObject.AddComponent<SurfaceTag>().surface = "wood";
                foreach (var t in tree.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
            }

            // 沼泽蛙鸣（3D 循环音源）
            var clip = AudioClip("amb_swamp");
            for (int i = 0; i < placed.Count; i += 8)
                Loop(parent, placed[i] + Vector3.up * 0.5f, clip, 0.6f, 4f, 35f);
            foreach (var p in placed) IslandStoryBuilder.ClearZones.Add((new Vector2(p.x, p.z), 3f));
            Debug.Log($"[Features] mangroves: {placed.Count}");
        }

        // ================= 海崖与岩石 =================

        public static void BuildCliffsAndRocks(Terrain terrain)
        {
            var parent = new GameObject("CliffsAndRocks").transform;
            AudioSetup.Tag(parent.gameObject, "stone");
            var cliffMats = new Dictionary<string, Material>();
            Material M(string id) => cliffMats.TryGetValue(id, out var m) ? m : cliffMats[id] = FPSSceneBuilder.Pbr(id, $"Models/{id}", 1f);

            // 北岸：从外海沿径向往岛内找崖脚，摆海崖模型，崖面朝海
            int cliffs = 0;
            for (float ang = 35f; ang <= 145f; ang += 7.5f)
            {
                var dir = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad));
                for (float r = 295f; r > 60f; r -= 1f)
                {
                    var p = dir * r;
                    if (IslandRegions.At(p.x, p.y).cliff < 0.55f) break;
                    float h = H(terrain, p.x, p.y);
                    if (h < 1.5f) continue;
                    string id = cliffs % 3 == 2 ? "coastal_cliff_02" : "coastal_cliff_01";
                    var pos = new Vector3(p.x, -1.5f, p.y);
                    var go = FPSSceneBuilder.PlaceModel($"Models/{id}", M(id), parent, pos, Mathf.Atan2(-dir.x, -dir.y) * Mathf.Rad2Deg + 180f + Random.Range(-15f, 15f), staticCollider: true);
                    go.transform.localScale *= Random.Range(1.6f, 2.4f);
                    var cb = FPSSceneBuilder.Bounds(go);
                    go.transform.position += Vector3.up * (-2f - cb.min.y); // 崖脚埋进海里 2 米
                    cliffs++;
                    break;
                }
            }

            // 巨石：溪谷两岸与高地草甸；岩壁：山腰陡坡；大块崖石：主峰
            int boulders = Scatter(terrain, parent, "boulder_01", M("boulder_01"), 26, 0.8f, 2.2f, (x, z, h, region) =>
                (region.riverDist > IslandRegions.RiverHalfWidth + 1.5f && region.riverDist < 14f) || (h > 6f && Random.value < 0.08f));
            int faces = Scatter(terrain, parent, "rock_face_01", M("rock_face_01"), 10, 1.5f, 3f, (x, z, h, region) =>
                h > 4f && Slope(terrain, x, z) > 22f);
            int peaks = Scatter(terrain, parent, "namaqualand_cliff_01", M("namaqualand_cliff_01"), 7, 1.5f, 2.5f, (x, z, h, region) =>
                h > 9f && Random.value < 0.05f);
            Debug.Log($"[Features] cliffs {cliffs}, boulders {boulders}, rock faces {faces}, peak rocks {peaks}");
        }

        static float Slope(Terrain t, float x, float z)
        {
            var d = t.terrainData;
            return d.GetSteepness((x - t.transform.position.x) / d.size.x, (z - t.transform.position.z) / d.size.z);
        }

        static int Scatter(Terrain terrain, Transform parent, string id, Material mat, int count, float minScale, float maxScale,
                           System.Func<float, float, float, IslandRegions.Info, bool> ok)
        {
            int placed = 0, attempts = 0;
            while (placed < count && attempts++ < 30000)
            {
                float x = Random.Range(-280f, 280f), z = Random.Range(-280f, 280f);
                float h = H(terrain, x, z);
                if (h < 0.5f || IslandStoryBuilder.InClearZone(x, z, 4f)) continue;
                if (!ok(x, z, h, IslandRegions.At(x, z))) continue;
                var go = FPSSceneBuilder.PlaceModel($"Models/{id}", mat, parent, new Vector3(x, h, z), Random.Range(0f, 360f), staticCollider: true);
                float s = Random.Range(minScale, maxScale);
                go.transform.localScale *= s;
                var b = FPSSceneBuilder.Bounds(go);
                go.transform.position += Vector3.up * (h - b.min.y - b.size.y * 0.2f); // 埋进地里一点
                placed++;
            }
            return placed;
        }

        // ================= 工具 =================

        static Material Mat(string name, string shader)
        {
            string path = $"{Gen}/Materials/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(mat, path);
            }
            else mat.shader = Shader.Find(shader);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static AudioClip AudioClip(string id) =>
            AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Audio/Final/{id}.wav") ?? AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Audio/Placeholder/{id}.wav");

        static void Loop(Transform parent, Vector3 pos, AudioClip clip, float volume, float min, float max)
        {
            if (clip == null) return;
            var go = new GameObject("Sound_" + clip.name);
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.playOnAwake = true;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.minDistance = min;
            src.maxDistance = max;
            src.volume = volume;
            src.dopplerLevel = 0f;
        }
    }
}
