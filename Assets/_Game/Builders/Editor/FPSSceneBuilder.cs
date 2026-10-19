using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
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
    /// 一键生成写实 FPS 测试场景：菜单 Tools/FPS/重建测试场景，或命令行 -executeMethod FPSSceneBuilder.Build。
    /// 资源全部来自 Poly Haven（CC0），放在 Assets/Art 下；本脚本生成的材质、网格、贴图放在 Assets/Art/Generated。
    /// </summary>
    public static class FPSSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/FPSTest.unity";
        const string Art = "Assets/Art";
        const string Gen = "Assets/Art/Generated";
        const string SkyPath = "Assets/Art/HDRI/sky_clamped_4k.hdr";
        const int WeaponLayer = 8;
        // HDRI 中太阳的方向（由 HDR 最亮区域算出，Skybox/Panoramic 坐标系）
        static readonly Vector3 SunDirection = new Vector3(0.5543f, 0.7418f, -0.3775f);

        [MenuItem("Tools/FPS/重建测试场景")]
        public static void Build()
        {
            foreach (var dir in new[] { Gen, Gen + "/Materials", Gen + "/Meshes", Gen + "/Textures", Path.GetDirectoryName(ScenePath) })
                Directory.CreateDirectory(dir);
            if (AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.DeleteAsset("Assets/Materials"); // 旧版纯色材质

            EnsureWeaponLayer();
            ConfigureImports();
            ConfigurePipeline();

            // ---------- 材质 ----------
            var floorMat = Pbr("Floor", "Textures/concrete_floor_02", 1f / 4f);
            var brickMat = Pbr("Brick", "Textures/brick_wall_006", 1f / 3f);
            var concreteMat = Pbr("Concrete", "Textures/concrete_wall_004", 1f / 3f);
            var pistolMat = Pbr("Pistol", "Models/service_pistol", 1f);
            var crateMat = Pbr("Crate", "Models/wooden_crate_02", 1f);
            var barrierMat = Pbr("Barrier", "Models/concrete_road_barrier", 1f);
            var barrelMat = Pbr("Barrel", "Models/Barrel_01", 1f);
            var targetMat = Painted("TargetPaint", new Color(0.78f, 0.75f, 0.68f), 0.55f, 0.4f);
            var fxMat = Sprite("FX_Soft", GenerateTexture("soft_dot", 64, SoftDot));
            var holeMat = Sprite("FX_BulletHole", GenerateTexture("bullet_hole", 128, BulletHole));

            // ---------- 场景 ----------
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.LookRotation(-SunDirection);
            sun.color = new Color(1f, 0.95f, 0.88f);
            sun.intensity = 2.2f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 1f;
            SetupSky(sun);

            var env = new GameObject("Environment").transform;
            Box("Floor", env, new Vector3(0, -0.5f, 0), new Vector3(50, 1, 50), floorMat);
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f * Mathf.Deg2Rad;
                var wall = Box("Wall", env, new Vector3(Mathf.Sin(a) * 25.5f, 2.5f, Mathf.Cos(a) * 25.5f), new Vector3(52, 5, 1), brickMat);
                wall.transform.rotation = Quaternion.Euler(0, i * 90f, 0);
            }

            // 掩体与高台
            Box("Cover", env, new Vector3(-7, 0.6f, -4), new Vector3(3, 1.2f, 1), concreteMat);
            Box("Cover", env, new Vector3(7, 0.6f, 0), new Vector3(1, 1.2f, 3), concreteMat);
            Box("Cover", env, new Vector3(-2, 1.25f, 6), new Vector3(1, 2.5f, 4), concreteMat);
            Box("Cover", env, new Vector3(12, 1.5f, 10), new Vector3(4, 3f, 4), concreteMat);
            Box("Cover", env, new Vector3(-13, 1.5f, 12), new Vector3(6, 3f, 1), concreteMat);
            Box("Platform", env, new Vector3(0, 0.5f, 20), new Vector3(16, 1f, 6), concreteMat);
            Box("Pillar", env, new Vector3(-18, 2.5f, -12), new Vector3(1.2f, 5f, 1.2f), concreteMat);
            Box("Pillar", env, new Vector3(18, 2.5f, -12), new Vector3(1.2f, 5f, 1.2f), concreteMat);

            var props = new GameObject("Props").transform;
            foreach (var (pos, rot) in new[] { (new Vector3(-4, 0, -10), 0f), (new Vector3(-0.7f, 0, -10), 0f), (new Vector3(3, 0, 3), 90f), (new Vector3(14, 0, -6), 30f), (new Vector3(-10, 0, 4), -20f), (new Vector3(9, 0, 16), 0f) })
                PlaceModel("Models/concrete_road_barrier", barrierMat, props, pos, rot, staticCollider: true);

            var crates = new[] { new Vector3(5, 0, -8), new Vector3(6.1f, 0, -8.3f), new Vector3(5.5f, 0, -6.9f), new Vector3(-9, 0, -2), new Vector3(16, 0, 4), new Vector3(16.4f, 0, 5.2f), new Vector3(-15, 0, 18) };
            var placedCrates = new List<GameObject>();
            foreach (var p in crates)
                placedCrates.Add(PlaceModel("Models/wooden_crate_02", crateMat, props, p, Random.Range(-15f, 15f), mass: 25f));
            // 叠一个箱子
            var top = PlaceModel("Models/wooden_crate_02", crateMat, props, new Vector3(5.5f, 0, -8.1f), 8f, mass: 25f);
            var b = Bounds(placedCrates[0]);
            top.transform.position += Vector3.up * b.size.y;

            foreach (var p in new[] { new Vector3(-6, 0, -9), new Vector3(-6.7f, 0, -8.3f), new Vector3(10.5f, 0, 1.5f), new Vector3(-19, 0, 8), new Vector3(-18.2f, 0, 8.6f), new Vector3(19, 0, 18) })
                PlaceModel("Models/Barrel_01", barrelMat, props, p, Random.Range(0f, 360f), mass: 40f, convex: true);

            var targets = new GameObject("Targets").transform;
            var postMat = Painted("TargetPost", new Color(0.12f, 0.12f, 0.13f), 0.8f, 0.35f);
            for (int i = 0; i < 6; i++)
                SteelTarget(targets, new Vector3(-6.25f + i * 2.5f, 1f, 20.5f), 180f, targetMat, postMat);
            var extra = new[] { (new Vector3(-14f, 0, 8f), 150f), (new Vector3(13f, 0, 14f), 200f), (new Vector3(-20f, 0, -4f), 110f), (new Vector3(20f, 0, 6f), 250f) };
            foreach (var (p, yaw) in extra)
                SteelTarget(targets, p, yaw, targetMat, postMat);

            var probe = new GameObject("ReflectionProbe").AddComponent<ReflectionProbe>();
            probe.transform.position = new Vector3(0, 2.5f, 0);
            probe.size = new Vector3(50, 12, 50);
            probe.boxProjection = true;
            probe.mode = ReflectionProbeMode.Baked;
            probe.resolution = 256;

            CreateVolume();
            BuildPlayer(pistolMat);
            AudioSetup.CreateManager();
            AudioSetup.SetupPlayer(GameObject.Find("Player"), null);

            var fx = new GameObject("FX").AddComponent<FXManager>();
            fx.additiveMaterial = fxMat;
            fx.dustMaterial = fxMat;
            fx.bulletHoleMaterial = holeMat;

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            BakeLighting();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"FPS 测试场景已生成：{ScenePath}");
        }

        // ================= 玩家与武器 =================

        internal static void BuildPlayer(Material pistolMat)
        {
            var player = new GameObject("Player");
            player.transform.position = new Vector3(0, 1, -18);
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.stepOffset = 0.4f;

            var cameraRoot = new GameObject("CameraRoot").transform;
            cameraRoot.SetParent(player.transform, false);
            cameraRoot.localPosition = new Vector3(0, 0.7f, 0);

            var camGo = new GameObject("MainCamera") { tag = "MainCamera" };
            camGo.transform.SetParent(cameraRoot, false);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 500f;
            cam.fieldOfView = 72f;
            cam.cullingMask = ~(1 << WeaponLayer);
            camGo.AddComponent<AudioListener>();
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            camData.stopNaN = true; // 防止个别 NaN 像素被泛光放大成白雾
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camData.antialiasingQuality = AntialiasingQuality.High;

            // 武器单独一个叠加摄像机：不会穿墙，也不受近裁剪影响
            var weaponCamGo = new GameObject("WeaponCamera");
            weaponCamGo.transform.SetParent(camGo.transform, false);
            var weaponCam = weaponCamGo.AddComponent<Camera>();
            weaponCam.fieldOfView = 50f; // 枪模用更小的 FOV，透视不夸张
            weaponCam.nearClipPlane = 0.01f;
            weaponCam.farClipPlane = 5f;
            weaponCam.cullingMask = 1 << WeaponLayer;
            var weaponData = weaponCam.GetUniversalAdditionalCameraData();
            weaponData.renderType = CameraRenderType.Overlay;
            weaponData.renderPostProcessing = true;
            weaponData.renderShadows = false;
            camData.cameraStack.Add(weaponCam);

            var holder = new GameObject("WeaponHolder");
            holder.transform.SetParent(camGo.transform, false);
            holder.transform.localPosition = new Vector3(0.11f, -0.12f, 0.36f);
            holder.transform.localRotation = Quaternion.Euler(0f, -2f, 0f);

            var (pistol, muzzleLocal) = CreateViewModel("Models/service_pistol", pistolMat, holder.transform);
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(pistol.transform, false);
            muzzle.localPosition = muzzleLocal;
            muzzle.rotation = holder.transform.rotation; // 枪管已对准 holder 的 +Z

            var flash = new GameObject("MuzzleLight").AddComponent<Light>();
            flash.transform.SetParent(muzzle, false);
            flash.type = LightType.Point;
            flash.range = 6f;
            flash.intensity = 6f;
            flash.color = new Color(1f, 0.7f, 0.4f);
            flash.shadows = LightShadows.None;
            flash.enabled = false; // 只在开枪的瞬间打开

            SetLayerRecursive(holder, WeaponLayer);

            var controller = player.AddComponent<PlayerController>();
            controller.cameraRoot = cameraRoot;

            var feel = holder.AddComponent<WeaponFeel>();
            feel.player = controller;

            // 背包：开局带着手枪和一个备用弹匣
            var inventory = player.AddComponent<PlayerInventory>();
            inventory.startItems.Add(new PlayerInventory.Stack { item = GameplaySetup.Item("pistol"), count = 1 });
            inventory.startItems.Add(new PlayerInventory.Stack { item = GameplaySetup.Item("ammo_9mm"), count = 12 });

            var weapons = camGo.AddComponent<WeaponController>();
            weapons.slots = GameplaySetup.BuildViewModels(holder.transform, pistol, muzzle);
            weapons.weaponFeel = feel;
            weapons.muzzleLight = flash;
            weapons.hitMask = ~(1 << WeaponLayer);
            weapons.projectilePrefab = GameplaySetup.SpearPrefab();
            SetLayerRecursive(holder, WeaponLayer);
            for (int i = 1; i < weapons.slots.Count; i++) weapons.slots[i].viewModel.SetActive(false); // 默认拿手枪

            // 游泳：氧气、气泡、水下画面和声音
            var breath = player.AddComponent<Breath>();
            breath.bubbles = MarineBuilder.Bubbles(camGo.transform);
            camGo.AddComponent<UnderwaterAudio>();
            var underwater = camGo.AddComponent<UnderwaterFX>();
            underwater.volume = MarineBuilder.UnderwaterVolume();
            underwater.particles = MarineBuilder.MarineSnow(camGo.transform);
            underwater.causticFrames = MarineBuilder.CausticFrames();
            underwater.marineLayer = MarineBuilder.MarineLayer;

            var hud = player.AddComponent<HUD>();
            hud.weapons = weapons;
            hud.player = controller;

            var bag = player.AddComponent<InventoryUI>();
            bag.inventory = inventory;
            bag.weapons = weapons;
        }

        /// <summary>
        /// 把枪模摆正：最长水平轴对准 +Z，握把一端朝后；返回枪模和枪口在枪模局部空间的位置。
        /// </summary>
        internal static (GameObject, Vector3) CreateViewModel(string folder, Material mat, Transform parent)
        {
            var prefab = LoadModel(folder);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = "Pistol";
            // 这个 FBX 是展示用的套件：两把完整手枪（_a/_b 两种状态）+ 散落的弹匣和子弹，只保留 _b 那把
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>().ToList())
                if (!mf.name.EndsWith("_b")) Object.DestroyImmediate(mf.gameObject);
            AssignMaterial(go, mat);
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = true;
            }

            // 在模型自身坐标系里收集顶点
            var pts = new List<Vector3>();
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                foreach (var v in mf.sharedMesh.vertices)
                    pts.Add(go.transform.InverseTransformPoint(mf.transform.TransformPoint(v)));
            var min = pts.Aggregate(Vector3.Min);
            var max = pts.Aggregate(Vector3.Max);
            var size = max - min;

            // 长轴（x 或 z）为枪管方向；握把在低处，握把所在一端为后
            bool alongX = size.x > size.z;
            float lowY = min.y + size.y * 0.3f;
            var low = pts.Where(p => p.y < lowY).ToList();
            float gripAxis = low.Average(p => alongX ? p.x : p.z);
            float center = alongX ? (min.x + max.x) / 2 : (min.z + max.z) / 2;
            float sign = gripAxis < center ? 1f : -1f; // 枪口朝向长轴的哪一端

            Quaternion fix = alongX ? Quaternion.Euler(0, sign > 0 ? -90 : 90, 0) : Quaternion.Euler(0, sign > 0 ? 0 : 180, 0);

            // 枪口：前端、上部（枪管在套筒上半部分）
            float frontAxis = sign > 0 ? (alongX ? max.x : max.z) : (alongX ? min.x : min.z);
            var front = pts.Where(p => Mathf.Abs((alongX ? p.x : p.z) - frontAxis) < 0.01f).ToList();
            var muzzleModel = front.Count > 0
                ? new Vector3(front.Average(p => p.x), front.Average(p => p.y), front.Average(p => p.z))
                : new Vector3(alongX ? frontAxis : (min.x + max.x) / 2, max.y - size.y * 0.15f, alongX ? (min.z + max.z) / 2 : frontAxis);

            // 用一个中间节点承载旋转，枪模中心对齐到 holder 原点
            var pivot = new GameObject("PistolPivot").transform;
            pivot.SetParent(parent, false);
            go.transform.SetParent(pivot, false);
            go.transform.localRotation = fix;
            var centerModel = (min + max) / 2;
            go.transform.localPosition = -(fix * Vector3.Scale(centerModel, go.transform.localScale));

            Debug.Log($"[FPS] pistol size {size}, alongX {alongX}, muzzleSign {sign}, muzzle {muzzleModel}");
            // muzzleModel 已经是 go 的局部坐标，Muzzle 直接挂在 go 下
            return (go, muzzleModel);
        }

        // ================= 场景元素 =================

        /// <summary>钢板人形靶：立柱 + 躯干板 + 头部板，命中钢板算伤害。pos 为底座落地点。</summary>
        internal static void SteelTarget(Transform parent, Vector3 pos, float yaw, Material plateMat, Material postMat)
        {
            var root = new GameObject("SteelTarget").transform;
            root.SetParent(parent);
            root.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));

            var plate = new GameObject("Plate").transform;
            plate.SetParent(root, false);
            plate.gameObject.AddComponent<Target>();
            Part("Torso", plate, new Vector3(0, 1.15f, 0), new Vector3(0.46f, 0.62f, 0.025f), plateMat, true);
            Part("Neck", plate, new Vector3(0, 1.5f, 0), new Vector3(0.1f, 0.08f, 0.025f), plateMat, true);
            Part("Head", plate, new Vector3(0, 1.66f, 0), new Vector3(0.24f, 0.26f, 0.025f), plateMat, true);

            Part("Post", root, new Vector3(0, 0.42f, 0.03f), new Vector3(0.06f, 0.84f, 0.06f), postMat, true);
            Part("Base", root, new Vector3(0, 0.03f, 0.03f), new Vector3(0.5f, 0.06f, 0.35f), postMat, true);
        }

        internal static void Part(string name, Transform parent, Vector3 localPos, Vector3 size, Material mat, bool collider)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.AddComponent<MeshFilter>().sharedMesh = BoxMesh(size);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (collider) go.AddComponent<BoxCollider>().size = size;
        }

        internal static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = pos;
            go.AddComponent<MeshFilter>().sharedMesh = BoxMesh(size);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.AddComponent<BoxCollider>().size = size;
            go.isStatic = true;
            return go;
        }

        /// <summary>按米展开 UV 的立方体网格，贴图密度在任意尺寸下一致。</summary>
        internal static Mesh BoxMesh(Vector3 s)
        {
            string path = $"{Gen}/Meshes/Box_{s.x:0.##}x{s.y:0.##}x{s.z:0.##}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;

            var h = s / 2;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            void Face(Vector3 n, Vector3 u, Vector3 v, float su, float sv)
            {
                int i = verts.Count;
                Vector3 c = Vector3.Scale(n, h);
                Vector3 hu = Vector3.Scale(u, h), hv = Vector3.Scale(v, h);
                verts.Add(c - hu - hv); verts.Add(c + hu - hv); verts.Add(c + hu + hv); verts.Add(c - hu + hv);
                uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(su, 0)); uvs.Add(new Vector2(su, sv)); uvs.Add(new Vector2(0, sv));
                tris.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
            }
            Face(Vector3.forward, Vector3.left, Vector3.up, s.x, s.y);
            Face(Vector3.back, Vector3.right, Vector3.up, s.x, s.y);
            Face(Vector3.right, Vector3.forward, Vector3.up, s.z, s.y);
            Face(Vector3.left, Vector3.back, Vector3.up, s.z, s.y);
            Face(Vector3.up, Vector3.right, Vector3.forward, s.x, s.z);
            Face(Vector3.down, Vector3.right, Vector3.back, s.x, s.z);

            var mesh = new Mesh { name = Path.GetFileNameWithoutExtension(path) };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            Unwrapping.GenerateSecondaryUVSet(mesh);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        internal static GameObject PlaceModel(string folder, Material mat, Transform parent, Vector3 pos, float yaw,
                                     float mass = 0f, bool convex = false, bool staticCollider = false)
        {
            var prefab = LoadModel(folder);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(parent);
            // 保留模型根节点自带的旋转（有的 FBX 根节点带 90° 轴向修正）
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0) * prefab.transform.localRotation);
            if (mat != null) AssignMaterial(go, mat);

            var bounds = Bounds(go);
            go.transform.position += Vector3.up * (pos.y - bounds.min.y);

            if (staticCollider)
            {
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                    mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                foreach (var t in go.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
            }
            else if (mass > 0f)
            {
                if (convex)
                {
                    foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                    {
                        var mc = mf.gameObject.AddComponent<MeshCollider>();
                        mc.sharedMesh = mf.sharedMesh;
                        mc.convex = true;
                    }
                }
                else
                {
                    var bc = go.AddComponent<BoxCollider>();
                    bounds = Bounds(go);
                    bc.center = go.transform.InverseTransformPoint(bounds.center);
                    bc.size = Quaternion.Inverse(go.transform.rotation) * bounds.size;
                    bc.size = new Vector3(Mathf.Abs(bc.size.x), Mathf.Abs(bc.size.y), Mathf.Abs(bc.size.z));
                    var ls = go.transform.lossyScale;
                    bc.size = new Vector3(bc.size.x / ls.x, bc.size.y / ls.y, bc.size.z / ls.z);
                }
                var rb = go.AddComponent<Rigidbody>();
                rb.mass = mass;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
            }
            return go;
        }

        internal static GameObject LoadModel(string folder)
        {
            string id = Path.GetFileName(folder);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>($"{Art}/{folder}/{id}.fbx");
            if (go == null) throw new FileNotFoundException($"找不到模型 {Art}/{folder}/{id}.fbx");
            return go;
        }

        internal static void AssignMaterial(GameObject go, Material mat)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>())
                r.sharedMaterials = Enumerable.Repeat(mat, Mathf.Max(1, r.sharedMaterials.Length)).ToArray();
        }

        internal static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        internal static void SetLayerRecursive(GameObject go, int layer)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        // ================= 光照与后处理 =================

        internal static void SetupSky(Light sun)
        {
            var hdr = AssetDatabase.LoadAssetAtPath<Texture2D>(SkyPath);
            var skyMat = LoadOrCreateMaterial("Sky", "Skybox/Panoramic");
            skyMat.SetTexture("_MainTex", hdr);
            skyMat.SetFloat("_Mapping", 1);
            skyMat.SetFloat("_ImageType", 0);
            skyMat.SetFloat("_Exposure", 1f);
            skyMat.SetFloat("_Rotation", 0f);
            skyMat.DisableKeyword("_MAPPING_6_FRAMES_LAYOUT");
            EditorUtility.SetDirty(skyMat);

            RenderSettings.skybox = skyMat;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.25f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 1f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.004f;
            RenderSettings.fogColor = new Color(0.68f, 0.75f, 0.84f);
        }

        internal static void CreateVolume(string profileName = "PostProcessProfile", float temperature = 5f, float saturation = -8f, float contrast = 6f)
        {
            string path = $"{Gen}/{profileName}.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            T Add<T>() where T : VolumeComponent
            {
                var c = profile.Add<T>(true);
                c.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(c, profile);
                return c;
            }

            Add<Tonemapping>().mode.Override(TonemappingMode.ACES);
            var bloom = Add<Bloom>();
            bloom.threshold.Override(1.1f);
            bloom.intensity.Override(0.35f);
            bloom.scatter.Override(0.65f);
            bloom.highQualityFiltering.Override(true);
            var color = Add<ColorAdjustments>();
            color.postExposure.Override(0.25f);
            color.contrast.Override(contrast);
            color.saturation.Override(saturation);
            var wb = Add<WhiteBalance>();
            wb.temperature.Override(temperature);
            var smh = Add<ShadowsMidtonesHighlights>();
            smh.shadows.Override(new Vector4(0.97f, 1f, 1.06f, 0f));
            smh.highlights.Override(new Vector4(1.04f, 1f, 0.95f, 0f));
            var vignette = Add<Vignette>();
            vignette.intensity.Override(0.24f);
            vignette.smoothness.Override(0.45f);
            Add<ChromaticAberration>().intensity.Override(0.04f);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var vol = new GameObject("PostProcess").AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 1;
            vol.sharedProfile = profile;
        }

        internal static void BakeLighting()
        {
            string path = $"{Gen}/LightingSettings.lighting";
            var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(path);
            if (ls == null)
            {
                ls = new LightingSettings { name = "LightingSettings" };
                AssetDatabase.CreateAsset(ls, path);
            }
            ls.bakedGI = false;          // 只烘焙环境光和反射探针，几秒完成
            ls.realtimeGI = false;
            Lightmapping.lightingSettings = ls;
            Lightmapping.Bake();
        }

        internal static void ConfigurePipeline()
        {
            var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            if (rp == null) return;
            var so = new SerializedObject(rp);
            void Set(string prop, System.Action<SerializedProperty> set)
            {
                var p = so.FindProperty(prop);
                if (p != null) set(p); else Debug.LogWarning($"[FPS] URP 资产没有属性 {prop}");
            }
            Set("m_ShadowDistance", p => p.floatValue = 70f);
            Set("m_ShadowCascadeCount", p => p.intValue = 4);
            Set("m_MainLightShadowmapResolution", p => p.intValue = 4096);
            Set("m_SoftShadowQuality", p => p.intValue = 3);
            Set("m_SupportsHDR", p => p.boolValue = true);
            Set("m_ColorGradingMode", p => p.intValue = 1); // HDR 调色
            Set("m_SupportsCameraDepthTexture", p => p.boolValue = true);
            so.ApplyModifiedProperties();
        }

        internal static void EnsureWeaponLayer()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            var layer = layers.GetArrayElementAtIndex(WeaponLayer);
            if (layer.stringValue != "Weapon")
            {
                layer.stringValue = "Weapon";
                tagManager.ApplyModifiedProperties();
            }
        }

        internal static void ConfigureImports()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Art }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith(Gen) || !(AssetImporter.GetAtPath(path) is TextureImporter ti)) continue;
                string n = Path.GetFileNameWithoutExtension(path);
                bool hdr = path.EndsWith(".hdr");
                bool normal = n.EndsWith("_normal");
                bool linear = n.EndsWith("_ms") || n.EndsWith("_ao") || n.EndsWith("_mask");
                // 植物叶片：保留 Alpha 覆盖率，远处 mip 不会越来越稀
                bool cutout = n.EndsWith("albedo") && ti.DoesSourceTextureHaveAlpha();
                int maxSize = hdr ? 4096 : 2048;

                bool ok = ti.maxTextureSize == maxSize
                          && (!normal || ti.textureType == TextureImporterType.NormalMap)
                          && (!linear || !ti.sRGBTexture)
                          && (!cutout || ti.mipMapsPreserveCoverage)
                          && (!hdr || !ti.mipmapEnabled);
                if (ok) continue;

                ti.maxTextureSize = maxSize;
                if (normal) ti.textureType = TextureImporterType.NormalMap;
                if (linear) ti.sRGBTexture = false;
                if (cutout)
                {
                    ti.alphaIsTransparency = true;
                    ti.mipMapsPreserveCoverage = true;
                    ti.alphaTestReferenceValue = 0.5f;
                }
                if (hdr)
                {
                    ti.mipmapEnabled = false;
                    ti.wrapModeU = TextureWrapMode.Repeat;
                    ti.wrapModeV = TextureWrapMode.Clamp;
                    ti.textureCompression = TextureImporterCompression.CompressedHQ;
                }
                ti.SaveAndReimport();
            }

            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Art }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is ModelImporter mi)) continue;
                // 导入 FBX 自带材质只为保留材质名（树干/枝/叶靠名字区分），之后会被替换成我们的 URP 材质
                // 枪模要读顶点来摆正；草/蕨/灌木要把网格烘焙成地形细节用的独立网格
                bool readable = new[] { "service_pistol", "grass_medium_01", "fern_02", "shrub_03" }.Any(path.Contains);
                bool lods = SetMeshLodGeneration(mi, !readable);
                if (mi.materialImportMode == ModelImporterMaterialImportMode.ImportStandard && mi.bakeAxisConversion && mi.isReadable == readable && !lods) continue;
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                mi.bakeAxisConversion = true;
                mi.isReadable = readable;
                mi.SaveAndReimport();
            }
        }

        /// <summary>
        /// Unity 6.2+ 的 Mesh LOD（导入时自动减面生成 LOD）。用反射设置，旧版本没有这个属性时直接跳过。
        /// 返回 true 表示设置有变化、需要重新导入。
        /// </summary>
        static bool SetMeshLodGeneration(ModelImporter mi, bool enable)
        {
            var prop = typeof(ModelImporter).GetProperty("generateMeshLods");
            if (prop == null || prop.PropertyType != typeof(bool)) return false;
            if ((bool)prop.GetValue(mi) == enable) return false;
            prop.SetValue(mi, enable);
            return true;
        }

        // ================= 材质与贴图 =================

        internal static Material Pbr(string name, string folder, float tiling)
        {
            string id = Path.GetFileName(folder);
            string dir = $"{Art}/{folder}";
            var mat = LoadOrCreateMaterial(name, "Universal Render Pipeline/Lit");
            mat.SetTexture("_BaseMap", FindTex(dir, id + "_albedo"));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_BumpMap", FindTex(dir, id + "_normal"));
            mat.SetFloat("_BumpScale", 1f);
            mat.EnableKeyword("_NORMALMAP");
            mat.SetTexture("_MetallicGlossMap", FindTex(dir, id + "_ms"));
            mat.SetFloat("_Smoothness", 1f);
            mat.SetFloat("_SmoothnessTextureChannel", 0);
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            mat.SetTexture("_OcclusionMap", FindTex(dir, id + "_ao"));
            mat.SetFloat("_OcclusionStrength", 1f);
            mat.EnableKeyword("_OCCLUSIONMAP");
            mat.SetTextureScale("_BaseMap", Vector2.one * tiling);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        internal static Material Painted(string name, Color color, float metallic, float smoothness)
        {
            var mat = LoadOrCreateMaterial(name, "Universal Render Pipeline/Lit");
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetColor("_EmissionColor", Color.black);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        internal static Material Sprite(string name, Texture2D tex)
        {
            var mat = LoadOrCreateMaterial(name, "Sprites/Default");
            mat.mainTexture = tex;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        internal static Material LoadOrCreateMaterial(string name, string shader)
        {
            string path = $"{Gen}/Materials/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader.name != shader)
            {
                mat.shader = Shader.Find(shader);
            }
            return mat;
        }

        internal static Texture2D FindTex(string dir, string name)
        {
            var guid = AssetDatabase.FindAssets(name + " t:Texture2D", new[] { dir })
                .FirstOrDefault(g => Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g)) == name);
            if (guid == null) throw new FileNotFoundException($"找不到贴图 {dir}/{name}");
            return AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
        }

        internal static Texture2D GenerateTexture(string name, int size, System.Func<float, float, float, Color> pixel)
        {
            string path = $"{Gen}/Textures/{name}.png";
            if (!File.Exists(path))
            {
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var rnd = new System.Random(7);
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float u = (x + 0.5f) / size * 2 - 1, v = (y + 0.5f) / size * 2 - 1;
                        tex.SetPixel(x, y, pixel(u, v, (float)rnd.NextDouble()));
                    }
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                ti.alphaIsTransparency = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        internal static Color SoftDot(float u, float v, float n)
        {
            float r = Mathf.Sqrt(u * u + v * v);
            float a = Mathf.Pow(Mathf.Clamp01(1 - r), 2.2f);
            return new Color(1, 1, 1, a);
        }

        internal static Color BulletHole(float u, float v, float n)
        {
            float r = Mathf.Sqrt(u * u + v * v);
            float ang = Mathf.Atan2(v, u);
            float jag = 0.06f * Mathf.Sin(ang * 7f) + 0.04f * Mathf.Sin(ang * 13f + 1f);
            if (r < 0.22f + jag * 0.5f) return new Color(0.02f, 0.02f, 0.02f, 1f);
            float ring = Mathf.Clamp01(1 - (r - 0.22f) / (0.55f + jag));
            float shade = Mathf.Lerp(0.25f, 0.12f, ring);
            return new Color(shade, shade * 0.97f, shade * 0.94f, Mathf.Pow(ring, 1.5f) * (0.75f + 0.25f * n));
        }
    }
}
