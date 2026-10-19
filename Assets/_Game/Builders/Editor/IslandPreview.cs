using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
        }

        [MenuItem("Tools/Island/渲染预览截图")]
        public static void Capture()
        {
            EditorSceneManager.OpenScene(IslandBuilder.ScenePath);
            var player = GameObject.Find("Player").transform;
            var root = player.Find("CameraRoot");
            var cam = root.GetComponentInChildren<Camera>();
            var cycle = Object.FindFirstObjectByType<DayNightCycle>();
            var ocean = Object.FindFirstObjectByType<OceanFollow>();
            var terrain = Object.FindFirstObjectByType<Terrain>();
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
            Debug.Log("[Island] 预览截图已写入 Screenshots/Island/");
        }

        static Vector3 WithGround(this Vector3 v, System.Func<float, float, float> ground) => new Vector3(v.x, ground(v.x, v.z), v.z);

        static void Shot(Camera cam, Transform player, Transform root, DayNightCycle cycle, OceanFollow ocean,
                         Vector3 pos, float yaw, float pitch, float time, string name)
        {
            player.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            root.localRotation = Quaternion.Euler(pitch, 0, 0);
            cycle.timeOfDay = time;
            cycle.Apply(false);
            DynamicGI.UpdateEnvironment();
            if (cycle.skyProbe != null)
            {
                cycle.skyProbe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing;
                cycle.skyProbe.RenderProbe();
            }
            var cp = cam.transform.position;
            ocean.transform.position = new Vector3(Mathf.Round(cp.x / 4) * 4, 0, Mathf.Round(cp.z / 4) * 4);

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
