using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Rishi.Core;
using Rishi.Inventory;
using Rishi.Story;
using Rishi.Wildlife;

namespace Rishi.Builders
{
    /// <summary>
    /// 海底世界：海床起伏（沙波、礁盘、外缘陡坡）、海草床、海带林、珊瑚礁（鹿角、脑、桌、扇珊瑚、管状海绵、海葵）、
    /// 海胆、海星、可拾取的贝壳、能开出珍珠的巨型砗磲、海底石碑与父亲的潜水写字板、深处的小沉船，以及各种鱼。
    /// 还负责水下画面需要的资源：后处理、焦散贴图、悬浮颗粒、呼气气泡。
    /// </summary>
    public static class MarineBuilder
    {
        const string Dir = IslandBuilder.Gen + "/Marine";
        public const int MarineLayer = 9;
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

        // ================= 海床形状（IslandBuilder 生成高度图时调用） =================

        /// <summary>礁盘分布（0~1）。</summary>
        public static float ReefMask(float x, float z)
        {
            float n = Mathf.PerlinNoise(x * 0.017f + 31f, z * 0.017f + 17f) * 0.7f + Mathf.PerlinNoise(x * 0.05f + 3f, z * 0.05f + 9f) * 0.3f;
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.54f, 0.66f, n));
        }

        public static float MeadowMask(float x, float z) => Mathf.InverseLerp(0.45f, 0.62f, Mathf.PerlinNoise(x * 0.022f + 71f, z * 0.022f + 5f));
        public static float KelpMask(float x, float z) => Mathf.InverseLerp(0.5f, 0.66f, Mathf.PerlinNoise(x * 0.012f + 140f, z * 0.012f + 60f));

        /// <summary>给水下部分加上沙波、礁盘和外缘陡坡。h 是原始高度（海平面 0）。</summary>
        public static float ShapeSeabed(float x, float z, float h)
        {
            float under = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-2.8f, -0.8f, h));
            if (under <= 0f) return h;
            float dunes = (Mathf.PerlinNoise(x * 0.04f + 5f, z * 0.04f + 9f) - 0.5f) * 1.8f
                        + Mathf.Sin((x * 0.8f + z * 0.6f) * 0.32f + Mathf.PerlinNoise(x * 0.01f, z * 0.01f) * 6f) * 0.15f;
            float reef = ReefMask(x, z);
            float bump = reef * (2.0f + (Mathf.PerlinNoise(x * 0.22f + 1f, z * 0.22f + 2f) - 0.5f) * 2.6f + (Mathf.PerlinNoise(x * 0.8f, z * 0.8f) - 0.5f) * 0.7f);
            float r = Mathf.Sqrt(x * x + z * z);
            float drop = -1.6f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(250f, 292f, r));
            float result = h + under * (dunes + bump + drop);
            // 礁盘不能冒出水面
            if (h < -2f) result = Mathf.Min(result, -1.4f);
            return result;
        }

        // ================= 场景 =================

        public static void Build(Terrain terrain, Vector3 spawn)
        {
            Directory.CreateDirectory(Dir);
            EnsureLayer();
            mats.Clear();
            var root = new GameObject("Seabed").transform;
            var rnd = new System.Random(4242);
            float R() => (float)rnd.NextDouble();

            // ---------- 网格变体 ----------
            var seagrass = new Mesh[4];
            for (int i = 0; i < seagrass.Length; i++) seagrass[i] = SaveMesh(MarineMeshes.SeagrassPatch("Seagrass_" + i, 100 + i, 2.8f, 300));
            var kelpHeights = new[] { 5f, 7f, 9f, 11f, 13f, 15f };
            var kelp = new Mesh[kelpHeights.Length * 2];
            for (int i = 0; i < kelp.Length; i++) kelp[i] = SaveMesh(MarineMeshes.Kelp("Kelp_" + i, 200 + i, kelpHeights[i % kelpHeights.Length]));
            var staghornColors = new[] { (new Color(0.72f, 0.62f, 0.45f), new Color(0.75f, 0.7f, 0.95f)), (new Color(0.55f, 0.45f, 0.5f), new Color(0.85f, 0.55f, 0.8f)), (new Color(0.62f, 0.58f, 0.4f), new Color(0.6f, 0.85f, 0.9f)) };
            var staghorn = new Mesh[6];
            for (int i = 0; i < staghorn.Length; i++) { var (b, t) = staghornColors[i % 3]; staghorn[i] = SaveMesh(MarineMeshes.Staghorn("Staghorn_" + i, 300 + i, b, t)); }
            var brainColors = new[] { (new Color(0.66f, 0.58f, 0.34f), new Color(0.34f, 0.3f, 0.16f)), (new Color(0.48f, 0.56f, 0.36f), new Color(0.24f, 0.3f, 0.18f)), (new Color(0.62f, 0.5f, 0.42f), new Color(0.38f, 0.26f, 0.22f)) };
            var brain = new Mesh[3];
            for (int i = 0; i < brain.Length; i++) { var (a, g) = brainColors[i]; brain[i] = SaveMesh(MarineMeshes.BrainCoral("Brain_" + i, i, 0.5f, a, g)); }
            var table = new Mesh[3];
            for (int i = 0; i < table.Length; i++) table[i] = SaveMesh(MarineMeshes.TableCoral("Table_" + i, 400 + i, Mathf.Lerp(0.6f, 0.95f, i / 2f), new Color(0.46f, 0.5f, 0.52f), new Color(0.75f, 0.78f, 0.82f)));
            var fanColors = new[] { new Color(0.55f, 0.12f, 0.35f), new Color(0.8f, 0.45f, 0.12f), new Color(0.45f, 0.18f, 0.6f) };
            var fan = new Mesh[3];
            for (int i = 0; i < fan.Length; i++) fan[i] = SaveMesh(MarineMeshes.FanCoral("Fan_" + i, 500 + i, Mathf.Lerp(0.7f, 1.1f, i / 2f), fanColors[i]));
            var spongeColors = new[] { new Color(0.85f, 0.42f, 0.12f), new Color(0.48f, 0.22f, 0.58f), new Color(0.75f, 0.68f, 0.2f) };
            var sponge = new Mesh[3];
            for (int i = 0; i < sponge.Length; i++) sponge[i] = SaveMesh(MarineMeshes.TubeSponge("Sponge_" + i, 600 + i, spongeColors[i]));
            var anemone = new[]
            {
                SaveMesh(MarineMeshes.Anemone("Anemone_0", 700, new Color(0.82f, 0.62f, 0.5f), new Color(0.95f, 0.55f, 0.85f))),
                SaveMesh(MarineMeshes.Anemone("Anemone_1", 701, new Color(0.55f, 0.75f, 0.45f), new Color(0.85f, 0.95f, 0.5f))),
            };
            var urchin = SaveMesh(MarineMeshes.Urchin("Urchin", 800));
            var starfish = new[] { SaveMesh(MarineMeshes.Starfish("Starfish_0", 900, 0.14f, new Color(0.85f, 0.32f, 0.12f))), SaveMesh(MarineMeshes.Starfish("Starfish_1", 901, 0.16f, new Color(0.15f, 0.32f, 0.72f))) };
            var scallop = SaveMesh(MarineMeshes.Scallop("Scallop", new Color(0.92f, 0.62f, 0.5f)));
            var conch = SaveMesh(MarineMeshes.Conch("Conch"));
            var clamBottom = SaveMesh(MarineMeshes.ClamHalf("ClamBottom", 0.42f, 0.5f, false, new Color(0.72f, 0.7f, 0.6f), new Color(0.1f, 0.35f, 0.6f)));
            var clamTop = SaveMesh(MarineMeshes.ClamHalf("ClamTop", 0.42f, 0.5f, true, new Color(0.72f, 0.7f, 0.6f), new Color(0.1f, 0.35f, 0.6f)));

            // ---------- 材质 ----------
            var mSeagrass = Mat("Seagrass", sway: 0.16f, swaySpeed: 1.1f, translucency: 0.35f, wrap: 0.6f, smooth: 0.15f);
            var mKelp = Mat("Kelp", sway: 0.55f, swaySpeed: 0.55f, translucency: 1.1f, wrap: 0.6f, smooth: 0.3f);
            var mCoral = Mat("Coral", wrap: 0.35f, smooth: 0.12f);
            var mFan = Mat("FanCoral", sway: 0.05f, swaySpeed: 0.9f, translucency: 0.5f, wrap: 0.5f, smooth: 0.1f);
            var mAnemone = Mat("Anemone", sway: 0.06f, swaySpeed: 1.6f, translucency: 1.0f, wrap: 0.5f, smooth: 0.5f);
            var mShell = Mat("Shell", wrap: 0.25f, smooth: 0.55f, sheen: 0.3f);
            var mClam = Mat("Clam", wrap: 0.3f, smooth: 0.45f, sheen: 0.2f);

            Transform Group(string n) { var g = new GameObject(n).transform; g.SetParent(root); return g; }
            var gGrass = Group("SeagrassMeadows");
            var gKelp = Group("KelpForest");
            var gReef = Group("CoralReef");
            var gLife = Group("SeabedLife");
            var gPick = Group("Shells");

            int nGrass = 0, nKelp = 0, nCoral = 0, nUrchin = 0, nStar = 0, nShell = 0, nClam = 0;
            var shellItem = GameplaySetup.Item("shell");
            var pearlItem = GameplaySetup.Item("pearl");
            float half = IslandBuilder.Size / 2f - 8f;
            for (int attempt = 0; attempt < 90000; attempt++)
            {
                float x = Mathf.Lerp(-half, half, R()), z = Mathf.Lerp(-half, half, R());
                if (!IslandBuilder.Sample(terrain, x, z, out float h, out float slope, out _)) continue;
                float depth = -h;
                if (depth < 1.2f) continue;
                float reef = ReefMask(x, z);
                var pos = new Vector3(x, h, z);
                float yaw = R() * 360f;

                if (reef > 0.5f && depth > 2.4f && depth < 15f && nCoral < 2600)
                {
                  int cluster = 3 + rnd.Next(4);
                  for (int ci = 0; ci < cluster; ci++)
                  {
                    if (ci > 0)
                    {
                        float ca = R() * Mathf.PI * 2f, cr = Mathf.Lerp(0.5f, 2.2f, R());
                        float cx = x + Mathf.Cos(ca) * cr, cz = z + Mathf.Sin(ca) * cr;
                        if (!IslandBuilder.Sample(terrain, cx, cz, out float ch, out _, out _) || -ch < 2f) continue;
                        pos = new Vector3(cx, ch, cz);
                        yaw = R() * 360f;
                    }
                    float pick = R();
                    if (pick < 0.3f) Place(gReef, staghorn[rnd.Next(staghorn.Length)], mCoral, pos - Vector3.up * 0.05f, yaw, Mathf.Lerp(0.8f, 1.8f, R()), false);
                    else if (pick < 0.48f) Place(gReef, brain[rnd.Next(brain.Length)], mCoral, pos - Vector3.up * 0.12f, yaw, Mathf.Lerp(0.6f, 1.8f, R()), true);
                    else if (pick < 0.6f) Place(gReef, table[rnd.Next(table.Length)], mCoral, pos - Vector3.up * 0.05f, yaw, Mathf.Lerp(0.8f, 1.5f, R()), true);
                    else if (pick < 0.73f) Place(gReef, fan[rnd.Next(fan.Length)], mFan, pos - Vector3.up * 0.05f, yaw, Mathf.Lerp(0.8f, 1.4f, R()), false);
                    else if (pick < 0.86f) Place(gReef, sponge[rnd.Next(sponge.Length)], mCoral, pos - Vector3.up * 0.03f, yaw, Mathf.Lerp(0.8f, 1.6f, R()), false);
                    else if (pick < 0.94f) Place(gReef, anemone[rnd.Next(anemone.Length)], mAnemone, pos - Vector3.up * 0.02f, yaw, Mathf.Lerp(0.8f, 1.5f, R()), false);
                    else if (nUrchin < 160) { Place(gLife, urchin, mCoral, pos, yaw, Mathf.Lerp(0.7f, 1.2f, R()), false); nUrchin++; }
                    nCoral++;
                  }
                    // 礁盘上的巨型砗磲：打开能拿到珍珠
                    if (nClam < 14 && R() < 0.03f && depth > 3f && depth < 13f) { Clam(gReef, pos, yaw, clamBottom, clamTop, mClam, pearlItem, nClam); nClam++; }
                }
                else if (reef < 0.3f && depth < 8f && depth > 1.2f && MeadowMask(x, z) > 0.4f && nGrass < 1800)
                {
                    Place(gGrass, seagrass[rnd.Next(seagrass.Length)], mSeagrass, pos - Vector3.up * 0.03f, yaw, Mathf.Lerp(0.8f, 1.3f, R()), false, shadows: false);
                    nGrass++;
                }
                else if (reef < 0.4f && depth > 7f && depth < 19f && KelpMask(x, z) > 0.45f && nKelp < 900)
                {
                    // 海带长到离水面一两米
                    int hi = 0;
                    for (int k = 0; k < kelpHeights.Length; k++) if (kelpHeights[k] < depth - 1.2f) hi = k;
                    int variant = Mathf.Clamp(hi - rnd.Next(2), 0, kelpHeights.Length - 1) + (R() < 0.5f ? 0 : kelpHeights.Length);
                    int stalks = 5 + rnd.Next(7);
                    for (int k = 0; k < stalks; k++)
                    {
                        float ka = R() * Mathf.PI * 2f, kr = k == 0 ? 0f : Mathf.Lerp(0.6f, 3.5f, R());
                        float kx = x + Mathf.Cos(ka) * kr, kz = z + Mathf.Sin(ka) * kr;
                        if (!IslandBuilder.Sample(terrain, kx, kz, out float kh, out _, out _) || -kh < 6f) continue;
                        int kv = Mathf.Clamp(variant % kelpHeights.Length - rnd.Next(2), 0, kelpHeights.Length - 1) + (R() < 0.5f ? 0 : kelpHeights.Length);
                        if (kelpHeights[kv % kelpHeights.Length] > -kh - 1f) kv = 0;
                        Place(gKelp, kelp[kv], mKelp, new Vector3(kx, kh - 0.1f, kz), R() * 360f, Mathf.Lerp(0.9f, 1.1f, R()), false);
                        nKelp++;
                    }
                }
                else if (reef < 0.5f && depth < 12f && R() < 0.03f)
                {
                    if (nStar < 80 && R() < 0.6f) { Place(gLife, starfish[rnd.Next(2)], mShell, pos + Vector3.up * 0.005f, yaw, Mathf.Lerp(0.8f, 1.3f, R()), false, shadows: false); nStar++; }
                    else if (nShell < 36)
                    {
                        var go = Place(gPick, R() < 0.5f ? scallop : conch, mShell, pos + Vector3.up * 0.01f, yaw, Mathf.Lerp(1f, 1.4f, R()), false, shadows: false);
                        go.name = "Shell";
                        var col = go.AddComponent<SphereCollider>();
                        col.isTrigger = true;
                        col.radius = 0.15f;
                        var p = go.AddComponent<ItemPickup>();
                        p.item = shellItem;
                        p.verb = "捡起";
                        nShell++;
                    }
                }
            }
            Debug.Log($"[Marine] seagrass {nGrass}, kelp {nKelp}, coral {nCoral}, urchin {nUrchin}, starfish {nStar}, shells {nShell}, clams {nClam}");

            PlaceReefRocks(terrain, gReef, rnd);
            BuildTabletSite(terrain, spawn, root);
            BuildSunkenBoat(terrain, spawn, root);
            BuildFish();
        }

        static GameObject Place(Transform parent, Mesh mesh, Material mat, Vector3 pos, float yaw, float scale, bool collider, bool shadows = true)
        {
            var go = new GameObject(mesh.name);
            go.transform.SetParent(parent);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            go.transform.localScale = Vector3.one * scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            go.layer = MarineLayer;
            if (collider)
            {
                var c = go.AddComponent<MeshCollider>();
                c.sharedMesh = mesh;
            }
            return go;
        }

        static void Clam(Transform parent, Vector3 pos, float yaw, Mesh bottom, Mesh top, Material mat, ItemDef pearl, int index)
        {
            var go = new GameObject("GiantClam");
            go.transform.SetParent(parent);
            go.transform.SetPositionAndRotation(pos + Vector3.up * 0.12f, Quaternion.Euler(0, yaw, 0));
            go.layer = MarineLayer;
            var b = new GameObject("Bottom");
            b.transform.SetParent(go.transform, false);
            b.AddComponent<MeshFilter>().sharedMesh = bottom;
            b.AddComponent<MeshRenderer>().sharedMaterial = mat;
            // 盖子绕后面的铰链转动
            var hinge = new GameObject("Hinge").transform;
            hinge.SetParent(go.transform, false);
            hinge.localPosition = new Vector3(0, 0, -0.25f);
            hinge.localRotation = Quaternion.Euler(-12f, 0, 0); // 微微张开，露出蓝色外套膜
            var t = new GameObject("Top");
            t.transform.SetParent(hinge, false);
            t.transform.localPosition = new Vector3(0, 0, 0.25f);
            t.AddComponent<MeshFilter>().sharedMesh = top;
            t.AddComponent<MeshRenderer>().sharedMaterial = mat;
            foreach (Transform c in go.GetComponentsInChildren<Transform>()) c.gameObject.layer = MarineLayer;
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(0.9f, 0.35f, 0.55f);
            var loot = go.AddComponent<LootContainer>();
            loot.displayName = "巨型砗磲";
            loot.verb = "撬开";
            loot.lid = hinge;
            loot.lidOpenEuler = new Vector3(-40f, 0, 0);
            loot.openSound = "clam_open";
            loot.contents.Add(new PlayerInventory.Stack { item = pearl, count = index % 4 == 0 ? 2 : 1 });
        }

        static void PlaceReefRocks(Terrain terrain, Transform parent, System.Random rnd)
        {
            var mat = IslandBuilder.PbrPrefix("rock_moss_set_01", "Models/rock_moss_set_01", "rock_moss_set_01_", false);
            var model = FPSSceneBuilder.LoadModel("Models/rock_moss_set_01");
            if (model == null) return;
            var rocks = model.GetComponentsInChildren<MeshFilter>();
            int count = 0;
            float half = IslandBuilder.Size / 2f - 10f;
            for (int a = 0; a < 20000 && count < 140; a++)
            {
                float x = Mathf.Lerp(-half, half, (float)rnd.NextDouble()), z = Mathf.Lerp(-half, half, (float)rnd.NextDouble());
                if (!IslandBuilder.Sample(terrain, x, z, out float h, out _, out _) || h > -2.5f) continue;
                if (ReefMask(x, z) < 0.35f && rnd.NextDouble() > 0.08) continue;
                var src = rocks[rnd.Next(rocks.Length)];
                var go = new GameObject("ReefRock");
                go.transform.SetParent(parent);
                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = src.sharedMesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;
                go.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                go.transform.rotation = Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 0) * src.transform.rotation;
                go.transform.localScale = src.transform.lossyScale * Mathf.Lerp(0.5f, 1.7f, (float)rnd.NextDouble());
                go.transform.position = Vector3.zero;
                var b = go.GetComponent<Renderer>().bounds;
                go.transform.position = new Vector3(x, h, z) - new Vector3(b.center.x, b.min.y + b.size.y * 0.35f, b.center.z);
                go.layer = MarineLayer;
                go.isStatic = true;
                count++;
            }
        }

        /// <summary>在出生点外海找一块指定深度的海床。</summary>
        static Vector3 FindOffshore(Terrain terrain, Vector3 spawn, float minDepth, float maxDepth, float angleOffset, bool wantReef)
        {
            var outward = new Vector3(spawn.x, 0, spawn.z).normalized;
            Vector3 best = spawn;
            float bestScore = float.MaxValue;
            for (float ang = -70f; ang <= 70f; ang += 4f)
                for (float d = 10f; d < 200f; d += 2f)
                {
                    var dir = Quaternion.Euler(0, ang + angleOffset, 0) * outward;
                    var p = spawn + dir * d;
                    if (!IslandBuilder.Sample(terrain, p.x, p.z, out float h, out float slope, out _)) continue;
                    float depth = -h;
                    if (depth < minDepth || depth > maxDepth || slope > 25f) continue;
                    float score = d + Mathf.Abs(ang) * 0.4f + (wantReef ? (1f - ReefMask(p.x, p.z)) * 20f : ReefMask(p.x, p.z) * 30f);
                    if (score < bestScore) { bestScore = score; best = new Vector3(p.x, h, p.z); }
                }
            if (bestScore == float.MaxValue) Debug.LogWarning($"[Marine] 在 {spawn} 外海没找到 {minDepth}~{maxDepth} 米深的海床");
            else Debug.Log($"[Marine] 外海 {minDepth}~{maxDepth} 米：{best}");
            return best;
        }

        static void BuildTabletSite(Terrain terrain, Vector3 spawn, Transform root)
        {
            var pos = FindOffshore(terrain, spawn, 7.5f, 11f, 18f, true);
            var site = new GameObject("SeaTabletSite").transform;
            site.SetParent(root);
            site.position = pos;
            float yaw = Mathf.Atan2(spawn.x - pos.x, spawn.z - pos.z) * Mathf.Rad2Deg; // 刻面朝向海岸
            var stone = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Generated/Materials/RuinStone.mat")
                        ?? FPSSceneBuilder.Painted("RuinStone", new Color(0.55f, 0.52f, 0.45f), 0f, 0.2f);
            var tablet = new GameObject("SeaTablet");
            tablet.transform.SetParent(site);
            tablet.transform.SetPositionAndRotation(pos + Vector3.up * 0.6f, Quaternion.Euler(-14f, yaw, 6f));
            FPSSceneBuilder.Part("Slab", tablet.transform, new Vector3(0, 0.3f, 0), new Vector3(1.3f, 2.1f, 0.32f), stone, true);
            FPSSceneBuilder.Part("Cap", tablet.transform, new Vector3(0, 1.42f, 0), new Vector3(1.45f, 0.2f, 0.4f), stone, false);
            // 刻痕：发光的阿玛拉符号（暗金色，水下很显眼）
            var glyph = FPSSceneBuilder.Painted("SeaGlyph", new Color(0.35f, 0.3f, 0.18f), 0f, 0.3f);
            glyph.EnableKeyword("_EMISSION");
            glyph.SetColor("_EmissionColor", new Color(0.55f, 0.42f, 0.12f) * 0.8f);
            for (int i = 0; i < 5; i++)
                FPSSceneBuilder.Part("Glyph" + i, tablet.transform, new Vector3((i - 2) * 0.22f, 0.9f - (i % 2) * 0.25f, 0.165f), new Vector3(0.12f, 0.12f, 0.01f), glyph, false);
            FPSSceneBuilder.Part("Sun", tablet.transform, new Vector3(0, 0.05f, 0.165f), new Vector3(0.36f, 0.36f, 0.01f), glyph, false);
            foreach (var t in tablet.GetComponentsInChildren<Transform>()) t.gameObject.layer = MarineLayer;
            var note = tablet.AddComponent<NoteItem>();
            note.verb = "查看";
            note.note = new NoteData { title = "海底石碑", body = SeaTabletText };
            note.objectiveStage = 0;
            var glow = new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(tablet.transform, false);
            glow.transform.localPosition = new Vector3(0, 0.6f, 1.2f);
            glow.type = LightType.Point;
            glow.range = 4f;
            glow.intensity = 0.8f;
            glow.color = new Color(0.9f, 0.7f, 0.35f);
            glow.shadows = LightShadows.None;

            // 父亲的潜水写字板，卡在石碑脚下
            var slateMat = FPSSceneBuilder.Painted("DiveSlate", new Color(0.88f, 0.9f, 0.86f), 0f, 0.4f);
            var slate = new GameObject("DiveSlate");
            slate.transform.SetParent(site);
            var sp = pos + Quaternion.Euler(0, yaw, 0) * new Vector3(0.9f, 0, 0.9f);
            sp.y = terrain.SampleHeight(sp) + terrain.transform.position.y + 0.04f;
            slate.transform.SetPositionAndRotation(sp, Quaternion.Euler(8f, yaw + 30f, -4f));
            FPSSceneBuilder.Part("Board", slate.transform, Vector3.zero, new Vector3(0.24f, 0.012f, 0.32f), slateMat, true);
            var pencil = FPSSceneBuilder.Painted("SlatePencil", new Color(0.15f, 0.15f, 0.15f), 0f, 0.3f);
            FPSSceneBuilder.Part("Pencil", slate.transform, new Vector3(0.14f, 0.01f, 0.05f), new Vector3(0.01f, 0.01f, 0.14f), pencil, false);
            var sn = slate.AddComponent<NoteItem>();
            sn.verb = "阅读";
            sn.note = new NoteData { title = "父亲的潜水写字板", body = DiveSlateText };
            sn.hideOnRead = true;
            IslandStoryBuilder.NoteGlow(slate.transform);
            foreach (var t in slate.GetComponentsInChildren<Transform>()) t.gameObject.layer = MarineLayer;
        }

        static void BuildSunkenBoat(Terrain terrain, Vector3 spawn, Transform root)
        {
            var pos = FindOffshore(terrain, spawn, 13f, 18.5f, -35f, false);
            var site = new GameObject("SunkenBoat").transform;
            site.SetParent(root);
            site.position = pos;
            // 复制岸上沉船的船体（去掉剧情交互），缩小、侧翻，长满藻类
            var source = GameObject.Find("Shipwreck");
            if (source != null)
            {
                var boat = Object.Instantiate(source, site);
                boat.name = "SunkenHull";
                foreach (var mb in boat.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(mb);
                foreach (var l in boat.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l.gameObject);
                foreach (var a in boat.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(a);
                foreach (var z in boat.GetComponentsInChildren<AudioReverbZone>(true)) Object.DestroyImmediate(z);
                boat.transform.localScale = source.transform.localScale * 0.6f;
                boat.transform.rotation = Quaternion.Euler(8f, Random.Range(0f, 360f), 28f);
                boat.transform.position = pos;
                var bounds = FPSSceneBuilder.Bounds(boat);
                boat.transform.position += Vector3.up * (pos.y - bounds.min.y - bounds.size.y * 0.3f);
                var algae = new MaterialPropertyBlock();
                algae.SetColor("_BaseColor", new Color(0.42f, 0.52f, 0.42f));
                foreach (var r in boat.GetComponentsInChildren<Renderer>()) r.SetPropertyBlock(algae);
            }

            // 船边半埋的箱子
            var wood = FPSSceneBuilder.Painted("SunkenChestWood", new Color(0.28f, 0.24f, 0.16f), 0f, 0.15f);
            var iron = FPSSceneBuilder.Painted("SunkenChestIron", new Color(0.22f, 0.2f, 0.18f), 0.7f, 0.25f);
            var chest = new GameObject("SunkenChest");
            chest.transform.SetParent(site);
            var cp = pos + new Vector3(2.2f, 0, 1.4f);
            cp.y = terrain.SampleHeight(cp) + terrain.transform.position.y - 0.08f;
            chest.transform.SetPositionAndRotation(cp, Quaternion.Euler(4f, 35f, -6f));
            FPSSceneBuilder.Part("Body", chest.transform, new Vector3(0, 0.18f, 0), new Vector3(0.8f, 0.36f, 0.5f), wood, true);
            FPSSceneBuilder.Part("BandL", chest.transform, new Vector3(-0.3f, 0.18f, 0), new Vector3(0.05f, 0.37f, 0.51f), iron, false);
            FPSSceneBuilder.Part("BandR", chest.transform, new Vector3(0.3f, 0.18f, 0), new Vector3(0.05f, 0.37f, 0.51f), iron, false);
            var lid = new GameObject("Lid").transform;
            lid.SetParent(chest.transform, false);
            lid.localPosition = new Vector3(0, 0.36f, -0.25f);
            FPSSceneBuilder.Part("LidBoard", lid, new Vector3(0, 0.06f, 0.25f), new Vector3(0.82f, 0.12f, 0.52f), wood, false);
            var loot = chest.AddComponent<LootContainer>();
            loot.displayName = "沉船木箱";
            loot.lid = lid;
            loot.lidOpenEuler = new Vector3(-80f, 0, 0);
            loot.openMessage = "箱子里塞满了泥沙，泥沙底下有东西在闪。";
            loot.contents.Add(new PlayerInventory.Stack { item = GameplaySetup.Item("gold_coin"), count = 3 });
            loot.contents.Add(new PlayerInventory.Stack { item = GameplaySetup.Item("spear"), count = 4 });
            loot.contents.Add(new PlayerInventory.Stack { item = GameplaySetup.Item("pearl"), count = 1 });
            foreach (var t in site.GetComponentsInChildren<Transform>()) t.gameObject.layer = MarineLayer;
        }

        // ================= 鱼 =================

        static void BuildFish()
        {
            var life = new GameObject("MarineLife").AddComponent<MarineLife>();
            if (!StoreAssets.Available) { Debug.LogWarning("[Marine] 没有导入商店鱼类资源，海里暂时没有鱼"); return; }
            // 商店模型：Fish - PolyPack（Alstra Infinite）
            life.species.Add(StoreFish("银鱼群", StoreAssets.PolyFish("FishV3"), 0.2f, MarineLife.Habitat.OpenWater, new Vector2Int(28, 46), 1.6f, 6f, 5.5f, 2.6f, new Vector2(1.5f, 12f), 3, tail: 14f, tailSpeed: 14f));
            life.species.Add(StoreFish("红鲷鱼", StoreAssets.PolyFish("FishV4"), 0.3f, MarineLife.Habitat.Reef, new Vector2Int(4, 9), 0.7f, 3f, 3.5f, 1.1f, new Vector2(2f, 11f), 4, tail: 10f, tailSpeed: 8f, night: 0f));
            life.species.Add(StoreFish("蓝鳍鱼", StoreAssets.PolyFish("FishV1"), 0.32f, MarineLife.Habitat.Reef, new Vector2Int(2, 5), 0.8f, 3.2f, 3.5f, 0.9f, new Vector2(2f, 12f), 3, tail: 10f, tailSpeed: 8f, night: 0f));
            life.species.Add(StoreFish("黑鲷", StoreAssets.PolyFish("FishV2"), 0.55f, MarineLife.Habitat.Reef, new Vector2Int(1, 3), 0.6f, 2.8f, 4f, 1.5f, new Vector2(2.5f, 14f), 3, tail: 8f, tailSpeed: 5f, night: 0.2f));
            life.species.Add(StoreFish("石斑鱼", StoreAssets.PolyFish("FishV2"), 0.95f, MarineLife.Habitat.Seabed, new Vector2Int(1, 1), 0.35f, 2f, 3f, 0.2f, new Vector2(6f, 19f), 2, tail: 6f, tailSpeed: 3f));
            life.species.Add(StoreFish("梭鱼", StoreAssets.PolyFish("FishV3"), 1.1f, MarineLife.Habitat.OpenWater, new Vector2Int(1, 3), 0.9f, 5f, 3.5f, 2f, new Vector2(3f, 15f), 2, tail: 7f, tailSpeed: 5f));
        }

        static MarineLife.Species StoreFish(string name, GameObject prefab, float length, MarineLife.Habitat habitat, Vector2Int school, float speed, float flee, float fleeDist,
                                            float spread, Vector2 depth, int max, float tail, float tailSpeed, float night = 1f)
        {
            var (euler, rawLength) = StoreAssets.FishOrientation(prefab);
            return new MarineLife.Species
            {
                name = name, prefab = prefab, modelEuler = euler, modelScale = length / Mathf.Max(rawLength, 1e-3f),
                tailBeat = tail, tailBeatSpeed = tailSpeed, habitat = habitat, schoolSize = school,
                speed = speed, fleeSpeed = flee, fleeDistance = fleeDist, spread = spread, depth = depth, maxSchools = max, nightActivity = night,
            };
        }

        static MarineLife.Species Species(string name, Mesh mesh, float sheen, float smooth, float swimAmount, float swimSpeed, float swimFreq,
                                          MarineLife.Habitat habitat, Vector2Int school, float speed, float flee, float fleeDist, float spread, Vector2 depth, int max, float night = 1f)
        {
            mesh = SaveMesh(mesh);
            var mat = Mat(mesh.name, wrap: 0.3f, smooth: smooth, sheen: sheen);
            mat.SetFloat("_SwimAmount", swimAmount);
            mat.SetFloat("_SwimSpeed", swimSpeed);
            mat.SetFloat("_SwimFreq", swimFreq);
            mat.SetFloat("_Translucency", 0.25f);
            return new MarineLife.Species
            {
                name = name, mesh = mesh, material = mat, habitat = habitat, schoolSize = school,
                speed = speed, fleeSpeed = flee, fleeDistance = fleeDist, spread = spread, depth = depth, maxSchools = max, nightActivity = night,
            };
        }

        // ================= 资源 =================

        static Mesh SaveMesh(Mesh mesh)
        {
            string path = $"{Dir}/{mesh.name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static Material Mat(string name, float sway = 0f, float swaySpeed = 0.8f, float translucency = 0f, float wrap = 0.3f, float smooth = 0.3f, float sheen = 0f)
        {
            if (mats.TryGetValue(name, out var cached)) return cached;
            var mat = IslandBuilder.MakeMaterial("Marine_" + name, "Island/Marine");
            mat.SetFloat("_SwayAmount", sway);
            mat.SetFloat("_SwaySpeed", swaySpeed);
            mat.SetFloat("_Translucency", translucency);
            mat.SetFloat("_Wrap", wrap);
            mat.SetFloat("_Smoothness", smooth);
            mat.SetFloat("_Sheen", sheen);
            mat.SetFloat("_Cull", 0f);
            mat.SetFloat("_Cutoff", 0f);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            mats[name] = mat;
            return mat;
        }

        static void EnsureLayer()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layer = tagManager.FindProperty("layers").GetArrayElementAtIndex(MarineLayer);
            if (layer.stringValue == "Marine") return;
            layer.stringValue = "Marine";
            tagManager.ApplyModifiedProperties();
        }

        // ================= 水下画面资源（BuildPlayer 用） =================

        public static Texture2D[] CausticFrames()
        {
            var frames = new List<Texture2D>();
            for (int i = 0; i < 32; i++)
            {
                string path = $"Assets/Art/Marine/Caustics/caustic_{i:00}.png";
                if (AssetImporter.GetAtPath(path) is TextureImporter ti)
                {
                    if (ti.textureType != TextureImporterType.Default || ti.wrapMode != TextureWrapMode.Repeat || ti.sRGBTexture)
                    {
                        ti.textureType = TextureImporterType.Default;
                        ti.wrapMode = TextureWrapMode.Repeat;
                        ti.sRGBTexture = false;
                        ti.mipmapEnabled = true;
                        ti.alphaSource = TextureImporterAlphaSource.FromGrayScale;
                        ti.SaveAndReimport();
                    }
                    frames.Add(AssetDatabase.LoadAssetAtPath<Texture2D>(path));
                }
            }
            return frames.ToArray();
        }

        public static Volume UnderwaterVolume()
        {
            const string path = "Assets/Art/Generated/UnderwaterPostProcess.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            foreach (var c in new List<VolumeComponent>(profile.components)) { profile.Remove(c.GetType()); Object.DestroyImmediate(c, true); }
            T Add<T>() where T : VolumeComponent
            {
                var c = profile.Add<T>(true);
                c.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(c, profile);
                return c;
            }
            var color = Add<ColorAdjustments>();
            color.colorFilter.Override(new Color(0.88f, 1f, 1f));
            color.saturation.Override(-4f);
            color.contrast.Override(8f);
            color.postExposure.Override(0.2f);
            var vig = Add<Vignette>();
            vig.intensity.Override(0.3f);
            vig.smoothness.Override(0.5f);
            vig.color.Override(new Color(0f, 0.06f, 0.08f));
            var ca = Add<ChromaticAberration>();
            ca.intensity.Override(0.18f);
            var lens = Add<LensDistortion>();
            lens.intensity.Override(-0.12f);
            var bloom = Add<Bloom>();
            bloom.intensity.Override(0.6f);
            bloom.threshold.Override(0.9f);
            bloom.scatter.Override(0.75f);
            EditorUtility.SetDirty(profile);

            var go = new GameObject("UnderwaterVolume");
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 20;
            vol.weight = 0f;
            vol.sharedProfile = profile;
            return vol;
        }

        static Material ParticleMaterial(string name, Color tint)
        {
            var mat = IslandBuilder.MakeMaterial(name, "Universal Render Pipeline/Particles/Unlit");
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Generated/Textures/soft_dot.png");
            if (tex != null) mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", tint);
            mat.SetFloat("_Surface", 1f); // 透明
            mat.SetFloat("_Blend", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>水中悬浮的颗粒（海雪），跟着摄像机。</summary>
        public static ParticleSystem MarineSnow(Transform cam)
        {
            var go = new GameObject("MarineSnow");
            go.transform.SetParent(cam, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.01f, 0.06f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.03f);
            main.startColor = new Color(0.85f, 0.95f, 0.9f, 0.55f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 900;
            main.playOnAwake = false;
            var em = ps.emission;
            em.rateOverTime = 110f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(14f, 9f, 14f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.08f;
            noise.frequency = 0.3f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = ParticleMaterial("MarineSnow", new Color(0.8f, 0.95f, 0.9f, 0.6f));
            r.shadowCastingMode = ShadowCastingMode.Off;
            return ps;
        }

        /// <summary>呼气时从嘴边冒出的气泡（Breath 调用 Emit）。</summary>
        public static ParticleSystem Bubbles(Transform cam)
        {
            var go = new GameObject("Bubbles");
            go.transform.SetParent(cam, false);
            go.transform.localPosition = new Vector3(0f, -0.12f, 0.22f);
            go.transform.localRotation = Quaternion.Euler(-90f, 0, 0);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.035f);
            main.startColor = new Color(0.9f, 0.97f, 1f, 0.75f);
            main.gravityModifier = -0.25f; // 往上浮
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 0.03f;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.3f;
            noise.frequency = 1.5f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = ParticleMaterial("Bubble", new Color(0.9f, 0.97f, 1f, 0.8f));
            r.shadowCastingMode = ShadowCastingMode.Off;
            return ps;
        }

        // ================= 文本 =================

        const string SeaTabletText =
            "一块半埋在白沙里的石碑，表面长满了细小的珊瑚，刻痕却依然清晰。\n\n" +
            "上半部分刻着一座建在火山上的城市，城中央是一轮发光的太阳。下半部分，同一座城市沉到了水面以下，太阳被画在城市的最深处，周围是一圈圈向外扩散的波纹。\n\n" +
            "最下面一行阿玛拉文字，和父亲笔记里抄过的一模一样：\n\n" +
            "「海替我们守着它。不要潜得太深。」\n\n" +
            "你注意到，周围的鱼都绕着这块石碑游，没有一条靠近。";

        const string DiveSlateText =
            "一块潜水用的白色写字板，挂绳断了，铅笔还拴在上面。是父亲的字。\n\n" +
            "「第 9 天。水深 9 米。石碑 #2，与神殿壁画同源。」\n" +
            "「沉城的方向和壁画一致 —— 正对环形群岛的中心。」\n" +
            "「气瓶剩 60 巴。上浮。」\n\n" +
            "最后一行字迹歪斜，像是在水下匆忙写的：\n\n" +
            "「下面有东西在敲。三下。」";
    }
}
