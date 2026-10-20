using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Rishi.Core;

namespace Rishi.Builders
{
    /// <summary>
    /// 第二座岛：环礁。一圈珊瑚礁围着中间浅浅的礁湖，礁环上断断续续露出几座沙洲小岛（长椰子树和草），
    /// 朝主岛的方向有一道水道能开船进礁湖，礁湖内侧的一段浅滩是红树林。
    /// 地形由 Spec 参数化（种子、位置、大小、礁环半径与宽度、礁湖深度、沙洲多少），和主岛共用一组地表图层。
    /// 地形在 [地形] 步骤生成，植被在 [地貌与植被] 步骤生成。
    /// </summary>
    public static class AtollBuilder
    {
        [System.Serializable]
        public struct Spec
        {
            public string name;
            public int seed;
            [Tooltip("岛中心的世界坐标（x, z）")] public Vector2 center;
            [Tooltip("地形边长（米）")] public float size;
            [Tooltip("礁环的平均半径（米）")] public float ringRadius;
            [Tooltip("礁环宽度（米），沙洲在礁环中间")] public float ringWidth;
            [Tooltip("礁湖深度（米）")] public float lagoonDepth;
            [Tooltip("0~1，礁环上露出水面的沙洲比例")] public float motuCoverage;
            [Tooltip("水道宽度（度）")] public float passWidth;
        }

        /// <summary>主岛东北方向约 770 米外（中间是 400 米的开阔海面），水道朝向主岛。</summary>
        public static readonly Spec Default = new Spec
        {
            name = "Atoll",
            seed = 4242,
            center = new Vector2(760f, 140f),
            size = 512f,
            ringRadius = 150f,
            ringWidth = 26f,
            lagoonDepth = 4.5f,
            motuCoverage = 0.55f,
            passWidth = 7f,
        };

        public struct Result
        {
            public Terrain terrain;
            public Texture2D heightTex;
            public Vector4 rect, heightRange;
        }

        const float MinHeight = IslandBuilder.MinHeight;
        const float HeightRange = 30f; // -20 ~ 10 米
        const int HeightRes = 513, AlphaRes = 512, DetailRes = 512;

        // ================= 形状 =================

        /// <summary>水道方向（弧度）：从环礁中心指向主岛（世界原点）。</summary>
        static float PassAngle(Spec s) => Mathf.Atan2(-s.center.y, -s.center.x);

        /// <summary>环礁内侧长红树林的一段（弧度）：水道的对面偏一点。</summary>
        internal static float MangroveAngle(Spec s) => PassAngle(s) + Mathf.PI * 0.85f;

        static float AngleDiff(float a, float b) => Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, b * Mathf.Rad2Deg)) * Mathf.Deg2Rad;

        /// <summary>沿圆周的周期噪声（角度绕一圈首尾相接）。</summary>
        static float RingNoise(Spec s, float ang, float freq, float offset, int octaves = 3)
            => Fbm(Mathf.Cos(ang) * freq + s.seed * 0.013f + offset, Mathf.Sin(ang) * freq + s.seed * 0.007f + offset * 0.5f, octaves);

        /// <summary>海平面以上的高度（米）；lx, lz 是相对岛中心的坐标。</summary>
        public static float HeightAt(Spec s, float lx, float lz)
        {
            float r = Mathf.Sqrt(lx * lx + lz * lz);
            float ang = Mathf.Atan2(lz, lx);
            // 礁环半径：轻微的椭圆 + 沿圆周起伏
            float R = s.ringRadius * (1f + 0.08f * Mathf.Cos(2f * (ang - 0.6f)) + (RingNoise(s, ang, 1.4f, 3.1f) - 0.5f) * 0.22f);
            float d = r - R;            // > 0 礁环外侧，< 0 礁湖一侧
            float half = s.ringWidth * 0.5f;

            float h;
            if (d >= 0f) h = Mathf.Lerp(-0.6f, -19f, SmoothStep(half, half + 55f, d));       // 礁坪 → 外侧陡坡 → 外海
            else
            {
                h = Mathf.Lerp(-0.6f, -s.lagoonDepth, SmoothStep(half, half + 28f, -d));      // 礁坪 → 礁湖
                // 礁湖里零星的珊瑚丘
                float bommie = SmoothStep(0.68f, 0.8f, Mathf.PerlinNoise(lx * 0.04f + s.seed * 0.01f, lz * 0.04f + 5f));
                h = Mathf.Lerp(h, -1.3f, bommie * SmoothStep(half + 10f, half + 30f, -d));
                h += (Mathf.PerlinNoise(lx * 0.08f + 9f, lz * 0.08f + 2f) - 0.5f) * 0.6f;
            }

            // 沙洲：礁环中间露出水面的小岛，沿圆周断断续续
            float along = RingNoise(s, ang, 2.6f, 11.7f);
            float threshold = Mathf.Lerp(0.62f, 0.38f, s.motuCoverage);
            float motu = SmoothStep(threshold, threshold + 0.06f, along);
            float widthNoise = (Mathf.PerlinNoise(lx * 0.03f + 4f, lz * 0.03f + 1f) - 0.5f) * 8f;
            float across = 1f - SmoothStep(half * 0.35f, half + 3f + widthNoise, Mathf.Abs(d + 2f));
            float dune = 2.6f + (Mathf.PerlinNoise(lx * 0.05f + 2f, lz * 0.05f + 7f) - 0.3f) * 1.4f; // 最高 3~4 米
            float land = motu * across;
            h = Mathf.Lerp(h, Mathf.Max(h, dune), SmoothStep(0f, 1f, land));

            // 水道：朝主岛方向切开礁环，船能开进礁湖
            float chan = 1f - SmoothStep(s.passWidth * 0.5f * Mathf.Deg2Rad, s.passWidth * Mathf.Deg2Rad, AngleDiff(ang, PassAngle(s)));
            if (d > -half - 30f) h = Mathf.Lerp(h, Mathf.Min(h, -3.2f), chan);

            return Mathf.Clamp(h, MinHeight + 0.5f, MinHeight + HeightRange - 0.5f);
        }

        // ================= 地形 =================

        public static Result BuildTerrain(Spec s)
        {
            var data = new TerrainData { heightmapResolution = HeightRes, alphamapResolution = AlphaRes, baseMapResolution = 512 };
            data.size = new Vector3(s.size, HeightRange, s.size);
            IslandBuilder.SaveAsset(data, $"{IslandBuilder.Gen}/Terrain/{s.name}Terrain.asset"); // 先存资产再写数据（同主岛）
            data.SetDetailResolution(DetailRes, 32);

            var heights = new float[HeightRes, HeightRes];
            for (int zi = 0; zi < HeightRes; zi++)
                for (int xi = 0; xi < HeightRes; xi++)
                {
                    float lx = (xi / (HeightRes - 1f) - 0.5f) * s.size, lz = (zi / (HeightRes - 1f) - 0.5f) * s.size;
                    heights[zi, xi] = (HeightAt(s, lx, lz) - MinHeight) / HeightRange;
                }
            data.SetHeights(0, 0, heights);

            // 海洋着色器判断水深用的高度图
            const int hr = 257;
            var heightTex = new Texture2D(hr, hr, TextureFormat.RFloat, false, true) { wrapMode = TextureWrapMode.Clamp, name = s.name + "Height" };
            var px = new Color[hr * hr];
            for (int zi = 0; zi < hr; zi++)
                for (int xi = 0; xi < hr; xi++)
                    px[zi * hr + xi] = new Color(data.GetInterpolatedHeight(xi / (hr - 1f), zi / (hr - 1f)) / HeightRange, 0, 0, 0);
            heightTex.SetPixels(px);
            heightTex.Apply();
            IslandBuilder.SaveAsset(heightTex, $"{IslandBuilder.Gen}/Terrain/{s.name}Height.asset");

            data.terrainLayers = IslandBuilder.TerrainLayers();
            PaintSplat(s, data);
            EditorUtility.SetDirty(data);

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = s.name;
            go.transform.position = new Vector3(s.center.x - s.size / 2, MinHeight, s.center.y - s.size / 2);
            go.isStatic = true;
            var terrain = go.GetComponent<Terrain>();
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 4f;
            terrain.basemapDistance = 250f;
            terrain.detailObjectDistance = 90f;
            terrain.shadowCastingMode = ShadowCastingMode.TwoSided;
            var main = IslandBuilder.MainTerrain;
            if (main != null) terrain.materialTemplate = main.materialTemplate;
            Debug.Log($"[Atoll] 环礁地形：中心 {s.center}，礁环半径 {s.ringRadius} 米");

            return new Result
            {
                terrain = terrain,
                heightTex = heightTex,
                rect = new Vector4(s.center.x - s.size / 2, s.center.y - s.size / 2, s.size, s.size),
                heightRange = new Vector4(MinHeight, HeightRange, 0, 0),
            };
        }

        static void PaintSplat(Spec s, TerrainData data)
        {
            int res = data.alphamapResolution, n = IslandBuilder.TerrainLayers().Length;
            var maps = new float[res, res, n];
            var w = new float[n];
            float mangroveAng = MangroveAngle(s);
            for (int zi = 0; zi < res; zi++)
                for (int xi = 0; xi < res; xi++)
                {
                    float u = xi / (res - 1f), v = zi / (res - 1f);
                    float lx = (u - 0.5f) * s.size, lz = (v - 0.5f) * s.size;
                    float h = data.GetInterpolatedHeight(u, v) + MinHeight;
                    float noise = Mathf.PerlinNoise(lx * 0.07f + 50f, lz * 0.07f + 20f) - 0.5f;
                    float r = Mathf.Sqrt(lx * lx + lz * lz);
                    for (int l = 0; l < n; l++) w[l] = 0f;

                    float wet = 1f - SmoothStep(-0.3f, 0.8f, h + noise * 0.5f);
                    float veg = SmoothStep(2.1f, 2.8f, h + noise * 1.2f);
                    w[0] = wet;                                          // 湿沙
                    w[1] = (1f - wet) * (1f - veg);                      // 干沙（白沙）
                    float shade = SmoothStep(-0.05f, 0.2f, noise);       // 沙洲上：草地里夹着椰林下的落叶
                    w[2] = veg * (1f - shade);
                    w[3] = veg * shade;

                    // 礁坪：退潮时露出的礁石滩（礁环外侧，水下 0~2 米）
                    float reefFlat = SmoothStep(-2.2f, -0.9f, h) * (1f - SmoothStep(-0.2f, 0.4f, h)) * SmoothStep(s.ringRadius * 0.85f, s.ringRadius, r);
                    w[9] = reefFlat * 1.4f;

                    // 红树林：礁湖内侧的浅滩是泥地
                    float mangrove = 1f - SmoothStep(0.35f, 0.6f, AngleDiff(Mathf.Atan2(lz, lx), mangroveAng));
                    float mud = mangrove * (1f - SmoothStep(s.ringRadius * 0.85f, s.ringRadius, r)) * SmoothStep(-1.6f, -0.6f, h) * (1f - SmoothStep(1.6f, 2.4f, h));
                    w[5] = mud * 1.8f;
                    for (int l = 0; l <= 3; l++) w[l] *= 1f - mud * 0.9f;

                    // 水下：礁湖是白沙，珊瑚丘和外坡是礁石
                    float uw = 1f - SmoothStep(-1.6f, -0.7f, h);
                    if (uw > 0f)
                    {
                        float reef = SmoothStep(0.62f, 0.78f, Mathf.PerlinNoise(lx * 0.04f + s.seed * 0.01f, lz * 0.04f + 5f)) + SmoothStep(-6f, -12f, h);
                        for (int l = 0; l < n; l++) w[l] *= 1f - uw;
                        w[1] += uw * (1f - Mathf.Clamp01(reef));
                        w[9] += uw * Mathf.Clamp01(reef) * 1.2f;
                    }

                    float sum = 0f;
                    for (int l = 0; l < n; l++) sum += w[l];
                    for (int l = 0; l < n; l++) maps[zi, xi, l] = w[l] / Mathf.Max(sum, 1e-4f);
                }
            data.SetAlphamaps(0, 0, maps);
        }

        // ================= 植被 =================

        /// <summary>沙洲上的椰子树、草丛和灌木，礁湖内侧的红树林。地表植物沿用主岛已经建好的细节原型。</summary>
        public static void BuildNature(Spec s, Terrain mainTerrain)
        {
            var terrain = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None).FirstOrDefault(t => t.name == s.name);
            if (terrain == null) { Debug.LogWarning($"[Atoll] 场景里没有 {s.name} 地形，跳过植被"); return; }
            var root = new GameObject(s.name + "Nature").transform;
            var rng = new System.Random(s.seed);
            float R() => (float)rng.NextDouble();
            float H(float x, float z) => terrain.SampleHeight(new Vector3(x, 0, z)) + terrain.transform.position.y;
            var c = new Vector3(s.center.x, 0, s.center.y);

            // 椰子树：沙洲上成片，向海倾斜
            var palms = new GameObject("Palms").transform;
            palms.SetParent(root, false);
            var palmPrefabs = Enumerable.Range(0, 6).Select(JunglePlants.Palm).ToArray();
            var placed = new List<Vector3>();
            for (int i = 0; i < 20000 && placed.Count < 120; i++)
            {
                float a = R() * Mathf.PI * 2f, r = s.ringRadius * (0.7f + R() * 0.55f);
                float x = c.x + Mathf.Cos(a) * r, z = c.z + Mathf.Sin(a) * r;
                float h = H(x, z);
                if (h < 1.5f) continue;
                var p = new Vector3(x, h, z);
                if (placed.Any(q => (q - p).sqrMagnitude < 4.2f * 4.2f)) continue;
                placed.Add(p);
                float hx = H(x + 2, z) - H(x - 2, z), hz = H(x, z + 2) - H(x, z - 2);
                var down = new Vector3(-hx, 0, -hz);
                float yaw = down.sqrMagnitude > 1e-4f ? Mathf.Atan2(down.x, down.z) * Mathf.Rad2Deg + (R() - 0.5f) * 70f : R() * 360f;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(palmPrefabs[rng.Next(palmPrefabs.Length)], palms);
                go.transform.SetPositionAndRotation(p - Vector3.up * 0.1f, Quaternion.Euler(0, yaw, 0));
                go.transform.localScale = Vector3.one * (0.85f + R() * 0.3f);
                go.isStatic = true;
            }

            // 红树林：礁湖内侧泥滩，气根立在浅水里
            var mangroves = new GameObject("Mangroves").transform;
            mangroves.SetParent(root, false);
            var crown = AssetDatabase.LoadAssetAtPath<GameObject>($"{IslandBuilder.Gen}/Prefabs/island_tree_02.prefab") ?? IslandBuilder.TreePrefab("island_tree_02");
            var rootMeshes = Enumerable.Range(0, 3).Select(JunglePlants.MangroveRoots).ToArray();
            var bark = JunglePlants.BarkMaterial();
            float mAng = MangroveAngle(s);
            var mPlaced = new List<Vector3>();
            for (int i = 0; i < 8000 && mPlaced.Count < 30; i++)
            {
                float a = mAng + (R() - 0.5f) * 0.9f, r = s.ringRadius * (0.72f + R() * 0.25f);
                float x = c.x + Mathf.Cos(a) * r, z = c.z + Mathf.Sin(a) * r;
                float h = H(x, z);
                if (h < -1.0f || h > 1.8f) continue;
                var p = new Vector3(x, h, z);
                if (mPlaced.Any(q => (q - p).sqrMagnitude < 6.5f * 6.5f)) continue;
                mPlaced.Add(p);
                float sc = 1.1f + R() * 0.5f;
                var tree = new GameObject("Mangrove").transform;
                tree.SetParent(mangroves);
                tree.SetPositionAndRotation(p, Quaternion.Euler(0, R() * 360f, 0));
                var cr = (GameObject)PrefabUtility.InstantiatePrefab(crown, tree);
                cr.transform.localPosition = Vector3.up * 1.4f * sc;
                cr.transform.localScale = Vector3.one * sc;
                var roots = new GameObject("Roots");
                roots.transform.SetParent(tree, false);
                roots.transform.localScale = Vector3.one * sc;
                roots.AddComponent<MeshFilter>().sharedMesh = rootMeshes[mPlaced.Count % rootMeshes.Length];
                roots.AddComponent<MeshRenderer>().sharedMaterial = bark;
                var col = tree.gameObject.AddComponent<CapsuleCollider>();
                col.radius = 0.9f * sc;
                col.height = 2.4f * sc;
                col.center = Vector3.up * 1.2f * sc;
                tree.gameObject.AddComponent<SurfaceTag>().surface = "wood";
                foreach (var t in tree.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
            }

            PaintDetails(terrain, mainTerrain);
            Debug.Log($"[Atoll] 椰子树 {placed.Count} 棵，红树林 {mPlaced.Count} 棵");
        }

        /// <summary>草丛、蕨和灌木：复用主岛的细节原型，按名字挑出草和灌木两类。</summary>
        static void PaintDetails(Terrain terrain, Terrain mainTerrain)
        {
            if (mainTerrain == null) return;
            var data = terrain.terrainData;
            var protos = mainTerrain.terrainData.detailPrototypes;
            data.detailPrototypes = protos;
            int Kind(DetailPrototype p)
            {
                string name = p.prototype != null ? p.prototype.name.ToLowerInvariant() : "";
                if (name.Contains("grass")) return 0;
                if (name.Contains("fern") || name.Contains("bush") || name.Contains("shrub")) return 1;
                return -1;
            }
            int res = data.detailResolution, ar = data.alphamapResolution;
            var alpha = data.GetAlphamaps(0, 0, ar, ar);
            var rng = new System.Random(7);
            for (int p = 0; p < protos.Length; p++)
            {
                int kind = Kind(protos[p]);
                var layer = new int[res, res];
                if (kind >= 0)
                    for (int zi = 0; zi < res; zi++)
                        for (int xi = 0; xi < res; xi++)
                        {
                            int ax = xi * (ar - 1) / (res - 1), az = zi * (ar - 1) / (res - 1);
                            float clump = Mathf.PerlinNoise(xi * 0.06f + p, zi * 0.06f);
                            float d = kind == 0
                                ? alpha[az, ax, 2] * (0.2f + clump) * 0.35f + alpha[az, ax, 3] * 0.04f
                                : alpha[az, ax, 3] * SmoothStep(0.45f, 0.75f, clump) * 0.25f;
                            layer[zi, xi] = (int)d + (rng.NextDouble() < d % 1 ? 1 : 0);
                        }
                data.SetDetailLayer(0, 0, p, layer);
            }
            EditorUtility.SetDirty(data);
        }

        // ================= 工具 =================

        static float SmoothStep(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3 - 2 * t);
        }

        static float Fbm(float x, float y, int octaves)
        {
            float s = 0, a = 0.5f, norm = 0;
            for (int i = 0; i < octaves; i++)
            {
                s += a * Mathf.PerlinNoise(x, y);
                norm += a;
                x = x * 2.03f + 17.1f;
                y = y * 2.03f + 9.7f;
                a *= 0.5f;
            }
            return s / norm;
        }
    }
}
