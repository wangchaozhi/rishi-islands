using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
    /// 荒岛场景离线预览：不同机位 × 不同时间，截图写到项目根目录 Screenshots/Island/。
    /// 命令行 -executeMethod Rishi.Builders.IslandPreview.BuildAndCapture（先重建再截图）或 IslandPreview.Capture。
    /// </summary>
    public static class IslandPreview
    {
        public static void BuildAndCapture()
        {
            IslandBuilder.Build();
            Capture();
            CutscenePreview.Capture(IslandBuilder.ScenePath, "CG02_WakeUp", new[] { 1.5f, 5f, 8.8f, 12f, 17.3f, 21f, 24.9f });
            CutscenePreview.Capture(IslandBuilder.ScenePath, "CG01_Storm", new[] { 2.5f, 7f, 11.2f, 13.5f, 16.6f, 18.2f });
            CutscenePreview.Capture(IslandBuilder.ScenePath, "CG03_StoneDoor", new[] { 1f, 3.5f, 6f });
            CutscenePreview.Capture(IslandBuilder.ScenePath, "CG04_FirstHeart", new[] { 1f, 3f });
            CutscenePreview.Capture(IslandBuilder.ScenePath, "CG05_Nightfall", new[] { 1.5f, 6f, 9.5f });
        }

        /// <summary>只截 CG03 / CG04 / CG05 的画面（改了神殿过场后快速检查用）。</summary>
        public static void CaptureTempleCutscenes()
        {
            CutscenePreview.Capture(IslandBuilder.ScenePath, "CG03_StoneDoor", new[] { 1f, 3.5f, 6f });
            CutscenePreview.Capture(IslandBuilder.ScenePath, "CG04_FirstHeart", new[] { 1f, 3f });
            CutscenePreview.Capture(IslandBuilder.ScenePath, "CG05_Nightfall", new[] { 1.5f, 6f, 9.5f });
            CaptureSetSail();
        }

        /// <summary>只截 CG06 启航。</summary>
        public static void CaptureSetSail() => CutscenePreview.Capture(IslandBuilder.ScenePath, "CG06_SetSail", new[] { 1.5f, 5.5f, 9f });

        [MenuItem("Tools/Island/渲染预览截图")]
        public static void Capture()
        {
            EditorSceneManager.OpenScene(IslandBuilder.ScenePath);
            var player = GameObject.Find("Player").transform;
            var root = player.Find("CameraRoot");
            var cam = root.GetComponentInChildren<Camera>();
            var cycle = Object.FindFirstObjectByType<DayNightCycle>();
            var ocean = Object.FindFirstObjectByType<OceanFollow>();
            var terrain = IslandBuilder.MainTerrain;
            Vector3 spawn = player.position;

            Directory.CreateDirectory("Screenshots/Island");
            float Ground(float x, float z) => terrain.SampleHeight(new Vector3(x, 0, z)) + terrain.transform.position.y + 1.05f;

            float spawnYaw = player.eulerAngles.y;
            var wreck = GameObject.Find("Shipwreck").transform.position;
            var temple = GameObject.Find("SunTemple").transform;
            Vector3 Toward(Vector3 from, Vector3 to) => new Vector3(0, Mathf.Atan2(to.x - from.x, to.z - from.z) * Mathf.Rad2Deg, 0);

            Shot(cam, player, root, cycle, ocean, spawn, spawnYaw, 3f, 9f, "01_spawn_wreck");
            Shot(cam, player, root, cycle, ocean, spawn, spawnYaw - 70f, -2f, 7.0f, "00_sunrise_wakeup");
            var nearWreck = (wreck + (spawn - wreck).normalized * 14f + Vector3.left * 4f).WithGround(Ground);
            Shot(cam, player, root, cycle, ocean, nearWreck, Toward(nearWreck, wreck).y, 6f, 16.8f, "02_wreck_close");
            var templeFar = (temple.position + temple.forward * 24f).WithGround(Ground);
            Shot(cam, player, root, cycle, ocean, templeFar, Toward(templeFar, temple.position).y, -4f, 10.5f, "03_temple_far");
            var templeNear = (temple.position + temple.forward * 8.5f).WithGround(Ground) + Vector3.up * 0.7f;
            Shot(cam, player, root, cycle, ocean, templeNear, Toward(templeNear, temple.position).y, 8f, 15f, "04_temple_pillars");
            var camp = GameObject.Find("FatherCamp").transform;
            var campView = (camp.position + camp.forward * 4.5f + camp.right * 1.5f).WithGround(Ground) + Vector3.up * 0.4f;
            Shot(cam, player, root, cycle, ocean, campView, Toward(campView, camp.position).y, 22f, 11f, "05_father_camp");
            Shot(cam, player, root, cycle, ocean, templeFar, Toward(templeFar, temple.position).y, -4f, 18.2f, "06_temple_sunset");
            // 多地貌
            Shot(cam, player, root, cycle, ocean, new Vector3(20, 2.5f, 310), 185f, -10f, 10f, "08_north_cliffs");
            var mangroves = GameObject.Find("Mangroves");
            if (mangroves != null && mangroves.transform.childCount > 0)
            {
                var m0 = mangroves.transform.GetChild(0).position;
                var swampView = (m0 + new Vector3(-12f, 0, 3f)).WithGround(Ground) + Vector3.up * 0.4f;
                Shot(cam, player, root, cycle, ocean, swampView, Toward(swampView, m0).y, 3f, 9.5f, "09_swamp");
            }
            var riverMid = IslandRegions.RiverPath[4];
            var riverView = new Vector3(riverMid.x + 6f, 0, riverMid.y + 7f).WithGround(Ground) + Vector3.up * 0.3f;
            var downstream = IslandRegions.RiverPath[6];
            Shot(cam, player, root, cycle, ocean, riverView, Toward(riverView, new Vector3(downstream.x, 0, downstream.y)).y, 6f, 15.5f, "10_river");

            // 水面细节：瀑布、岸边涌浪、浅水焦散
            var spray = GameObject.Find("WaterfallSpray");
            if (spray != null)
            {
                var ps = spray.GetComponent<ParticleSystem>();
                ps.Simulate(4f, true, true); // 编辑器里粒子不会自己跑，先模拟几秒
                var fallPos = spray.transform.position;
                var fallView = (fallPos + spray.transform.forward * 13f + spray.transform.right * 3f).WithGround(Ground) + Vector3.up * 0.2f;
                Shot(cam, player, root, cycle, ocean, fallView, Toward(fallView, fallPos).y, 8f, 10.5f, "26_waterfall");
            }
            var surfView = new Vector3(spawn.x + 6f, 0, spawn.z + 2f).WithGround(Ground) + Vector3.up * 0.4f;
            Shot(cam, player, root, cycle, ocean, surfView, 200f, 16f, 11f, "27_shore_surf");
            // 浅水：沿出生点往海里走到水深约 1.5 米处，站在水面上往下看
            Vector3 shallow = spawn;
            for (float d = 0; d < 60f; d += 0.5f)
            {
                var sp = spawn + Vector3.back * d;
                if (terrain.SampleHeight(sp) + terrain.transform.position.y < -1.5f) { shallow = sp; break; }
            }
            Shot(cam, player, root, cycle, ocean, new Vector3(shallow.x, 0.9f, shallow.z), 150f, 62f, 12f, "28_shallow_caustics");
            var inJungle = new Vector3(25f, 0, 40f).WithGround(Ground) + Vector3.up * 0.2f;
            Shot(cam, player, root, cycle, ocean, inJungle, 200f, 2f, 11f, "11_deep_jungle");
            Shot(cam, player, root, cycle, ocean, new Vector3(-170, 95, -260), 35f, 22f, 10f, "12_aerial");
            // 太阳与月亮：朝向它们拍（方位来自 DayNightCycle）
            var beach = new Vector3(-170, 60, -260); // 空中开阔视角，避免被树挡住
            float SkyYaw(Vector3 d) => Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float SkyPitch(Vector3 d) => -Mathf.Asin(d.y) * Mathf.Rad2Deg;
            var sky = RenderSettings.skybox;
            float coverage = sky.GetFloat("_CloudCoverage");
            sky.SetFloat("_CloudCoverage", 0.3f); // 少云，确认日月圆盘
            cycle.timeOfDay = 17.1f; cycle.Apply(false);
            var toSun = -cycle.sun.transform.forward;
            Shot(cam, player, root, cycle, ocean, beach, SkyYaw(toSun), SkyPitch(toSun) + 6f, 17.1f, "14_sun_low");
            cycle.timeOfDay = 23.5f; cycle.Apply(false);
            var toMoon = Shader.GetGlobalVector("_IslandMoonDir");
            Shot(cam, player, root, cycle, ocean, beach, SkyYaw(toMoon), Mathf.Max(SkyPitch(toMoon), -45f) * 0.8f, 23.5f, "15_moon_night");
            cycle.timeOfDay = 7.3f; cycle.Apply(false);
            toMoon = Shader.GetGlobalVector("_IslandMoonDir");
            Shot(cam, player, root, cycle, ocean, beach, SkyYaw(toMoon), SkyPitch(toMoon) + 4f, 7.3f, "16_morning_moon");
            sky.SetFloat("_CloudCoverage", coverage);

            // ---------- 水下 ----------
            CaptureUnderwater(cam, player, root, cycle, ocean, terrain, spawn);
            // ---------- 武器 ----------
            CaptureWeapons(cam, player, root, cycle, ocean, spawn, spawnYaw);

            // 地形细节在离线渲染里画不出来，手动摆一圈高灌丛检查外观
            var undergrowth = new GameObject("UndergrowthPreview").transform;
            var shrubCenter = new Vector3(25f, 0, 40f);
            for (int i = 0; i < 26; i++)
            {
                var (mesh, mat) = JunglePlants.TallShrub(i % 4);
                var go = new GameObject("Shrub");
                go.transform.SetParent(undergrowth);
                float a = i * 2.4f, r = 2.5f + (i % 7) * 1.6f;
                go.transform.position = (shrubCenter + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r)).WithGround(Ground) - Vector3.up * 1.05f;
                go.transform.rotation = Quaternion.Euler(0, i * 47f, 0);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            }
            string[] packPlants = { "Bush_A", "Bush_B", "Plant_A", "Plant_B", "Plant_C", "Plant_D", "Fern_A", "Fern_B" };
            for (int i = 0; i < 16; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/TerrainSampleAssets/Prefabs/{packPlants[i % packPlants.Length]}.prefab");
                if (prefab == null) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, undergrowth);
                float a = i * 1.9f + 0.7f, r = 1.8f + (i % 5) * 1.3f;
                go.transform.position = (shrubCenter + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r)).WithGround(Ground) - Vector3.up * 1.05f;
                go.transform.localScale = Vector3.one * 1.4f;
            }
            Shot(cam, player, root, cycle, ocean, shrubCenter.WithGround(Ground) + Vector3.up * 0.2f, 160f, 4f, 11f, "13_undergrowth");
            Object.DestroyImmediate(undergrowth.gameObject);

            // 小动物是运行时生成的，这里摆几只模型看外观
            var wl = new GameObject("WildlifePreview").transform;
            Vector3 fwd = Quaternion.Euler(0, spawnYaw, 0) * Vector3.forward, rightV = Quaternion.Euler(0, spawnYaw, 0) * Vector3.right;
            for (int i = 0; i < 4; i++)
            {
                var crab = Critters.Crab();
                crab.transform.SetParent(wl);
                var p = (spawn + fwd * (1.6f + i * 0.5f) + rightV * (i - 1.5f) * 0.6f).WithGround(Ground) - Vector3.up * 1.03f;
                crab.transform.SetPositionAndRotation(p, Quaternion.Euler(0, i * 70f, 0));
            }
            for (int i = 0; i < 3; i++)
            {
                var b = Critters.Butterfly(i);
                b.transform.SetParent(wl);
                b.transform.position = spawn + fwd * 1.6f + rightV * (i - 1) * 0.35f + Vector3.up * (0.1f + i * 0.08f);
                b.transform.rotation = Quaternion.Euler(-60f, spawnYaw + 180f + i * 30f, 0);
                b.transform.Find("WingL").localRotation = Quaternion.Euler(0, 0, -25);
                b.transform.Find("WingR").localRotation = Quaternion.Euler(0, 0, 25);
            }
            // Living Birds：摆几只鸟检查 URP 材质
            string[] birds = { "lb_crowHQ", "lb_cardinalHQ", "lb_blueJayHQ", "lb_goldFinchHQ" };
            for (int i = 0; i < birds.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/living birds/resources/{birds[i]}.prefab");
                if (prefab == null) continue;
                var bird = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                bird.transform.SetParent(wl);
                var p = (spawn + fwd * (2.6f + i * 0.4f) + rightV * (i - 1.5f) * 0.7f).WithGround(Ground) - Vector3.up * 1.05f;
                bird.transform.SetPositionAndRotation(p, Quaternion.Euler(0, spawnYaw + 150f + i * 40f, 0));
                bird.transform.localScale *= 2.5f; // 放大一点方便看清
            }
            Shot(cam, player, root, cycle, ocean, spawn, spawnYaw, 30f, 10f, "07_wildlife");
            Object.DestroyImmediate(wl.gameObject);
            CaptureWeather(cam, player, root, cycle, ocean, spawn, spawnYaw);
            CaptureFlashlight(cam, player, root, cycle, ocean, spawn, spawnYaw);
            CaptureCampfires(cam, player, root, cycle, ocean);
            CaptureCreature(cam, player, root, cycle, ocean, spawn, spawnYaw);
            CaptureShade(cam, player, root, cycle, ocean, spawn, spawnYaw);
            CaptureFirstNight(cam, player, root, cycle, ocean);
            CaptureBeasts(cam, player, root, cycle, ocean);
            CaptureAtoll(cam, player, root, cycle, ocean, spawn);
            CaptureBoat(cam, player, root, cycle, ocean);
            CaptureTide(cam, player, root, cycle, ocean, spawn, spawnYaw);
            // 砍刀能砍开的灌木丛，和营地旁被灌木围住的补给箱
            var terrainT = IslandBuilder.MainTerrain;
            foreach (var (name, dist, height, file) in new[] { ("Thicket", 4.5f, 1.4f, "53_thicket"), ("HiddenCache", 5.5f, 2.6f, "54_hidden_cache") })
            {
                var go = GameObject.Find(name);
                if (go == null) continue;
                var p = go.transform.position;
                var eye = p + new Vector3(dist * 0.8f, 0, -dist * 0.6f);
                eye.y = terrainT.SampleHeight(eye) + terrainT.transform.position.y + height;
                Shot(cam, player, root, cycle, ocean, eye, Mathf.Atan2(p.x - eye.x, p.z - eye.z) * Mathf.Rad2Deg, Mathf.Atan2(eye.y + 0.7f - (p.y + 0.8f), dist) * Mathf.Rad2Deg, 11f, file);
            }
            // 碎片的代价：同一个黄昏视角，0 块碎片和 4 块碎片时的雾
            foreach (var (n, file) in new[] { (0, "47_dusk_fragments0"), (4, "48_dusk_fragments4") })
            {
                GameSignals.SetFragments(n);
                Shot(cam, player, root, cycle, ocean, spawn, spawnYaw + 150f, -2f, 19.6f, file);
            }
            GameSignals.SetFragments(0);
            Debug.Log("[Island] 预览截图已写入 Screenshots/Island/");
        }

        /// <summary>在出生点附近找一块满足条件的海床（找不到返回 false）。</summary>
        static bool FindSeabed(Terrain terrain, Vector3 near, float minDepth, float maxDepth, System.Func<float, float, bool> ok, out Vector3 pos)
        {
            for (float r = 20f; r < 220f; r += 6f)
                for (float a = 0f; a < 360f; a += 9f)
                {
                    float x = near.x + Mathf.Sin(a * Mathf.Deg2Rad) * r, z = near.z + Mathf.Cos(a * Mathf.Deg2Rad) * r;
                    float h = terrain.SampleHeight(new Vector3(x, 0, z)) + terrain.transform.position.y;
                    if (-h < minDepth || -h > maxDepth || !ok(x, z)) continue;
                    pos = new Vector3(x, h, z);
                    return true;
                }
            pos = default;
            return false;
        }

        static void UnderShot(Camera cam, Transform player, Transform root, DayNightCycle cycle, OceanFollow ocean, UnderwaterFX ufx,
                              Vector3 eye, float yaw, float pitch, float time, string name)
        {
            var pos = eye - Vector3.up * root.localPosition.y;
            Shot(cam, player, root, cycle, ocean, pos, yaw, pitch, time, name, ufx);
        }

        /// <summary>离线渲染不跑 MarineLife，手动在镜头前摆几群鱼看外观。</summary>
        static Transform FishPreview(Vector3 eye, float yaw)
        {
            var life = Object.FindFirstObjectByType<MarineLife>();
            var t = new GameObject("FishPreview").transform;
            if (life == null) return t;
            var fwd = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            var right = Quaternion.Euler(0, yaw, 0) * Vector3.right;
            int si = 0;
            foreach (var sp in life.species)
            {
                int n = Mathf.Clamp(sp.schoolSize.y, 1, 30);
                var center = eye + fwd * (3.5f + si * 1.4f) + right * ((si % 3) - 1) * 2.2f + Vector3.up * ((si % 2) * 0.8f - 0.6f);
                if (sp.habitat == MarineLife.Habitat.Seabed) center += Vector3.down * 0.8f;
                for (int i = 0; i < n; i++)
                {
                    var go = new GameObject(sp.name);
                    go.transform.SetParent(t);
                    GameObject model;
                    if (sp.prefab != null)
                    {
                        model = Object.Instantiate(sp.prefab, go.transform, false);
                        model.transform.localScale = sp.prefab.transform.localScale * sp.modelScale;
                    }
                    else
                    {
                        model = new GameObject("Model");
                        model.transform.SetParent(go.transform, false);
                        model.AddComponent<MeshFilter>().sharedMesh = sp.mesh;
                        model.AddComponent<MeshRenderer>().sharedMaterial = sp.material;
                    }
                    model.transform.localRotation = Quaternion.Euler(sp.modelEuler);
                    go.transform.position = center + Vector3.Scale(Random.insideUnitSphere, new Vector3(1.3f, 0.5f, 1f)) * Mathf.Min(sp.spread, 1.6f);
                    go.transform.rotation = Quaternion.Euler(Random.Range(-8f, 8f), yaw + 90f + Random.Range(-20f, 20f), 0);
                    go.transform.localScale = Vector3.one * Mathf.Lerp(sp.scale.x, sp.scale.y, Random.value);
                }
                si++;
            }
            return t;
        }

        static void CaptureUnderwater(Camera cam, Transform player, Transform root, DayNightCycle cycle, OceanFollow ocean, Terrain terrain, Vector3 spawn)
        {
            var ufx = cam.GetComponent<UnderwaterFX>();
            if (ufx == null) return;
            if (FindSeabed(terrain, spawn, 5f, 9f, (x, z) => MarineBuilder.ReefMask(x, z) > 0.7f, out var reef))
            {
                var eye = reef + Vector3.up * 2.6f;
                var fish = FishPreview(eye + Vector3.down * 0.6f, 30f);
                UnderShot(cam, player, root, cycle, ocean, ufx, eye, 30f, 22f, 11f, "17_underwater_reef");
                Object.DestroyImmediate(fish.gameObject);
                UnderShot(cam, player, root, cycle, ocean, ufx, eye, 210f, -55f, 11f, "18_surface_from_below");
            }
            else Debug.LogWarning("[Preview] 没找到礁盘");
            if (FindSeabed(terrain, spawn, 2.5f, 6f, (x, z) => MarineBuilder.MeadowMask(x, z) > 0.75f && MarineBuilder.ReefMask(x, z) < 0.2f, out var meadow))
                UnderShot(cam, player, root, cycle, ocean, ufx, meadow + Vector3.up * 1.1f, 120f, 12f, 10f, "19_seagrass");
            else Debug.LogWarning("[Preview] 没找到海草床");
            if (FindSeabed(terrain, spawn, 11f, 17f, (x, z) => MarineBuilder.KelpMask(x, z) > 0.75f, out var kelp))
                UnderShot(cam, player, root, cycle, ocean, ufx, kelp + Vector3.up * 2f, 60f, -18f, 12f, "20_kelp_forest");
            else Debug.LogWarning("[Preview] 没找到海带林");
            var tablet = GameObject.Find("SeaTablet");
            if (tablet != null)
            {
                var tp = tablet.transform.position;
                var eye = tp + tablet.transform.forward * 3.2f + Vector3.up * 0.7f;
                float yaw = Mathf.Atan2(tp.x - eye.x, tp.z - eye.z) * Mathf.Rad2Deg;
                UnderShot(cam, player, root, cycle, ocean, ufx, eye, yaw, 6f, 11f, "21_sea_tablet");
            }
            var boat = GameObject.Find("SunkenBoat");
            if (boat != null)
            {
                var bp = boat.transform.position;
                var eye = bp + new Vector3(5f, 2.5f, -5f);
                float yaw = Mathf.Atan2(bp.x - eye.x, bp.z - eye.z) * Mathf.Rad2Deg;
                UnderShot(cam, player, root, cycle, ocean, ufx, eye, yaw, 18f, 11f, "22_sunken_boat");
            }
            // 恢复到岸上
            player.position = spawn;
            ufx.ApplyNow();
        }

        static void CaptureWeapons(Camera cam, Transform player, Transform root, DayNightCycle cycle, OceanFollow ocean, Vector3 spawn, float yaw)
        {
            var wc = cam.GetComponent<WeaponController>();
            if (wc == null) return;
            if (wc.idleArms != null) wc.idleArms.SetActive(false);
            for (int i = 0; i < wc.slots.Count; i++)
            {
                for (int k = 0; k < wc.slots.Count; k++) wc.slots[k].viewModel.SetActive(k == i);
                Shot(cam, player, root, cycle, ocean, spawn, yaw + 40f, 4f, 10f, $"23_weapon_{wc.slots[i].def.id}");
            }
            // 开局空手：两只手在画面下方
            for (int k = 0; k < wc.slots.Count; k++) wc.slots[k].viewModel.SetActive(false);
            if (wc.idleArms != null) wc.idleArms.SetActive(true);
            Shot(cam, player, root, cycle, ocean, spawn, yaw + 40f, 4f, 10f, "23_weapon_unarmed");
            var machete = GameObject.Find("MachetePickup");
            if (machete != null)
            {
                var mp = machete.transform.position;
                var eye = mp + new Vector3(1.4f, 0f, -1.4f);
                Shot(cam, player, root, cycle, ocean, new Vector3(eye.x, mp.y + 0.6f, eye.z), Mathf.Atan2(mp.x - eye.x, mp.z - eye.z) * Mathf.Rad2Deg, 30f, 10f, "24_machete_pickup");
            }
            var gun = GameObject.Find("SpeargunPickup");
            if (gun != null)
            {
                var gp = gun.transform.position;
                var eye = gp + new Vector3(-1.3f, 0f, -1.3f);
                Shot(cam, player, root, cycle, ocean, new Vector3(eye.x, gp.y + 0.6f, eye.z), Mathf.Atan2(gp.x - eye.x, gp.z - eye.z) * Mathf.Rad2Deg, 30f, 10f, "25_speargun_pickup");
            }
        }

        /// <summary>动态天气：同一个海滩视角，分别是阴天、小雨、暴雨雷暴（含一次闪电）、浓雾、浓雾的夜。</summary>
        static void CaptureWeather(Camera cam, Transform player, Transform root, DayNightCycle cycle, OceanFollow ocean, Vector3 spawn, float spawnYaw)
        {
            var ws = Object.FindFirstObjectByType<Rishi.Weather.WeatherSystem>();
            if (ws == null || ws.config == null) return;
            WeatherProfile P(string id) => ws.config.weathers.Find(e => e.profile != null && e.profile.name == "Weather_" + id)?.profile;
            var shots = new (string id, float time, float flash, string name)[]
            {
                ("Overcast", 11f, 0f, "29_weather_overcast"),
                ("Rain", 11f, 0f, "30_weather_rain"),
                ("Thunderstorm", 16f, 0f, "31_weather_storm"),
                ("Thunderstorm", 16f, 1.2f, "32_weather_lightning"),
                ("Fog", 9f, 0f, "33_weather_fog"),
                ("Fog", 22.5f, 0f, "34_weather_fog_night"),
            };
            float yaw = spawnYaw - 35f;
            foreach (var (id, time, flash, name) in shots)
            {
                var p = P(id);
                if (p == null) continue;
                player.SetPositionAndRotation(spawn, Quaternion.Euler(0, yaw, 0)); // 雨幕跟着摄像机，先把摄像机放到位
                ws.ApplyNow(p, wet: true);
                Shader.SetGlobalFloat("_IslandSkyFlash", flash);
                Shot(cam, player, root, cycle, ocean, spawn, yaw, 4f, time, name);
            }
            Shader.SetGlobalFloat("_IslandSkyFlash", 0f);
            ws.ResetVisuals();
        }

        /// <summary>手电筒：夜里同一个视角关灯 / 开灯，以及营地里的手电筒拾取物。</summary>
        static void CaptureFlashlight(Camera cam, Transform player, Transform root, DayNightCycle cycle, OceanFollow ocean, Vector3 spawn, float spawnYaw)
        {
            var fl = player.GetComponent<Flashlight>();
            if (fl == null) return;
            // 出生点转身朝向丛林，夜里 22 点半
            float yaw = spawnYaw + 150f;
            fl.PreviewOn(false);
            Shot(cam, player, root, cycle, ocean, spawn, yaw, 16f, 22.5f, "35_night_dark");
            var wc = cam.GetComponent<WeaponController>();
            if (wc != null && wc.idleLeftArm != null) wc.idleLeftArm.SetActive(false); // 开手电时左手拿手电（游戏里 WeaponController 自动切换）
            fl.PreviewOn(true);
            Shot(cam, player, root, cycle, ocean, spawn, yaw, 16f, 22.5f, "36_night_flashlight");
            fl.PreviewOn(false);
            if (wc != null && wc.idleLeftArm != null) wc.idleLeftArm.SetActive(true);
            var pickup = GameObject.Find("FlashlightPickup");
            if (pickup != null)
            {
                var p = pickup.transform.position;
                var eye = p + new Vector3(0.9f, 0f, -0.9f);
                eye.y = p.y + 0.45f;
                Shot(cam, player, root, cycle, ocean, eye, Mathf.Atan2(p.x - eye.x, p.z - eye.z) * Mathf.Rad2Deg, 38f, 10.5f, "37_flashlight_pickup");
            }
        }

        /// <summary>篝火：营地和沙滩的篝火在夜里点燃的样子，以及白天熄灭的沙滩篝火。</summary>
        static void CaptureCampfires(Camera cam, Transform player, Transform root, DayNightCycle cycle, OceanFollow ocean)
        {
            foreach (var (name, time, lit, file) in new[] { ("CampFire", 21.5f, true, "38_campfire_camp_night"), ("BeachFire", 22f, true, "39_campfire_beach_night"), ("BeachFire", 10f, false, "40_campfire_beach_day") })
            {
                var go = GameObject.Find(name);
                if (go == null) continue;
                var fire = go.GetComponent<Rishi.Player.Campfire>();
                fire.PreviewLit(lit);
                var p = go.transform.position;
                var eye = p + go.transform.right * 3.2f + go.transform.forward * 2.2f;
                eye.y = p.y + 1.0f;
                Shot(cam, player, root, cycle, ocean, eye, Mathf.Atan2(p.x - eye.x, p.z - eye.z) * Mathf.Rad2Deg, 14f, time, file);
                fire.PreviewLit(false);
            }
        }

        /// <summary>生物框架：测试生物（F10 生成的那种占位外观）站在沙滩上，黄昏和夜里各一张。</summary>
        static void CaptureCreature(Camera cam, Transform player, Transform root, DayNightCycle cycle, OceanFollow ocean, Vector3 spawn, float spawnYaw)
        {
            var spawner = Object.FindFirstObjectByType<Rishi.Creatures.CreatureDebugSpawner>();
            if (spawner == null || spawner.testCreature == null) return;
            var fwd = Quaternion.Euler(0, spawnYaw - 35f, 0) * Vector3.forward;
            var terrain = IslandBuilder.MainTerrain;
            var pos = spawn + fwd * 6f;
            pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y;
            var c = Rishi.Creatures.CreatureDebugSpawner.Spawn(spawner.testCreature, pos, Quaternion.LookRotation(-fwd), cycle);
            Shot(cam, player, root, cycle, ocean, spawn, spawnYaw - 35f, 4f, 18.4f, "41_creature_dusk");
            Shot(cam, player, root, cycle, ocean, spawn, spawnYaw - 35f, 4f, 22.5f, "42_creature_night");
            Object.DestroyImmediate(c.gameObject);
        }

        /// <summary>影嗣：夜里站在沙滩上的黑烟人形，不开手电和用手电照着各一张。</summary>
        static void CaptureShade(Camera cam, Transform player, Transform root, DayNightCycle cycle, OceanFollow ocean, Vector3 spawn, float spawnYaw)
        {
            var spawner = Object.FindFirstObjectByType<Rishi.Creatures.ShadeSpawner>();
            if (spawner == null || spawner.shadeDef == null) return;
            float yaw = spawnYaw + 150f; // 朝丛林
            var fwd = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            var terrain = IslandBuilder.MainTerrain;
            var pos = spawn + fwd * 7f;
            pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y;
            var shade = Rishi.Creatures.Shade.Spawn(spawner.shadeDef, pos, Quaternion.LookRotation(-fwd), cycle, spawner.bodyMaterial, spawner.smokeMaterial, spawner.eyeMaterial);
            shade.smoke.Simulate(3f, true, true);
            Shot(cam, player, root, cycle, ocean, spawn, yaw, 4f, 22.5f, "43_shade_dark");
            var fl = player.GetComponent<Flashlight>();
            var wc = cam.GetComponent<WeaponController>();
            if (fl != null)
            {
                if (wc != null && wc.idleLeftArm != null) wc.idleLeftArm.SetActive(false);
                fl.PreviewOn(true);
                Shot(cam, player, root, cycle, ocean, spawn, yaw, 4f, 22.5f, "44_shade_flashlight");
                fl.PreviewOn(false);
                if (wc != null && wc.idleLeftArm != null) wc.idleLeftArm.SetActive(true);
            }
            Object.DestroyImmediate(shade.gameObject);
        }

        /// <summary>第一夜：手电照着沙滩上通向沉船的湿脚印；黄昏余光里林边站着的彼得。</summary>
        static void CaptureFirstNight(Camera cam, Transform player, Transform root, DayNightCycle cycle, OceanFollow ocean)
        {
            var ev = Object.FindFirstObjectByType<FirstNightEvent>(FindObjectsInactive.Include);
            if (ev == null || ev.footprints == null) return;
            ev.PreviewShow(true);
            var prints = ev.footprints.transform;
            if (prints.childCount > 2)
            {
                // 站在脚印旁边的干沙上，侧着看这一串从海里走上来的脚印
                var first = prints.GetChild(0).position;
                var last = prints.GetChild(prints.childCount - 1).position;
                var mid = (first + last) * 0.5f;
                var dir = Vector3.ProjectOnPlane(last - first, Vector3.up).normalized;
                var side = Vector3.Cross(Vector3.up, dir);
                var land = IslandBuilder.MainTerrain;
                var eye = mid + side * 4f;
                if (land.SampleHeight(mid - side * 4f) > land.SampleHeight(eye)) eye = mid - side * 4f; // 站在地势高（离海远）的那一侧
                eye.y = land.SampleHeight(eye) + land.transform.position.y + 1.05f;
                var back = (eye - mid).normalized;
                var fl = player.GetComponent<Flashlight>();
                var wc = cam.GetComponent<WeaponController>();
                if (wc != null && wc.idleLeftArm != null) wc.idleLeftArm.SetActive(false);
                fl?.PreviewOn(true);
                Shot(cam, player, root, cycle, ocean, eye, Mathf.Atan2(-back.x, -back.z) * Mathf.Rad2Deg, 30f, 21.5f, "45_wet_footprints");
                fl?.PreviewOn(false);
                if (wc != null && wc.idleLeftArm != null) wc.idleLeftArm.SetActive(true);
            }
            if (ev.peter != null)
            {
                var pp = ev.peter.transform.position;
                var eye = pp + ev.peter.transform.forward * 12f;
                eye.y = IslandBuilder.MainTerrain.SampleHeight(eye) + 1.05f + IslandBuilder.MainTerrain.transform.position.y;
                Shot(cam, player, root, cycle, ocean, eye, Mathf.Atan2(pp.x - eye.x, pp.z - eye.z) * Mathf.Rad2Deg, -2f, 16.5f, "46_peter_treeline");
            }
            ev.PreviewShow(false);
        }

        /// <summary>野猪和椰子蟹：白天的样子，和夜里用手电照着的蚀化形态。</summary>
        static void CaptureBeasts(Camera cam, Transform player, Transform root, DayNightCycle cycle, OceanFollow ocean)
        {
            var terrain = IslandBuilder.MainTerrain;
            var fl = player.GetComponent<Flashlight>();
            var wc = cam.GetComponent<WeaponController>();
            foreach (var (name, dist, file) in new[] { ("Boar", 4.2f, "49_boar"), ("CoconutCrab", 2.6f, "51_coconut_crab") })
            {
                var go = GameObject.Find(name);
                if (go == null) continue;
                var beast = go.GetComponent<Rishi.Creatures.Beast>();
                var p = go.transform.position;
                var eye = p + go.transform.forward * dist + go.transform.right * dist * 0.4f;
                eye.y = terrain.SampleHeight(eye) + terrain.transform.position.y + 1.05f;
                float yaw = Mathf.Atan2(p.x - eye.x, p.z - eye.z) * Mathf.Rad2Deg;
                float pitch = Mathf.Atan2(eye.y + 0.7f - (p.y + 0.4f), dist) * Mathf.Rad2Deg;
                beast.Preview(false);
                Shot(cam, player, root, cycle, ocean, eye, yaw, pitch, 10.5f, file + "_day");
                beast.Preview(true);
                if (wc != null && wc.idleLeftArm != null) wc.idleLeftArm.SetActive(false);
                fl?.PreviewOn(true);
                Shot(cam, player, root, cycle, ocean, eye, yaw, pitch, 22.5f, file + "_eclipse");
                fl?.PreviewOn(false);
                if (wc != null && wc.idleLeftArm != null) wc.idleLeftArm.SetActive(true);
                beast.Preview(false);
            }
        }

        /// <summary>潮汐：同一个机位，高潮（10:06）和低潮（16:18）——出生的沙滩看沉船、环礁沙洲上看外侧的礁坪。</summary>
        static void CaptureTide(Camera cam, Transform player, Transform root, DayNightCycle cycle, OceanFollow ocean, Vector3 spawn, float spawnYaw)
        {
            var tide = Object.FindFirstObjectByType<TideController>();
            if (tide == null || tide.config == null) return;
            float high = tide.config.firstHighTideHour, low = high + tide.config.periodHours * 0.5f;
            Shot(cam, player, root, cycle, ocean, spawn, spawnYaw, 6f, high, "62_tide_high_beach");
            Shot(cam, player, root, cycle, ocean, spawn, spawnYaw, 6f, low, "63_tide_low_beach");

            var spec = AtollBuilder.Default;
            var atoll = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None).FirstOrDefault(t => t.name == spec.name);
            if (atoll == null) return;
            var c = new Vector3(spec.center.x, 0, spec.center.y);
            Vector3 best = c; float bestH = -99f;
            for (float a = 0; a < 360f; a += 2f)
                for (float r = spec.ringRadius * 0.75f; r < spec.ringRadius * 1.2f; r += 3f)
                {
                    var p = c + Quaternion.Euler(0, a, 0) * Vector3.forward * r;
                    float h = atoll.SampleHeight(p) + atoll.transform.position.y;
                    if (h > bestH) { bestH = h; best = p; }
                }
            var eye = best + Vector3.up * (bestH + 2.5f); // 站在沙洲最高处再高一点，看得见外侧的礁坪
            var outward = best - c; outward.y = 0;
            float yaw = Mathf.Atan2(outward.x, outward.z) * Mathf.Rad2Deg;
            Shot(cam, player, root, cycle, ocean, eye, yaw, 16f, high, "64_tide_high_reef");
            Shot(cam, player, root, cycle, ocean, eye, yaw, 16f, low, "65_tide_low_reef");
        }

        /// <summary>小艇：搁在沙滩上船底破着、修好之后，和开到海上坐在船里看向环礁。</summary>
        static void CaptureBoat(Camera cam, Transform player, Transform root, DayNightCycle cycle, OceanFollow ocean)
        {
            var boat = Object.FindFirstObjectByType<Rishi.Vehicles.Boat>();
            if (boat == null) return;
            var b = boat.transform;
            var main = IslandBuilder.MainTerrain;
            var target = b.position + Vector3.up * 0.4f;
            var eye = b.position - b.forward * 3.2f + b.right * 3.4f;
            eye.y = main.SampleHeight(eye) + main.transform.position.y + 1.05f;
            float yaw = Mathf.Atan2(target.x - eye.x, target.z - eye.z) * Mathf.Rad2Deg;
            float pitch = Mathf.Atan2(eye.y + 0.7f - target.y, Vector3.Distance(new Vector3(eye.x, 0, eye.z), new Vector3(target.x, 0, target.z))) * Mathf.Rad2Deg;
            boat.SetRepaired(false);
            Shot(cam, player, root, cycle, ocean, eye, yaw, pitch, 9.5f, "59_dinghy_broken");
            boat.SetRepaired(true);
            Shot(cam, player, root, cycle, ocean, eye, yaw, pitch, 9.5f, "60_dinghy_repaired");

            // 开到海上：船浮在水面，坐在后座板上看向东北的环礁
            var pose = (b.position, b.rotation);
            var atoll = new Vector3(AtollBuilder.Default.center.x, 0, AtollBuilder.Default.center.y);
            var sea = new Vector3(b.position.x, -boat.draft, b.position.z) + Vector3.ProjectOnPlane(b.forward, Vector3.up).normalized * 45f; // 船头朝海，先划出去
            var dir = atoll - sea; dir.y = 0; dir.Normalize();
            b.SetPositionAndRotation(sea, Quaternion.LookRotation(dir));
            var seat = boat.seat.position;
            Shot(cam, player, root, cycle, ocean, seat, b.eulerAngles.y, 4f, 10f, "61_dinghy_at_sea");
            b.SetPositionAndRotation(pose.Item1, pose.Item2);
            boat.SetRepaired(false);
        }

        /// <summary>第二座岛：环礁的鸟瞰、站在沙洲上看礁湖、礁湖内侧的红树林、从主岛出生沙滩远眺。</summary>
        static void CaptureAtoll(Camera cam, Transform player, Transform root, DayNightCycle cycle, OceanFollow ocean, Vector3 spawn)
        {
            var spec = AtollBuilder.Default;
            var atoll = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None).FirstOrDefault(t => t.name == spec.name);
            if (atoll == null) return;
            var c = new Vector3(spec.center.x, 0, spec.center.y);
            float G(Vector3 p) => atoll.SampleHeight(p) + atoll.transform.position.y;
            float Yaw(Vector3 from, Vector3 to) => Mathf.Atan2(to.x - from.x, to.z - from.z) * Mathf.Rad2Deg;
            float far = cam.farClipPlane;

            // 鸟瞰：从主岛方向的高处看整个环礁
            var toMain = (-c).normalized;
            var eye = c + toMain * (spec.ringRadius + 140f) + Vector3.up * 110f;
            Shot(cam, player, root, cycle, ocean, eye, Yaw(eye, c), 24f, 11f, "55_atoll_aerial");

            // 站在最高的沙洲上看礁湖
            Vector3 best = c; float bestH = -99f;
            for (float a = 0; a < 360f; a += 2f)
                for (float r = spec.ringRadius * 0.75f; r < spec.ringRadius * 1.2f; r += 3f)
                {
                    var p = c + Quaternion.Euler(0, a, 0) * Vector3.forward * r;
                    float h = G(p);
                    if (h > bestH) { bestH = h; best = p; }
                }
            eye = best; eye.y = bestH + 1.05f;
            Shot(cam, player, root, cycle, ocean, eye, Yaw(eye, c), 4f, 10f, "56_atoll_lagoon");

            // 红树林：从礁湖中间低空看过去
            var mDir = new Vector3(Mathf.Cos(AtollBuilder.MangroveAngle(spec)), 0, Mathf.Sin(AtollBuilder.MangroveAngle(spec)));
            eye = c + mDir * spec.ringRadius * 0.5f + Vector3.up * 7f;
            Shot(cam, player, root, cycle, ocean, eye, Yaw(eye, c + mDir * spec.ringRadius), 6f, 15.5f, "57_atoll_mangroves");

            // 从主岛朝向环礁那边的岸上高处远眺（岸边是红树林，挡视线）：从环礁往主岛中心走，第一处高出海面 9 米的地方
            var main = IslandBuilder.MainTerrain;
            eye = spawn;
            for (float t = 0f; t < 1f; t += 0.002f)
            {
                var p = Vector3.Lerp(c, Vector3.zero, t);
                float h = main.SampleHeight(p) + main.transform.position.y;
                if (h > 9f) { eye = p; eye.y = h + 1.05f; break; }
            }
            Shot(cam, player, root, cycle, ocean, eye, Yaw(eye, c), 1f, 9f, "58_atoll_from_shore");
            cam.farClipPlane = far;
        }

        static Vector3 WithGround(this Vector3 v, System.Func<float, float, float> ground) => new Vector3(v.x, ground(v.x, v.z), v.z);

        static void Shot(Camera cam, Transform player, Transform root, DayNightCycle cycle, OceanFollow ocean,
                         Vector3 pos, float yaw, float pitch, float time, string name, UnderwaterFX ufx = null)
        {
            player.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            root.localRotation = Quaternion.Euler(pitch, 0, 0);
            cycle.timeOfDay = time;
            if (ufx != null) ufx.ApplyNow();
            cycle.Apply(false);
            Object.FindFirstObjectByType<TideController>()?.Apply(); // 海平面跟着这个时刻的潮位
            if (ufx != null) ufx.ApplyNow();
            DynamicGI.UpdateEnvironment();
            if (cycle.skyProbe != null)
            {
                cycle.skyProbe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing;
                cycle.skyProbe.RenderProbe();
            }
            var cp = cam.transform.position;
            ocean.transform.position = new Vector3(Mathf.Round(cp.x / 4) * 4, GameSignals.SeaLevel, Mathf.Round(cp.z / 4) * 4);

            const int w = 1600, h = 900;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cam.targetTexture = rt;
            for (int i = 0; i < 3; i++) cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes($"Screenshots/Island/{name}.png", tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(tex);
            rt.Release();
        }
    }
}
