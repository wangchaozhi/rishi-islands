using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
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
    /// 生成荒岛场景：程序化地形（沙滩/草地/丛林/岩壁）、海洋、植被、日夜交替、玩家。
    /// 菜单 Tools/Island/重建荒岛场景，或命令行 -executeMethod Rishi.Builders.IslandBuilder.Build；
    /// 也可以在 Tools/Island/分步重建 里单独重跑某一步（命令行用 IslandBuilder.RebuildStory / RebuildNature / RebuildAudio / RebuildVisual）。
    /// 海平面在 y = 0；地形高度范围 -20 ~ 60 米。
    /// </summary>
    public static class IslandBuilder
    {
        public const string ScenePath = "Assets/Scenes/Island.unity";
        const string Art = "Assets/Art";
        internal const string Gen = "Assets/Art/Generated/Island";
        internal const float Size = 600f;
        internal const float MinHeight = -20f;
        const float HeightRange = 100f;
        const int HeightRes = 1025;
        const int AlphaRes = 1024;
        const int DetailRes = 1024;
        const int Seed = 1337;

        // 场景按生成步骤分组：每一步新建的物体都收进同名根节点，单独重跑某一步时先删掉它的根节点再重建。
        // 地形是其余步骤的基础，改地形就整座岛重建；剧情重建会换掉玩家，所以连带重跑声音和画质；
        // 植被重建会重新生成植物材质，所以连带重跑画质（植物风动画着色器）。
        const string TerrainRoot = "[地形]", StoryRoot = "[剧情]", NatureRoot = "[地貌与植被]", AudioRoot = "[声音]", VisualRoot = "[画质]";

        static readonly (string root, System.Action build)[] Steps =
        {
            (TerrainRoot, BuildTerrainStep),
            (StoryRoot, BuildStoryStep),
            (NatureRoot, BuildNatureStep),
            (AudioRoot, BuildAudioStep),
            (VisualRoot, BuildVisualStep),
        };

        [MenuItem("Tools/Island/重建荒岛场景")]
        public static void Build()
        {
            Prepare(cleanPlants: true);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            foreach (var (root, build) in Steps) RunStep(root, build);
            Save(scene);
        }

        [MenuItem("Tools/Island/分步重建/地形（整座岛重建）")]
        public static void RebuildTerrain() => Build();

        [MenuItem("Tools/Island/分步重建/剧情（连带声音、画质）")]
        public static void RebuildStory() => Rerun(StoryRoot, AudioRoot, VisualRoot);

        [MenuItem("Tools/Island/分步重建/地貌与植被（连带画质）")]
        public static void RebuildNature() => Rerun(NatureRoot, VisualRoot);

        [MenuItem("Tools/Island/分步重建/声音")]
        public static void RebuildAudio() => Rerun(AudioRoot);

        [MenuItem("Tools/Island/分步重建/画质")]
        public static void RebuildVisual() => Rerun(VisualRoot);

        static void Rerun(params string[] roots)
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath);
            }
            var missing = Steps.Select(s => s.root).Where(r => scene.GetRootGameObjects().All(g => g.name != r)).ToList();
            if (missing.Count > 0)
            {
                Debug.LogError($"[Island] 场景里没有 {string.Join("、", missing)}，是分步之前的旧场景，请先用「重建荒岛场景」整体重建一次");
                return;
            }
            Prepare(cleanPlants: roots.Contains(NatureRoot));
            foreach (var (root, build) in Steps)
                if (roots.Contains(root)) RunStep(root, build);
            Save(scene);
        }

        /// <summary>删掉这一步上次生成的根节点，重新生成，并把新出现的根物体收进根节点。每一步用固定的随机种子，单独重跑和整体重建结果一致。</summary>
        static void RunStep(string rootName, System.Action build)
        {
            var scene = SceneManager.GetActiveScene();
            foreach (var old in scene.GetRootGameObjects().Where(g => g.name == rootName)) Object.DestroyImmediate(old);
            Random.InitState(Seed + System.Array.FindIndex(Steps, s => s.root == rootName) * 1000);
            var before = new HashSet<GameObject>(scene.GetRootGameObjects());
            build();
            var root = new GameObject(rootName);
            foreach (var go in scene.GetRootGameObjects())
                if (go != root && !before.Contains(go)) go.transform.SetParent(root.transform, true);
            Debug.Log($"[Island] 步骤 {rootName} 完成，{root.transform.childCount} 个物体");
        }

        static void Prepare(bool cleanPlants)
        {
            foreach (var dir in new[] { Gen, Gen + "/Materials", Gen + "/Meshes", Gen + "/Prefabs", Gen + "/Terrain" })
                Directory.CreateDirectory(dir);
            FPSSceneBuilder.EnsureSharedAssets();
            FPSSceneBuilder.EnsureWeaponLayer();
            FPSSceneBuilder.ConfigureImports();
            if (BirdsSetup.Available) { BirdsSetup.EnsureTags(); BirdsSetup.ConvertMaterials(); }
            if (cleanPlants) JunglePlants.Clean(); // 程序化植物的网格和预制体全部重新生成（只有植被步骤引用它们）
            JunglePlants.ConfigureTextures();
            FPSSceneBuilder.ConfigurePipeline();
            ConfigurePipelineForIsland();
        }

        static void Save(Scene scene)
        {
            var cycle = Object.FindFirstObjectByType<DayNightCycle>();
            if (cycle != null) { cycle.Apply(false); DynamicGI.UpdateEnvironment(); }
            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            var player = GameObject.Find("Player");
            Debug.Log($"[Island] 荒岛场景已保存：{ScenePath}" + (player != null ? $"，出生点 {player.transform.position}" : ""));
        }

        static Terrain IslandTerrain => Object.FindFirstObjectByType<Terrain>();
        static DayNightCycle Cycle => Object.FindFirstObjectByType<DayNightCycle>();

        // ---------- 步骤 1：地形（光照与天空、昼夜、地形、海洋） ----------
        static void BuildTerrainStep()
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.82f; // 模拟林冠下叶片间的反弹光，阴影不至于死黑
            var moon = new GameObject("Moon").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.62f, 0.72f, 1f);
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = 0.8f;

            var skyMat = MakeMaterial("IslandSky", "Island/Sky");
            skyMat.SetColor("_DayZenith", new Color(0.2f, 0.42f, 0.78f));
            skyMat.SetColor("_DayHorizon", new Color(0.52f, 0.67f, 0.86f));
            skyMat.SetColor("_SunsetZenith", new Color(0.36f, 0.43f, 0.62f));
            skyMat.SetColor("_SunsetHorizon", new Color(1f, 0.55f, 0.28f));
            skyMat.SetColor("_NightZenith", new Color(0.03f, 0.05f, 0.1f));
            skyMat.SetColor("_NightHorizon", new Color(0.07f, 0.1f, 0.16f));
            skyMat.SetFloat("_SunSize", 0.00045f);
            skyMat.SetFloat("_SunIntensity", 14f);
            skyMat.SetFloat("_MoonSize", 0.0007f);
            EditorUtility.SetDirty(skyMat);
            RenderSettings.skybox = skyMat;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.0015f;

            var probe = new GameObject("SkyReflectionProbe").AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.cullingMask = 0; // 只拍天空
            probe.size = new Vector3(5000, 2000, 5000);
            probe.resolution = 128;
            probe.hdr = true;
            probe.transform.position = new Vector3(0, 30, 0);

            var cycle = new GameObject("DayNightCycle").AddComponent<DayNightCycle>();
            cycle.config = ConfigAssets.DayNight; // 默认早上 7 点：风暴过后，日出后不久在沙滩上醒来（太阳高度 15°，低角度暖光）
            cycle.sun = sun;
            cycle.moon = moon;
            cycle.skyProbe = probe;
            cycle.timeOfDay = cycle.config.startTime;

            CreateTerrain(out var heightTex);
            CreateOcean(heightTex);
        }

        // ---------- 步骤 2：剧情（沉船、营地、神殿、拾取物、玩家、剧情系统、开场 CG） ----------
        static void BuildStoryStep()
        {
            var terrain = IslandTerrain;
            var cycle = Cycle;

            // 先建剧情场景，植被步骤会避开它们
            IslandStoryBuilder.ClearZones.Clear();
            var spawnPoint = FindBeachSpawn(terrain);
            IslandStoryBuilder.ClearZones.Add((new Vector2(spawnPoint.x, spawnPoint.z), IslandStoryBuilder.SpawnClearRadius));
            IslandStoryBuilder.BuildWreck(terrain, spawnPoint);
            IslandStoryBuilder.BuildCamp(terrain, spawnPoint);
            IslandStoryBuilder.BuildRuin(terrain);

            // 篝火：营地那圈熄灭的篝火、出生沙滩后面、神殿台阶前；点燃后是安全区和复活点
            var campRoot = GameObject.Find("FatherCamp").transform;
            CampfireBuilder.Build(campRoot, IslandStoryBuilder.Ground(terrain, campRoot.TransformPoint(new Vector3(0, 0, 1.4f))), campRoot.eulerAngles.y, ring: false, "CampFire");
            var beachFire = IslandStoryBuilder.Ground(terrain, spawnPoint + new Vector3(7f, 0, 9f));
            if (beachFire.y < 1.2f) beachFire = IslandStoryBuilder.Ground(terrain, spawnPoint + new Vector3(0, 0, 12f)); // 别落在水里
            CampfireBuilder.Build(null, beachFire, 20f, ring: true, "BeachFire");
            var temple = GameObject.Find("SunTemple").transform;
            CampfireBuilder.Build(temple, IslandStoryBuilder.Ground(terrain, temple.TransformPoint(new Vector3(-3.5f, 0, 14f))), temple.eulerAngles.y, ring: true, "TempleFire");

            // 武器与道具拾取物（放在沉船、营地旁）
            GameplaySetup.PlacePickups(terrain, spawnPoint);

            // 玩家：出生时面朝沉船
            var pistolMat = FPSSceneBuilder.Pbr("Pistol", "Models/service_pistol", 1f);
            FPSSceneBuilder.BuildPlayer(pistolMat);
            var player = GameObject.Find("Player");
            var wreck = GameObject.Find("Shipwreck").transform.position;
            float spawnYaw = Mathf.Atan2(wreck.x - spawnPoint.x, wreck.z - spawnPoint.z) * Mathf.Rad2Deg;
            player.transform.SetPositionAndRotation(spawnPoint + Vector3.up * 1.05f, Quaternion.Euler(0, spawnYaw, 0));
            var cam = player.GetComponentInChildren<Camera>();
            cam.farClipPlane = 3000f;
            player.GetComponent<HUD>().dayNight = cycle;
            cam.GetComponent<UnderwaterFX>().dayNight = cycle;
            GameplaySetup.SetupFlashlight(player, cam, FPSSceneBuilder.WeaponLayer); // 手电筒在营地捡到后才能用
            player.AddComponent<PlayerHealth>();

            // 剧情系统
            var story = new GameObject("Story");
            story.AddComponent<GameState>();
            var monologue = story.AddComponent<OpeningMonologue>();
            monologue.note = IslandStoryBuilder.Opening;
            var interactor = cam.gameObject.AddComponent<PlayerInteractor>();
            interactor.mask = ~(1 << 8);
            player.AddComponent<StoryUI>().interactor = interactor;

            // 开场 CG：CG01 风暴 → CG02 醒来
            var wakeUp = CutsceneBuilder.BuildWakeUp(player, wreck, monologue, playOnStart: false);
            CutsceneBuilder.BuildStorm(cycle, GameObject.Find("Ocean").GetComponent<Renderer>(), wakeUp);

            var fx = new GameObject("FX").AddComponent<FXManager>();
            fx.additiveMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Generated/Materials/FX_Soft.mat");
            fx.dustMaterial = fx.additiveMaterial;
            fx.bulletHoleMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Generated/Materials/FX_BulletHole.mat");
        }

        // ---------- 步骤 3：地貌与植被（溪流、海崖、红树林、树、石头、地表植物、海底世界、小动物、鸟群） ----------
        static void BuildNatureStep()
        {
            var terrain = IslandTerrain;
            CollectClearZones(terrain);

            IslandFeatures.BuildRiver(terrain);
            IslandFeatures.BuildCliffsAndRocks(terrain);
            IslandFeatures.BuildMangroves(terrain, TreePrefab("island_tree_02"));

            PlaceTrees(terrain);
            PlacePalms(terrain);
            PlaceCanopy(terrain);
            PlaceRocks(terrain);
            PaintDetails(terrain);

            // 海底世界：沙波、礁盘、珊瑚、海草、沉船残骸
            MarineBuilder.Build(terrain, FindBeachSpawn(terrain));

            var wildlife = new GameObject("Wildlife").AddComponent<WildlifeDirector>(); // 运行时通过 GameSignals 找玩家
            wildlife.config = ConfigAssets.Wildlife;
            wildlife.dayNight = Cycle;
            wildlife.fireflyMaterial = FireflyMaterial();
            if (BirdsSetup.Available) BirdsSetup.Build(terrain, Cycle); // Living Birds（Asset Store）
        }

        /// <summary>植物、石头要避开的空地：出生点、沉船、营地、神殿。从场景里的剧情物体读位置，单独重跑植被时也能用。</summary>
        static void CollectClearZones(Terrain terrain)
        {
            var zones = IslandStoryBuilder.ClearZones;
            zones.Clear();
            var spawn = FindBeachSpawn(terrain);
            zones.Add((new Vector2(spawn.x, spawn.z), IslandStoryBuilder.SpawnClearRadius));
            void Add(string name, float radius)
            {
                var go = GameObject.Find(name);
                if (go != null) zones.Add((new Vector2(go.transform.position.x, go.transform.position.z), radius));
                else Debug.LogWarning($"[Island] 场景里没有 {name}，植被不会避开它");
            }
            Add("Shipwreck", IslandStoryBuilder.WreckClearRadius);
            Add("FatherCamp", IslandStoryBuilder.CampClearRadius);
            Add("BeachFire", 3.5f);
            zones.Add((IslandStoryBuilder.RuinCenter, IslandStoryBuilder.RuinClearRadius));
        }

        // ---------- 步骤 4：声音（音效表、音效管理器、玩家脚步声与环境声） ----------
        static void BuildAudioStep()
        {
            var player = GameObject.Find("Player");
            foreach (var c in player.GetComponents<Footsteps>()) Object.DestroyImmediate(c);
            foreach (var c in player.GetComponents<AmbientController>()) Object.DestroyImmediate(c);
            AudioSetup.CreateManager();
            AudioSetup.SetupPlayer(player, Cycle);
        }

        // ---------- 步骤 5：画质（后处理、风、体积光、植物风动画、TAA、动态天气、过场电影后处理） ----------
        static void BuildVisualStep()
        {
            FPSSceneBuilder.CreateVolume("IslandPostProcess", temperature: -1f, saturation: -4f, contrast: 5f);
            new GameObject("Wind").AddComponent<WindController>().config = ConfigAssets.Wind;
            CinematicSetup.EnsureVolumetricLight();
            CinematicSetup.ApplyFoliageWind();
            CinematicSetup.TuneGameplayProfile("Assets/Art/Generated/IslandPostProcess.asset");
            CinematicSetup.UseTAA(GameObject.Find("Player").GetComponentInChildren<Camera>());
            // 动态天气（联动天空、光照、雾、风、海浪、体积光、雨、闪电）
            var weather = new GameObject("Weather").AddComponent<Rishi.Weather.WeatherSystem>();
            weather.config = ConfigAssets.Weather;
            weather.dayNight = Cycle;
            weather.rainMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Generated/Materials/FX_Soft.mat");

            var cinematic = CinematicSetup.CreateCinematicVolume();
            foreach (var cp in Object.FindObjectsByType<CutscenePlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None)) cp.cinematicVolume = cinematic;
        }

        // ================= 地形 =================

        /// <summary>海平面以上的高度（米），神殿周围压平成平台。x, z 为相对岛中心的世界坐标。</summary>
        static float HeightAt(float x, float z)
        {
            var rc = IslandStoryBuilder.RuinCenter;
            float d = Vector2.Distance(new Vector2(x, z), rc);
            float h = RawHeightAt(x, z);
            if (d > IslandStoryBuilder.RuinBlendRadius) return h;
            float flat = RawHeightAt(rc.x, rc.y);
            return Mathf.Lerp(flat, h, SmoothStep(IslandStoryBuilder.RuinFlatRadius, IslandStoryBuilder.RuinBlendRadius, d));
        }

        static float RawHeightAt(float x, float z, bool carveRiver = true)
        {
            float warp = (Fbm(x * 0.005f + 11.3f, z * 0.005f + 7.1f, 4) - 0.5f) * 110f;
            float r = Mathf.Sqrt(x * x + z * 1.15f * z * 1.15f) + warp;
            float coast = 1f - SmoothStep(150f, 240f, r); // 1 = 内陆，0 = 外海

            // 海床 -> 浅滩 -> 沙滩 -> 后滩
            float h = Mathf.Lerp(-18f, -1.2f, SmoothStep(0f, 0.32f, coast))
                    + Mathf.Lerp(0f, 3.8f, SmoothStep(0.26f, 0.62f, coast));
            h = MarineBuilder.ShapeSeabed(x, z, h); // 海底：沙波、礁盘、外缘陡坡

            // 沙滩后面是低矮起伏的丛林丘陵（最高十几米），没有高山
            float inland = SmoothStep(0.62f, 1f, coast);
            float hills = (Fbm(x * 0.011f + 3.7f, z * 0.011f + 1.9f, 5) - 0.4f) * 13f;
            float ridge = 1f - Mathf.Abs(Fbm(x * 0.0055f + 21f, z * 0.0055f + 5f, 4) * 2f - 1f);
            float knoll = 9f * Mathf.Exp(-((x - 35f) * (x - 35f) + (z - 80f) * (z - 80f)) / (2f * 70f * 70f)); // 岛中央一片缓丘
            float inlandBonus = inland * inland * (Mathf.Max(hills, -1.5f) + ridge * ridge * 5f + knoll);
            h += inlandBonus;

            // 分区地貌：北岸海崖、东岸沼泽、西侧溪谷
            h = IslandRegions.Apply(x, z, h, coast, inlandBonus, carveRiver);
            return Mathf.Clamp(h, MinHeight + 0.5f, MinHeight + HeightRange - 0.5f);
        }

        static Terrain CreateTerrain(out Texture2D heightTex)
        {
            var data = new TerrainData
            {
                heightmapResolution = HeightRes,
                alphamapResolution = AlphaRes,
                baseMapResolution = 1024,
            };
            data.size = new Vector3(Size, HeightRange, Size);
            // 先存成资产再写入数据：之后再 CreateAsset 会把已绘制的地表权重重置掉
            SaveAsset(data, $"{Gen}/Terrain/IslandTerrain.asset");
            data.SetDetailResolution(DetailRes, 32);

            IslandRegions.PrepareRiver((x, z) => RawHeightAt(x, z, carveRiver: false)); // 河床跟着真实地形走
            var heights = new float[HeightRes, HeightRes];
            for (int zi = 0; zi < HeightRes; zi++)
                for (int xi = 0; xi < HeightRes; xi++)
                {
                    float x = (xi / (float)(HeightRes - 1) - 0.5f) * Size;
                    float z = (zi / (float)(HeightRes - 1) - 0.5f) * Size;
                    heights[zi, xi] = (HeightAt(x, z) - MinHeight) / HeightRange;
                }
            data.SetHeights(0, 0, heights);

            // 海洋着色器用的高度图（判断水深：岸边减弱波浪）
            const int hr = 513;
            heightTex = new Texture2D(hr, hr, TextureFormat.RFloat, false, true) { wrapMode = TextureWrapMode.Clamp, name = "IslandHeight" };
            var px = new Color[hr * hr];
            for (int zi = 0; zi < hr; zi++)
                for (int xi = 0; xi < hr; xi++)
                    px[zi * hr + xi] = new Color(data.GetInterpolatedHeight(xi / (hr - 1f), zi / (hr - 1f)) / HeightRange, 0, 0, 0);
            heightTex.SetPixels(px);
            heightTex.Apply();
            SaveAsset(heightTex, $"{Gen}/Terrain/IslandHeight.asset");

            data.terrainLayers = new[]
            {
                Layer("damp_beach_sand", 4f, Color.white),                          // 0 湿沙
                Layer("aerial_beach_01", 7f, Color.white),                          // 1 干沙
                Pack("Grass_Moss_TerrainLayer") ?? Layer("leafy_grass", 3.5f, Color.white),   // 2 草地（官方样例包的苔藓草地）
                Layer("forest_leaves_02", 3f, Color.white),      // 3 丛林落叶（潮湿、偏暗）
                Pack("Rock_TerrainLayer") ?? Layer("rock_face_03", 9f, new Color(0.85f, 0.85f, 0.82f)), // 4 岩壁
                Pack("Muddy_TerrainLayer") ?? Layer("brown_mud_leaves_01", 3f, Color.white),   // 5 沼泽泥地
                Pack("Pebbles_A_TerrainLayer") ?? Layer("river_small_rocks", 2.5f, Color.white), // 6 河床卵石
                Layer("mossy_rock", 5f, Color.white),                              // 7 青苔岩（丛林里的陡坡）
                Pack("Soil_Rocks_TerrainLayer") ?? Layer("aerial_rocks_02", 10f, Color.white),  // 8 碎石土
                Pack("Tidal_Pools_TerrainLayer") ?? Layer("coast_sand_rocks_02", 4f, Color.white), // 9 崖下礁石滩（潮池）
            };
            PaintSplat(data);
            EditorUtility.SetDirty(data);

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "Island";
            go.transform.position = new Vector3(-Size / 2, MinHeight, -Size / 2);
            go.isStatic = true;
            var terrain = go.GetComponent<Terrain>();
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 3f;
            terrain.basemapDistance = 250f;
            terrain.detailObjectDistance = 90f;
            terrain.detailObjectDensity = 1f;
            terrain.shadowCastingMode = ShadowCastingMode.TwoSided;

            var mat = MakeMaterial("IslandTerrain", "Universal Render Pipeline/Terrain/Lit");
            mat.SetFloat("_EnableHeightBlend", 1f);
            mat.SetFloat("_HeightTransition", 0.25f);
            mat.EnableKeyword("_TERRAIN_BLEND_HEIGHT");
            mat.SetFloat("_EnableInstancedPerPixelNormal", 1f);
            mat.EnableKeyword("_TERRAIN_INSTANCED_PERPIXEL_NORMAL");
            mat.EnableKeyword("_NORMALMAP");
            mat.EnableKeyword("_MASKMAP");
            EditorUtility.SetDirty(mat);
            terrain.materialTemplate = mat;
            return terrain;
        }

        const string SamplePack = "Assets/TerrainSampleAssets";

        /// <summary>Unity 官方 Terrain Sample Asset Pack 里的地表图层（用户从 Asset Store 下载）；没导入时返回 null。</summary>
        static TerrainLayer Pack(string name) => AssetDatabase.LoadAssetAtPath<TerrainLayer>($"{SamplePack}/TerrainLayers/{name}.terrainlayer");

        static TerrainLayer Layer(string id, float tile, Color tint)
        {
            string dir = $"{Art}/Terrain/{id}";
            string path = $"{Gen}/Terrain/{id}.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, path);
            }
            layer.diffuseTexture = FPSSceneBuilder.FindTex(dir, id + "_albedo");
            layer.normalMapTexture = FPSSceneBuilder.FindTex(dir, id + "_normal");
            layer.maskMapTexture = FPSSceneBuilder.FindTex(dir, id + "_mask");
            layer.normalScale = 1f;
            layer.tileSize = new Vector2(tile, tile);
            layer.smoothness = 1f;
            layer.metallic = 0f;
            layer.maskMapRemapMin = Vector4.zero;
            layer.maskMapRemapMax = Vector4.one;
            layer.diffuseRemapMin = Vector4.zero;
            layer.diffuseRemapMax = new Vector4(tint.r, tint.g, tint.b, 1f);
            EditorUtility.SetDirty(layer);
            return layer;
        }

        const int LayerCount = 10;

        static void PaintSplat(TerrainData data)
        {
            int res = data.alphamapResolution;
            var maps = new float[res, res, LayerCount];
            var w = new float[LayerCount];
            for (int zi = 0; zi < res; zi++)
                for (int xi = 0; xi < res; xi++)
                {
                    float u = xi / (res - 1f), v = zi / (res - 1f);
                    float x = (u - 0.5f) * Size, z = (v - 0.5f) * Size;
                    float h = data.GetInterpolatedHeight(u, v) + MinHeight;
                    float slope = data.GetSteepness(u, v);
                    float n = Mathf.PerlinNoise(x * 0.06f + 100, z * 0.06f + 100) - 0.5f;
                    float n2 = Mathf.PerlinNoise(x * 0.015f + 40, z * 0.015f + 70);
                    var region = IslandRegions.At(x, z);

                    float wet = 1f - SmoothStep(-0.3f, 0.9f, h + n * 0.6f);
                    float veg = SmoothStep(2.4f, 4.2f, h + n * 2f);
                    float dry = (1f - wet) * (1f - veg);
                    float forest = veg * SmoothStep(0.05f, 0.3f, n2 + SmoothStep(3f, 7f, h) * 0.6f); // 几乎全岛都是雨林，只留零星林间空地
                    float grass = veg - forest;
                    float rock = SmoothStep(26f, 38f, slope + n * 8f);

                    for (int l = 0; l < LayerCount; l++) w[l] = 0;
                    w[0] = wet; w[1] = dry; w[2] = grass; w[3] = forest;

                    // 北岸：崖下是礁石滩，不是白沙
                    float shore = region.cliff * (1f - veg);
                    w[9] = shore * 1.5f;
                    w[0] *= 1f - region.cliff; w[1] *= 1f - region.cliff;

                    // 东岸沼泽：低处全是泥地
                    float mud = region.swamp * (1f - SmoothStep(2f, 4.5f, h + n * 2f));
                    w[5] = mud * 1.6f;
                    for (int l = 0; l <= 3; l++) w[l] *= 1f - mud * 0.9f;

                    // 溪谷：河床铺卵石
                    float bed = (1f - SmoothStep(IslandRegions.RiverHalfWidth, IslandRegions.RiverHalfWidth + 3f, region.riverDist + n * 2f)) * SmoothStep(-1.5f, 0f, h);
                    w[6] = bed * 2f;
                    for (int l = 0; l <= 3; l++) w[l] *= 1f - bed;

                    // 陡坡：海崖和高处是裸岩，丛林里是青苔岩
                    float high = SmoothStep(30f, 40f, h) + region.cliff * 0.8f;
                    w[4] = rock * Mathf.Clamp01(high) ;
                    w[7] = rock * (1f - Mathf.Clamp01(high));
                    w[8] = SmoothStep(42f, 52f, h + n * 6f) * 1.5f + rock * SmoothStep(46f, 56f, h);
                    float k = 1f - Mathf.Clamp01(rock + w[8]);
                    for (int l = 0; l <= 3; l++) w[l] *= k;
                    w[5] *= 1f - rock; w[6] *= 1f - rock * 0.5f; w[9] *= 1f - rock * 0.5f;

                    // 水下：白沙为主，礁盘是礁石，零星的碎石带
                    float uw = 1f - SmoothStep(-1.6f, -0.7f, h);
                    if (uw > 0f)
                    {
                        float reef = MarineBuilder.ReefMask(x, z);
                        float gravel = SmoothStep(0.62f, 0.75f, Mathf.PerlinNoise(x * 0.05f + 3f, z * 0.05f + 8f));
                        for (int l = 0; l < LayerCount; l++) w[l] *= 1f - uw;
                        w[1] += uw * (1f - reef) * (1f - gravel * 0.5f);
                        w[9] += uw * reef * 1.3f;
                        w[6] += uw * gravel * 0.5f * (1f - reef);
                    }

                    float sum = 0;
                    for (int l = 0; l < LayerCount; l++) sum += w[l];
                    for (int l = 0; l < LayerCount; l++) maps[zi, xi, l] = w[l] / Mathf.Max(sum, 1e-4f);
                }
            data.SetAlphamaps(0, 0, maps);
        }

        static Vector3 FindBeachSpawn(Terrain terrain)
        {
            // 从南边外海往岛中心走，找到第一块高出海面 1.5 米的沙滩
            float baseY = terrain.transform.position.y;
            for (float z = -Size / 2 + 5; z < 0; z += 0.5f)
            {
                if (terrain.SampleHeight(new Vector3(0, 0, z)) + baseY > 1.5f)
                {
                    var p = new Vector3(0, 0, z + 4f);
                    p.y = terrain.SampleHeight(p) + baseY;
                    return p;
                }
            }
            return new Vector3(0, 5, 0);
        }

        // ================= 海洋 =================

        static void CreateOcean(Texture2D heightTex)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{Gen}/Meshes/OceanGrid.asset");
            if (mesh == null)
            {
                mesh = BuildOceanMesh(256, 3000f);
                AssetDatabase.CreateAsset(mesh, $"{Gen}/Meshes/OceanGrid.asset");
            }

            var mat = MakeMaterial("Ocean", "Island/Ocean");
            mat.SetTexture("_NormalMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Art}/Water/water_normal.png"));
            mat.SetTexture("_IslandHeight", heightTex);
            mat.SetVector("_IslandRect", new Vector4(-Size / 2, -Size / 2, Size, Size));
            mat.SetVector("_IslandHeightRange", new Vector4(MinHeight, HeightRange, 0, 0));
            mat.SetFloat("_NormalStrength", 0.55f);
            mat.SetColor("_DeepColor", new Color(0.03f, 0.24f, 0.32f));
            mat.SetFloat("_ScatterDensity", 0.14f);
            mat.SetFloat("_WaveScale", 0.85f);
            EditorUtility.SetDirty(mat);

            var go = new GameObject("Ocean");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            go.AddComponent<OceanFollow>();
        }

        /// <summary>中心密、外围疏的方形网格（顶点间距随距离平方增长），半径 radius 米。</summary>
        static Mesh BuildOceanMesh(int n, float radius)
        {
            var verts = new Vector3[(n + 1) * (n + 1)];
            for (int j = 0; j <= n; j++)
                for (int i = 0; i <= n; i++)
                {
                    float u = i / (float)n * 2 - 1, v = j / (float)n * 2 - 1;
                    verts[j * (n + 1) + i] = new Vector3(Mathf.Sign(u) * u * u * radius, 0, Mathf.Sign(v) * v * v * radius);
                }
            var tris = new int[n * n * 6];
            int t = 0;
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    int a = j * (n + 1) + i, b = a + 1, c = a + n + 1, d = c + 1;
                    tris[t++] = a; tris[t++] = c; tris[t++] = b;
                    tris[t++] = b; tris[t++] = c; tris[t++] = d;
                }
            var mesh = new Mesh { name = "OceanGrid", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(radius * 2, 40, radius * 2));
            return mesh;
        }

        // ================= 植被 =================

        static void PlaceTrees(Terrain terrain)
        {
            var parent = new GameObject("Trees").transform;
            var prefabs = new[] { TreePrefab("island_tree_01"), TreePrefab("island_tree_02") };
            var placed = new List<Vector3>();
            int attempts = 0;
            while (placed.Count < 220 && attempts++ < 80000)
            {
                float x = Random.Range(-Size / 2 + 20, Size / 2 - 20), z = Random.Range(-Size / 2 + 20, Size / 2 - 20);
                if (!Sample(terrain, x, z, out float h, out float slope, out float[] w)) continue;
                if (IslandStoryBuilder.InClearZone(x, z, 3f)) continue; // 沉船、神殿、出生点周围不长树不放石头
                if (h < 3.0f || slope > 32f) continue;
                if (w[6] > 0.25f || w[8] > 0.4f || w[9] > 0.3f) continue; // 河床、山顶、崖下礁石滩不长树
                float chance = w[3] * 0.95f + w[2] * 0.12f; // 丛林密，草地稀
                if (Random.value > chance) continue;
                var p = new Vector3(x, h, z);
                if (placed.Any(q => (q - p).sqrMagnitude < 5.5f * 5.5f)) continue;
                placed.Add(p);

                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[Random.Range(0, prefabs.Length)], parent);
                float s = Random.Range(1.9f, 2.7f); // 雨林大树，10~14 米
                go.transform.SetPositionAndRotation(p - Vector3.up * 0.15f, Quaternion.Euler(Random.Range(-3f, 3f), Random.Range(0f, 360f), Random.Range(-3f, 3f)));
                go.transform.localScale = Vector3.one * s;
                go.isStatic = true;
            }
            Debug.Log($"[Island] trees: {placed.Count}");
        }

        /// <summary>雨林冠层：上千棵轻量的程序化大树，林冠基本连成一片。</summary>
        static void PlaceCanopy(Terrain terrain)
        {
            var parent = new GameObject("Canopy").transform;
            var prefabs = Enumerable.Range(0, 6).Select(JunglePlants.CanopyTree).ToArray();
            // 用网格哈希做最小间距检查，上千棵也很快
            var grid = new Dictionary<(int, int), List<Vector3>>();
            const float minDist = 4.5f;
            bool TooClose(Vector3 p)
            {
                int gx = Mathf.FloorToInt(p.x / minDist), gz = Mathf.FloorToInt(p.z / minDist);
                for (int dx = -1; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                        if (grid.TryGetValue((gx + dx, gz + dz), out var list) && list.Any(q => (q - p).sqrMagnitude < minDist * minDist)) return true;
                return false;
            }
            int count = 0, attempts = 0;
            while (count < 1500 && attempts++ < 120000)
            {
                float x = Random.Range(-Size / 2 + 15, Size / 2 - 15), z = Random.Range(-Size / 2 + 15, Size / 2 - 15);
                if (!Sample(terrain, x, z, out float h, out float slope, out float[] w)) continue;
                if (IslandStoryBuilder.InClearZone(x, z, 3f)) continue;
                if (h < 3.2f || slope > 34f || w[3] < 0.45f || w[6] > 0.2f || w[5] > 0.4f) continue;
                if (Random.value > w[3]) continue;
                var p = new Vector3(x, h, z);
                if (TooClose(p)) continue;
                var key = (Mathf.FloorToInt(x / minDist), Mathf.FloorToInt(z / minDist));
                if (!grid.TryGetValue(key, out var cell)) grid[key] = cell = new List<Vector3>();
                cell.Add(p);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[Random.Range(0, prefabs.Length)], parent);
                go.transform.SetPositionAndRotation(p - Vector3.up * 0.2f, Quaternion.Euler(0, Random.Range(0f, 360f), 0));
                go.transform.localScale = Vector3.one * Random.Range(0.8f, 1.2f);
                go.isStatic = true;
                count++;
            }
            Debug.Log($"[Island] canopy trees: {count}");
        }

        /// <summary>椰子树：沿海岸成片生长、向海倾斜；内陆零星分布。</summary>
        static void PlacePalms(Terrain terrain)
        {
            var parent = new GameObject("Palms").transform;
            var prefabs = Enumerable.Range(0, 6).Select(JunglePlants.Palm).ToArray();
            var placed = new List<Vector3>();
            int coastal = 0, inland = 0, attempts = 0;
            while ((coastal < 170 || inland < 50) && attempts++ < 60000)
            {
                float x = Random.Range(-Size / 2 + 15, Size / 2 - 15), z = Random.Range(-Size / 2 + 15, Size / 2 - 15);
                if (!Sample(terrain, x, z, out float h, out float slope, out float[] w)) continue;
                if (IslandStoryBuilder.InClearZone(x, z, 1f) || slope > 24f) continue;
                if (w[9] > 0.2f || w[5] > 0.3f || w[8] > 0.3f) continue; // 崖下礁石滩、沼泽、山顶不长椰子
                bool isCoast = h > 1.6f && h < 6.5f;
                bool isInland = h >= 6.5f && h < 30f && w[4] < 0.3f;
                if (isCoast ? coastal >= 170 : !isInland || inland >= 50) continue;
                var p = new Vector3(x, h, z);
                if (placed.Any(q => (q - p).sqrMagnitude < 4.5f * 4.5f)) continue;
                placed.Add(p);
                if (isCoast) coastal++; else inland++;

                // 向下坡（海的方向）倾斜
                float hx = terrain.SampleHeight(new Vector3(x + 2, 0, z)) - terrain.SampleHeight(new Vector3(x - 2, 0, z));
                float hz = terrain.SampleHeight(new Vector3(x, 0, z + 2)) - terrain.SampleHeight(new Vector3(x, 0, z - 2));
                Vector3 down = new Vector3(-hx, 0, -hz);
                float yaw = down.sqrMagnitude > 1e-4f && isCoast ? Mathf.Atan2(down.x, down.z) * Mathf.Rad2Deg + Random.Range(-35f, 35f) : Random.Range(0f, 360f);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[Random.Range(0, prefabs.Length)], parent);
                go.transform.SetPositionAndRotation(p - Vector3.up * 0.1f, Quaternion.Euler(0, yaw, 0));
                go.transform.localScale = Vector3.one * Random.Range(0.85f, 1.15f);
                go.isStatic = true;
            }
            Debug.Log($"[Island] palms: coast {coastal}, inland {inland}");
        }

        /// <summary>把 FBX 做成带我们 URP 材质和碰撞体的树预制体。</summary>
        internal static GameObject TreePrefab(string id)
        {
            string path = $"{Gen}/Prefabs/{id}.prefab";
            string folder = $"Models/{id}";
            var trunk = FPSSceneBuilder.Pbr($"{id}_trunk", folder, 1f);
            var branches = PbrPrefix($"{id}_branches", folder, id + "_branches_", false);
            var leaves = PbrPrefix($"{id}_leaves", folder, id + "_leaves_", true);
            leaves.SetColor("_BaseColor", new Color(0.7f, 1f, 0.55f)); // 原贴图偏灰绿，压成湿润的热带绿

            var root = new GameObject(id);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(FPSSceneBuilder.LoadModel(folder));
            model.transform.SetParent(root.transform, false);
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterials = r.sharedMaterials.Select(m =>
                    m == null ? trunk : m.name.EndsWith("_leaves") ? leaves : m.name.EndsWith("_branches") ? branches : trunk).ToArray();
                r.shadowCastingMode = ShadowCastingMode.On;
                // 自动减面会把细小的叶片面片消掉，偏向高精度的 LOD
                if (r is MeshRenderer mr) mr.meshLodSelectionBias = -3f;
            }
            // 树干碰撞：底部 3 米的胶囊
            var col = root.AddComponent<CapsuleCollider>();
            col.radius = 0.28f;
            col.height = 3f;
            col.center = new Vector3(0, 1.5f, 0);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static void PlaceRocks(Terrain terrain)
        {
            var parent = new GameObject("Rocks").transform;
            var mat = PbrPrefix("rock_moss_set_01", "Models/rock_moss_set_01", "rock_moss_set_01_", false);
            var model = FPSSceneBuilder.LoadModel("Models/rock_moss_set_01");
            var rocks = model.GetComponentsInChildren<MeshFilter>().Select(mf => mf.transform).ToArray();

            int count = 0, attempts = 0;
            while (count < 90 && attempts++ < 20000)
            {
                float x = Random.Range(-Size / 2 + 20, Size / 2 - 20), z = Random.Range(-Size / 2 + 20, Size / 2 - 20);
                if (!Sample(terrain, x, z, out float h, out float slope, out float[] w)) continue;
                if (IslandStoryBuilder.InClearZone(x, z, 3f)) continue; // 沉船、神殿、出生点周围不长树不放石头
                if (h < -1.5f) continue;
                float chance = w[4] * 0.9f + w[7] * 0.6f + w[8] * 0.4f + w[6] * 0.25f + w[9] * 0.3f + w[3] * 0.12f + (h < 2f ? 0.08f : 0.02f);
                if (Random.value > chance) continue;

                var src = rocks[Random.Range(0, rocks.Length)];
                var go = new GameObject(src.name);
                go.transform.SetParent(parent);
                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = src.GetComponent<MeshFilter>().sharedMesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;
                go.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                float s = Random.Range(0.6f, 2.6f) * (w[4] > 0.5f ? 1.6f : 1f);
                go.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0) * src.rotation;
                go.transform.localScale = src.lossyScale * s;
                // 先放到原点算包围盒，再埋进地面 30%
                go.transform.position = Vector3.zero;
                var b = go.GetComponent<Renderer>().bounds;
                go.transform.position = new Vector3(x, h, z) - new Vector3(b.center.x, b.min.y + b.size.y * 0.3f, b.center.z);
                go.isStatic = true;
                count++;
            }
            Debug.Log($"[Island] rocks: {count}");
        }

        static void PaintDetails(Terrain terrain)
        {
            var data = terrain.terrainData;
            var grassMat = PbrPrefix("grass_medium_01", "Models/grass_medium_01", "grass_medium_01_", true);
            var fernMat = PbrPrefix("fern_02", "Models/fern_02", "fern_02_", true);
            var shrubMat = PbrPrefix("shrub_03", "Models/shrub_03", "shrub_03_", true);

            // 草：高丛 + 中丛；蕨类；小灌木
            var grassMeshes = DetailMeshes("grass_medium_01", new[] { "mid_b", "mid_c", "tall_a" }); // 面数适中的草丛
            var fernMeshes = DetailMeshes("fern_02", new[] { "_b", "_c" });
            var shrubMeshes = DetailMeshes("shrub_03", new[] { "_a_", "_b_" });

            var protos = new List<DetailPrototype>();
            var kinds = new List<int>(); // 0 草 1 蕨 2 灌木
            void Add(Mesh m, Material mat, float min, float max, int kind)
            {
                var prefab = DetailPrefab(m, mat, castShadows: kind != 0);
                protos.Add(new DetailPrototype
                {
                    usePrototypeMesh = true,
                    prototype = prefab,
                    renderMode = DetailRenderMode.VertexLit,
                    useInstancing = true,
                    minWidth = min, maxWidth = max, minHeight = min, maxHeight = max,
                    noiseSpread = 0.3f,
                    healthyColor = Color.white,
                    dryColor = new Color(0.85f, 0.8f, 0.65f),
                    alignToGround = 0.6f,
                    density = 1f,
                });
                kinds.Add(kind);
            }
            foreach (var m in grassMeshes) Add(m, grassMat, 2.2f, 3.6f, 0);
            foreach (var m in fernMeshes) Add(m, fernMat, 1.5f, 2.6f, 1);
            foreach (var m in shrubMeshes) Add(m, shrubMat, 2.0f, 3.5f, 2);
            for (int i = 0; i < 3; i++) { var (bm, bmat) = JunglePlants.Banana(i); Add(bm, bmat, 0.8f, 1.25f, 3); }
            for (int i = 0; i < 3; i++) { var (tm, tmat) = JunglePlants.Taro(i); Add(tm, tmat, 0.8f, 1.3f, 4); }
            for (int i = 0; i < 4; i++) { var (sm, smat) = JunglePlants.TallShrub(i); Add(sm, smat, 0.85f, 1.25f, 5); } // 高大灌丛
            // 官方样例包（用户下载）：灌木、蕨、宽叶植物，直接用它们的预制体和材质
            foreach (var (name, min, max, kind) in new[] { ("Bush_A", 1.2f, 2.0f, 6), ("Bush_B", 1.2f, 2.0f, 6), ("Plant_A", 1f, 1.6f, 7), ("Plant_B", 1f, 1.6f, 7), ("Plant_C", 1f, 1.6f, 7), ("Plant_D", 1f, 1.6f, 7), ("Fern_A", 1.1f, 1.8f, 1), ("Fern_B", 1.1f, 1.8f, 1) })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{SamplePack}/Prefabs/{name}.prefab");
                if (prefab == null || prefab.GetComponent<MeshFilter>() == null) continue;
                protos.Add(new DetailPrototype { usePrototypeMesh = true, prototype = prefab, renderMode = DetailRenderMode.VertexLit, useInstancing = true,
                    minWidth = min, maxWidth = max, minHeight = min, maxHeight = max, noiseSpread = 0.4f, healthyColor = Color.white, dryColor = new Color(0.9f, 0.9f, 0.8f), alignToGround = 0.5f, density = 1f });
                kinds.Add(kind);
            }
            data.detailPrototypes = protos.ToArray();

            int res = data.detailResolution;
            var layers = new int[protos.Count][,];
            for (int p = 0; p < protos.Count; p++) layers[p] = new int[res, res];
            var alpha = data.GetAlphamaps(0, 0, data.alphamapResolution, data.alphamapResolution);
            int ar = data.alphamapResolution;
            var rng = new System.Random(Seed);

            for (int zi = 0; zi < res; zi++)
                for (int xi = 0; xi < res; xi++)
                {
                    int ax = xi * (ar - 1) / (res - 1), az = zi * (ar - 1) / (res - 1);
                    float grassW = alpha[az, ax, 2], forestW = alpha[az, ax, 3], rockW = alpha[az, ax, 4] + alpha[az, ax, 7];
                    if (rockW > 0.4f || alpha[az, ax, 6] > 0.4f || alpha[az, ax, 8] > 0.5f || alpha[az, ax, 9] > 0.5f) continue;
                    forestW += alpha[az, ax, 5] * 0.5f; // 泥地边缘也长海芋、蕨类
                    float u = xi / (res - 1f), v = zi / (res - 1f);
                    if (IslandStoryBuilder.InClearZone((u - 0.5f) * Size, (v - 0.5f) * Size, -4f)) continue;
                    float clump = Mathf.PerlinNoise(u * 60f, v * 60f);
                    for (int p = 0; p < protos.Count; p++)
                    {
                        // 每格（约 0.6 米见方）的期望株数
                        float d = kinds[p] switch
                        {
                            0 => grassW * (0.3f + clump) * 0.45f + forestW * 0.05f,
                            1 => forestW * SmoothStep(0.25f, 0.65f, clump) * 0.45f,
                            3 => forestW * SmoothStep(0.45f, 0.75f, Mathf.PerlinNoise(u * 23f + 7f, v * 23f)) * 0.06f,
                            4 => forestW * SmoothStep(0.35f, 0.7f, Mathf.PerlinNoise(u * 31f, v * 31f + 3f)) * 0.08f,
                            5 => forestW * SmoothStep(0.3f, 0.65f, Mathf.PerlinNoise(u * 18f + 11f, v * 18f)) * 0.06f,
                            6 => (forestW + grassW * 0.5f) * SmoothStep(0.4f, 0.7f, Mathf.PerlinNoise(u * 26f + 5f, v * 26f + 9f)) * 0.05f,
                            7 => forestW * SmoothStep(0.35f, 0.7f, Mathf.PerlinNoise(u * 34f + 2f, v * 34f + 6f)) * 0.06f,
                            _ => (grassW + forestW) * SmoothStep(0.6f, 0.85f, clump) * 0.06f,
                        };
                        int count = (int)d + (rng.NextDouble() < d % 1 ? 1 : 0);
                        layers[p][zi, xi] = count;
                    }
                }
            for (int p = 0; p < protos.Count; p++) data.SetDetailLayer(0, 0, p, layers[p]);
            EditorUtility.SetDirty(data);
        }

        /// <summary>
        /// 把 FBX 里的子网格烘焙成独立网格：应用节点的旋转缩放，枢轴移到底部中心（地形细节系统只用网格本身，不看节点变换）。
        /// </summary>
        static Mesh[] DetailMeshes(string id, string[] nameParts)
        {
            var model = FPSSceneBuilder.LoadModel($"Models/{id}");
            var result = new List<Mesh>();
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
            {
                if (!nameParts.Any(mf.name.Contains)) continue;
                string path = $"{Gen}/Meshes/{mf.name}.asset";
                var baked = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (baked == null)
                {
                    var src = mf.sharedMesh;
                    var m = Matrix4x4.TRS(Vector3.zero, mf.transform.rotation, mf.transform.lossyScale);
                    var verts = src.vertices.Select(v => m.MultiplyPoint3x4(v)).ToArray();
                    var min = verts.Aggregate(Vector3.Min);
                    var max = verts.Aggregate(Vector3.Max);
                    var pivot = new Vector3((min.x + max.x) / 2, min.y, (min.z + max.z) / 2);
                    for (int i = 0; i < verts.Length; i++) verts[i] -= pivot;

                    baked = new Mesh { name = mf.name };
                    baked.vertices = verts;
                    baked.normals = src.normals.Select(n => m.MultiplyVector(n).normalized).ToArray();
                    baked.uv = src.uv;
                    baked.triangles = src.triangles;
                    baked.RecalculateTangents();
                    baked.RecalculateBounds();
                    AssetDatabase.CreateAsset(baked, path);
                }
                result.Add(baked);
            }
            return result.ToArray();
        }

        static GameObject DetailPrefab(Mesh mesh, Material mat, bool castShadows)
        {
            string path = $"{Gen}/Prefabs/Detail_{mesh.name}.prefab";
            var go = new GameObject(mesh.name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        // ================= 工具 =================

        /// <summary>带前缀贴图组的 PBR 材质；cutout 为 true 时是双面 Alpha 裁剪（叶片、草）。</summary>
        internal static Material PbrPrefix(string name, string folder, string prefix, bool cutout)
        {
            string dir = $"{Art}/{folder}";
            var mat = MakeMaterial(name, "Universal Render Pipeline/Lit");
            mat.SetTexture("_BaseMap", FPSSceneBuilder.FindTex(dir, prefix + "albedo"));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_BumpMap", FPSSceneBuilder.FindTex(dir, prefix + "normal"));
            mat.EnableKeyword("_NORMALMAP");
            mat.SetTexture("_MetallicGlossMap", FPSSceneBuilder.FindTex(dir, prefix + "ms"));
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            mat.SetFloat("_Smoothness", cutout ? 0.6f : 1f);
            mat.SetTexture("_OcclusionMap", FPSSceneBuilder.FindTex(dir, prefix + "ao"));
            mat.EnableKeyword("_OCCLUSIONMAP");
            if (cutout)
            {
                mat.SetFloat("_AlphaClip", 1f);
                mat.SetFloat("_Cutoff", 0.45f);
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.SetFloat("_Cull", 0f);
                mat.SetOverrideTag("RenderType", "TransparentCutout");
                mat.renderQueue = (int)RenderQueue.AlphaTest;
            }
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        internal static Material MakeMaterial(string name, string shaderName)
        {
            string path = $"{Gen}/Materials/{name}.mat";
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new System.Exception($"找不到着色器 {shaderName}");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else mat.shader = shader;
            return mat;
        }

        /// <summary>萤火虫：加法混合的 HDR 粒子材质，能触发泛光。</summary>
        static Material FireflyMaterial()
        {
            var mat = MakeMaterial("Firefly", "Universal Render Pipeline/Particles/Unlit");
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Generated/Textures/soft_dot.png"));
            mat.SetColor("_BaseColor", new Color(2.2f, 3f, 0.8f, 1f));
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 2f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.One);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void SaveAsset(Object obj, string path)
        {
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(obj, path);
        }

        internal static bool Sample(Terrain terrain, float x, float z, out float height, out float slope, out float[] weights)
        {
            var data = terrain.terrainData;
            float u = x / Size + 0.5f, v = z / Size + 0.5f;
            height = data.GetInterpolatedHeight(u, v) + MinHeight;
            slope = data.GetSteepness(u, v);
            int ar = data.alphamapResolution;
            var a = data.GetAlphamaps(Mathf.Clamp((int)(u * (ar - 1)), 0, ar - 1), Mathf.Clamp((int)(v * (ar - 1)), 0, ar - 1), 1, 1);
            weights = new float[a.GetLength(2)];
            for (int l = 0; l < weights.Length; l++) weights[l] = a[0, 0, l];
            return u > 0 && u < 1 && v > 0 && v < 1;
        }

        static void ConfigurePipelineForIsland()
        {
            var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            if (rp == null) return;
            var so = new SerializedObject(rp);
            void Set(string name, System.Action<SerializedProperty> set)
            {
                var p = so.FindProperty(name);
                if (p != null) set(p); else Debug.LogWarning($"[Island] URP 资产没有属性 {name}");
            }
            Set("m_RequireDepthTexture", p => p.boolValue = true);   // 海面算水深
            Set("m_RequireOpaqueTexture", p => p.boolValue = true);  // 海面折射
            Set("m_OpaqueDownsampling", p => p.intValue = 0);
            Set("m_ShadowDistance", p => p.floatValue = 140f);
            so.ApplyModifiedProperties();
        }

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
