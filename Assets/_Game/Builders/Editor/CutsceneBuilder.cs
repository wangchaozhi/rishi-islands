using System.Collections.Generic;
using System.IO;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering.Universal;
using UnityEngine.Timeline;
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
    /// 生成过场（Timeline 资产 + 过场摄像机 + 播放器）。生成的 Timeline 可以在 Window/Sequencing/Timeline 里继续手工调整。
    /// </summary>
    public static class CutsceneBuilder
    {
        const string Dir = "Assets/Art/Generated/Cutscenes";
        const string NoiseProfile = "Packages/com.unity.cinemachine/Presets/Noise/Handheld_normal_mild.asset";

        public class Shot
        {
            public float start, duration;
            public Vector3 fromPos, toPos;
            public Vector3 fromEuler, toEuler;
            public float fromFov = 55f, toFov = 55f;
            public bool easeOutOnly;
        }

        /// <summary>场景里共用的过场摄像机（带 CinemachineBrain）和被镜头轨道驱动的 Cinemachine 机位。</summary>
        public static (Camera cam, CinemachineCamera rig) EnsureRig()
        {
            var existing = GameObject.Find("CutsceneCamera");
            if (existing != null) return (existing.GetComponent<Camera>(), GameObject.Find("CutsceneRig").GetComponent<CinemachineCamera>());

            var camGo = new GameObject("CutsceneCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 3000f;
            cam.cullingMask = ~(1 << 8); // 不拍玩家手里的枪（武器层）
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.stopNaN = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<CinemachineBrain>();
            CinematicSetup.UseTAA(cam);
            camGo.SetActive(false);

            var rigGo = new GameObject("CutsceneRig");
            var rig = rigGo.AddComponent<CinemachineCamera>();
            rig.Priority = 100;
            var noise = rigGo.AddComponent<CinemachineBasicMultiChannelPerlin>();
            noise.NoiseProfile = AssetDatabase.LoadAssetAtPath<NoiseSettings>(NoiseProfile);
            noise.AmplitudeGain = 0.6f;
            noise.FrequencyGain = 0.5f;
            if (noise.NoiseProfile == null) Debug.LogWarning("[Cutscene] 找不到手持抖动预设 " + NoiseProfile);
            rigGo.SetActive(false);
            return (cam, rig);
        }

        /// <summary>创建一段过场：Timeline 资产（镜头 / 字幕 / 黑场轨道）+ 带 CutscenePlayer 的物体。</summary>
        public static CutscenePlayer Create(string name, List<Shot> shots, List<(float start, float dur, string text)> subtitles,
                                           List<(float start, float dur, float from, float to)> fades, bool playOnStart)
        {
            Directory.CreateDirectory(Dir);
            string path = $"{Dir}/{name}.playable";
            AssetDatabase.DeleteAsset(path);
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            timeline.name = name;
            AssetDatabase.CreateAsset(timeline, path);

            var camTrack = timeline.CreateTrack<CameraPathTrack>(null, "镜头");
            foreach (var s in shots)
            {
                var clip = camTrack.CreateClip<CameraMoveClip>();
                clip.start = s.start;
                clip.duration = s.duration;
                clip.displayName = $"镜头 {s.start:0.#}s";
                var a = (CameraMoveClip)clip.asset;
                a.startPosition = s.fromPos;
                a.endPosition = s.toPos;
                a.startEuler = s.fromEuler;
                a.endEuler = s.toEuler;
                a.startFov = s.fromFov;
                a.endFov = s.toFov;
                a.ease = s.easeOutOnly ? new AnimationCurve(new Keyframe(0, 0, 1.6f, 1.6f), new Keyframe(1, 1, 0, 0)) : AnimationCurve.EaseInOut(0, 0, 1, 1);
            }

            var subTrack = timeline.CreateTrack<SubtitleTrack>(null, "字幕");
            foreach (var (start, dur, text) in subtitles)
            {
                var clip = subTrack.CreateClip<SubtitleClip>();
                clip.start = start;
                clip.duration = dur;
                clip.displayName = text.Length > 12 ? text.Substring(0, 12) + "…" : text;
                ((SubtitleClip)clip.asset).text = text;
            }

            var fadeTrack = timeline.CreateTrack<FadeTrack>(null, "黑场");
            foreach (var (start, dur, from, to) in fades)
            {
                var clip = fadeTrack.CreateClip<FadeClip>();
                clip.start = start;
                clip.duration = dur;
                var a = (FadeClip)clip.asset;
                a.from = from;
                a.to = to;
            }
            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssets();

            var (cam, rig) = EnsureRig();
            var go = new GameObject(name);
            var director = go.AddComponent<PlayableDirector>();
            director.playableAsset = timeline;
            director.extrapolationMode = DirectorWrapMode.None;
            director.playOnAwake = false;
            director.SetGenericBinding(camTrack, rig.transform);
            var player = go.AddComponent<CutscenePlayer>();
            player.cutsceneCamera = cam;
            player.rig = rig;
            player.playOnStart = playOnStart;
            return player;
        }

        // ================= CG02 · 醒来 =================

        public static CutscenePlayer BuildWakeUp(GameObject player, Vector3 wreck, OpeningMonologue monologue, bool playOnStart)
        {
            Vector3 eye = player.transform.position + Vector3.up * 0.7f;   // 与 CameraRoot 的高度一致
            Vector3 spawn = player.transform.position - Vector3.up * 1.05f; // 脚下的沙滩
            float yaw = player.transform.eulerAngles.y;
            Vector3 toIsland = new Vector3(-spawn.x, 0, -spawn.z).normalized;
            Vector3 seaward = -toIsland;
            float islandYaw = Mathf.Atan2(toIsland.x, toIsland.z) * Mathf.Rad2Deg;
            Vector3 toWreck = (wreck - spawn).normalized;
            float wreckYaw = Mathf.Atan2(toWreck.x, toWreck.z) * Mathf.Rad2Deg;
            Vector3 right = Vector3.Cross(Vector3.up, toWreck).normalized;
            var cam = player.GetComponentInChildren<Camera>();

            var shots = new List<Shot>
            {
                // 1. 海面上空缓缓下降，岛在前方
                new Shot { start = 0f, duration = 9f,
                           fromPos = spawn + seaward * 75f + Vector3.up * 42f, toPos = spawn + seaward * 24f + Vector3.up * 8f,
                           fromEuler = new Vector3(18f, islandYaw, 0), toEuler = new Vector3(6f, islandYaw + 6f, 0),
                           fromFov = 50f, toFov = 55f },
                // 2. 贴着沙滩，望向远处的沉船
                new Shot { start = 9f, duration = 8f,
                           fromPos = spawn - right * 6f + Vector3.up * 0.45f - toWreck * 1f, toPos = spawn - right * 3.5f + Vector3.up * 0.55f + toWreck * 1.5f,
                           fromEuler = new Vector3(1f, wreckYaw - 8f, 0), toEuler = new Vector3(0f, wreckYaw - 2f, 0),
                           fromFov = 42f, toFov = 40f },
                // 3. 从躺在沙滩上的视角，慢慢起身，落到玩家的眼睛位置
                new Shot { start = 17f, duration = 8f,
                           fromPos = spawn + Vector3.up * 0.25f, toPos = eye,
                           fromEuler = new Vector3(-55f, yaw + 25f, 12f), toEuler = new Vector3(0f, yaw, 0f),
                           fromFov = 65f, toFov = cam != null ? cam.fieldOfView : 72f, easeOutOnly = true },
            };
            var subtitles = new List<(float, float, string)>
            {
                (2.5f, 4f, "三个月前，父亲在南太平洋失踪了。"),
                (7f, 4.2f, "他寄回的最后一封信里，只有一张手绘海图，和一句话——"),
                (11.8f, 3.2f, "「别来找我。」"),
                (18.5f, 4f, "……风暴过去了。我还活着。"),
            };
            var fades = new List<(float, float, float, float)>
            {
                (0f, 3f, 1f, 0f),
                (16.6f, 0.4f, 0f, 1f),
                (17f, 1.4f, 1f, 0f),
            };
            var cp = Create("CG02_WakeUp", shots, subtitles, fades, playOnStart);
            monologue.waitForCutscene = true;
            UnityEventTools.AddPersistentListener(cp.onFinished, monologue.Show);
            return cp;
        }

        // ================= CG03 石门开启 · CG04 第一块心 · CG05 入夜 =================

        /// <summary>神殿里的三段过场：解开石柱机关时 CG03，打开石匣时 CG04，太阳开始坠落时 CG05（入夜加速到 11 秒）。</summary>
        public static void BuildTempleCutscenes(PillarPuzzle puzzle, TreasureChest chest, DayNightCycle cycle)
        {
            var temple = puzzle.transform.root.Find("SunTemple") ?? GameObject.Find("SunTemple").transform;
            Vector3 fwd = Vector3.ProjectOnPlane(temple.forward, Vector3.up).normalized; // 神殿正面朝外
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            float Yaw(Vector3 d) => Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float Pitch(Vector3 d) => -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            Vector3 Look(Vector3 from, Vector3 to) { var d = to - from; return new Vector3(Pitch(d), Yaw(d), 0); }

            // ---- CG03 石门开启：石柱的符文亮起 → 正面看石门在轰鸣中沉入地下 ----
            var door = puzzle.door.position;
            var pillar = puzzle.pillars != null && puzzle.pillars.Length > 0 ? puzzle.pillars[0].transform.position : door + fwd * 4f;
            var p1a = pillar + fwd * 2.2f + right * 1.4f + Vector3.up * 1.1f;
            var p1b = pillar + fwd * 1.6f + right * 0.9f + Vector3.up * 1.3f;
            var d2a = door + fwd * 11f + Vector3.up * 1.4f;
            var d2b = door + fwd * 7.5f + Vector3.up * 1.6f;
            var cg03 = Create("CG03_StoneDoor", new List<Shot>
            {
                new Shot { start = 0f, duration = 2.2f, fromPos = p1a, toPos = p1b, fromEuler = Look(p1a, pillar + Vector3.up * 1.2f), toEuler = Look(p1b, pillar + Vector3.up * 1.4f), fromFov = 45f, toFov = 40f },
                new Shot { start = 2.2f, duration = 4.8f, fromPos = d2a, toPos = d2b, fromEuler = Look(d2a, door + Vector3.up * 1.5f), toEuler = Look(d2b, door + Vector3.up * 1.2f), fromFov = 50f, toFov = 46f },
            }, new List<(float, float, string)>(), new List<(float, float, float, float)> { (0f, 0.6f, 1f, 0f), (6.4f, 0.6f, 0f, 1f) }, false);
            UnityEventTools.AddPersistentListener(puzzle.onSolved, cg03.Play);

            // ---- CG04 第一块心：俯看石匣打开 → 碎片升起，镜头推近 ----
            // 石匣模型的根节点不在箱子中心，按渲染包围盒取箱子的位置；碎片会从原位升起 0.6 米
            var cr = chest.GetComponentsInChildren<Renderer>();
            var cb = cr[0].bounds;
            foreach (var r in cr) cb.Encapsulate(r.bounds);
            var box = cb.center;
            var heart = (chest.fragment != null ? chest.fragment.position : new Vector3(box.x, cb.max.y, box.z)) + Vector3.up * 0.6f;
            var c1a = box + fwd * 1.6f + right * 0.7f + Vector3.up * 1.3f;
            var c1b = box + fwd * 1.2f + right * 0.45f + Vector3.up * 1.05f;
            var c2a = heart + fwd * 1.7f + Vector3.up * 0.2f; // 拉开一点：画面下方是打开的石匣，上方是升起的碎片
            var c2b = heart + fwd * 1.15f + Vector3.up * 0.12f;
            var aim = heart - Vector3.up * 0.25f;
            var cg04 = Create("CG04_FirstHeart", new List<Shot>
            {
                new Shot { start = 0f, duration = 1.8f, fromPos = c1a, toPos = c1b, fromEuler = Look(c1a, box + Vector3.up * 0.3f), toEuler = Look(c1b, box + Vector3.up * 0.4f), fromFov = 45f, toFov = 42f },
                new Shot { start = 1.8f, duration = 2.6f, fromPos = c2a, toPos = c2b, fromEuler = Look(c2a, aim), toEuler = Look(c2b, aim), fromFov = 42f, toFov = 36f },
            }, new List<(float, float, string)> { (2.2f, 2.2f, "太阳之心的碎片……它是温的。") },
               new List<(float, float, float, float)> { (0f, 0.4f, 1f, 0f), (4.0f, 0.4f, 0f, 1f) }, false);
            UnityEventTools.AddPersistentListener(chest.onOpened, cg04.Play);

            // ---- CG05 入夜：神殿上空，太阳以不自然的速度坠进林冠，天色迅速暗下去 ----
            float sunsetYaw = cycle != null && cycle.config != null ? cycle.config.sunAzimuth : 35f; // 太阳在这个方位落下
            var s1a = temple.position - Vector3.ProjectOnPlane(Quaternion.Euler(0, sunsetYaw, 0) * Vector3.forward, Vector3.up) * 6f + Vector3.up * 20f;
            var s1b = s1a + Vector3.up * 6f;
            var cg05 = Create("CG05_Nightfall", new List<Shot>
            {
                new Shot { start = 0f, duration = 11f, fromPos = s1a, toPos = s1b, fromEuler = new Vector3(-6f, sunsetYaw, 0), toEuler = new Vector3(2f, sunsetYaw + 4f, 0), fromFov = 58f, toFov = 52f },
            }, new List<(float, float, string)> { (2.5f, 3.5f, "……太阳在往下坠？"), (7f, 3.2f, "不对。现在才是下午。") },
               new List<(float, float, float, float)> { (0f, 0.6f, 1f, 0f), (10.2f, 0.8f, 0f, 1f) }, false);
            UnityEventTools.AddPersistentListener(chest.onNightfall, cg05.Play);
            chest.nightfallSeconds = 11f;
        }

        // ================= CG01 · 风暴 =================

        /// <summary>开场：夜里的海上风暴，闪电照亮远处的岛，撞上暗礁，落水黑屏，然后接 CG02。</summary>
        public static CutscenePlayer BuildStorm(DayNightCycle dayNight, Renderer ocean, CutscenePlayer next)
        {
            var B = new Vector3(0, 0, -900f); // 船的位置，岛在正北方

            // ---- 风暴控制器（带船），由激活轨道控制显隐 ----
            var stormGo = new GameObject("CG01_Storm");
            var storm = stormGo.AddComponent<StormFX>();
            storm.dayNight = dayNight;
            storm.weather = ConfigAssets.Storm;
            storm.ocean = ocean;
            storm.rainMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Generated/Materials/FX_Soft.mat");
            var lightningLight = new GameObject("Lightning").AddComponent<Light>();
            lightningLight.transform.SetParent(stormGo.transform, false);
            lightningLight.type = LightType.Directional;
            lightningLight.color = new Color(0.75f, 0.82f, 1f);
            lightningLight.intensity = 0f;
            lightningLight.shadows = LightShadows.None;
            storm.lightning = lightningLight;
            storm.cues = new List<StormFX.Cue>
            {
                new StormFX.Cue { time = 7.6f, sound = "wave_crash" },
                new StormFX.Cue { time = 11.0f, flash = true },                       // 闪电照亮远处的岛
                new StormFX.Cue { time = 16.0f, flash = true, sound = "hull_crash" }, // 撞礁
                new StormFX.Cue { time = 17.2f, sound = "wave_crash" },
            };
            BuildBoat(stormGo.transform, B);

            // ---- 镜头：甲板第一人称视角，随船摇晃 ----
            Vector3 eye = B + new Vector3(0.25f, 2.9f, -1.8f);
            var shots = new List<Shot>();
            float[] rollSeq = { -7f, 6f, -8f, 5f };
            float[] pitchSeq = { 6f, -3f, 7f, -2f };
            for (int i = 0; i < 3; i++)
                shots.Add(new Shot
                {
                    start = i * 3f, duration = 3f,
                    fromPos = eye + Vector3.up * (i % 2 == 0 ? 0.35f : -0.3f),
                    toPos = eye + Vector3.up * (i % 2 == 0 ? -0.3f : 0.35f) + Vector3.forward * 0.3f * i,
                    fromEuler = new Vector3(pitchSeq[i], -4f + i * 2f, rollSeq[i]),
                    toEuler = new Vector3(pitchSeq[i + 1], -2f + i * 2f, rollSeq[i + 1]),
                    fromFov = 62f, toFov = 62f,
                });
            Vector3 bow = B + new Vector3(0.1f, 2.7f, 3.4f);
            shots.Add(new Shot { start = 9f, duration = 3.5f, fromPos = eye + Vector3.forward * 0.9f, toPos = bow, fromEuler = new Vector3(-2f, 2f, 5f), toEuler = new Vector3(1f, 0f, -4f), fromFov = 60f, toFov = 48f });
            shots.Add(new Shot { start = 12.5f, duration = 3.5f, fromPos = bow, toPos = bow + new Vector3(0, 0.3f, 0.2f), fromEuler = new Vector3(1f, 0f, -4f), toEuler = new Vector3(-1f, 1f, 5f), fromFov = 48f, toFov = 44f });
            // 撞礁：猛地前倾、翻滚、坠向海面
            shots.Add(new Shot { start = 16f, duration = 1.1f, fromPos = bow + new Vector3(0, 0.3f, 0.2f), toPos = bow + new Vector3(0.4f, -0.6f, 0.9f), fromEuler = new Vector3(-1f, 1f, 5f), toEuler = new Vector3(32f, 8f, -28f), fromFov = 44f, toFov = 70f, easeOutOnly = true });
            shots.Add(new Shot { start = 17.1f, duration = 2.4f, fromPos = bow + new Vector3(0.4f, -0.6f, 0.9f), toPos = B + new Vector3(2.2f, 0.25f, 4.5f), fromEuler = new Vector3(32f, 8f, -28f), toEuler = new Vector3(75f, 50f, -70f), fromFov = 70f, toFov = 80f });

            var subtitles = new List<(float, float, string)>
            {
                (1.5f, 4f, "第七天。船长说，这片海域没有名字。"),
                (6f, 3.4f, "雷达上什么都没有……"),
                (11.4f, 3.4f, "可我看见了——一座岛。海图上不存在的岛。"),
                (16.3f, 1.4f, "礁石——！"),
                (20.2f, 3f, "（冰冷的海水灌进来。然后，什么都没有了。）"),
            };
            var fades = new List<(float, float, float, float)> { (0f, 2.5f, 1f, 0f), (18.7f, 0.8f, 0f, 1f), (19.5f, 4.5f, 1f, 1f) };

            var cp = Create("CG01_Storm", shots, subtitles, fades, playOnStart: true);
            var director = cp.GetComponent<PlayableDirector>();
            var timeline = (TimelineAsset)director.playableAsset;
            var act = timeline.CreateTrack<ActivationTrack>(null, "风暴");
            var clip = act.CreateDefaultClip();
            clip.start = 0;
            clip.duration = 24f;
            clip.displayName = "风暴与船";
            act.postPlaybackState = ActivationTrack.PostPlaybackState.Inactive;
            director.SetGenericBinding(act, stormGo);
            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssets();
            stormGo.SetActive(false);
            storm.followCamera = cp.cutsceneCamera.transform;

            UnityEventTools.AddPersistentListener(cp.onFinished, next.Play);
            return cp;
        }

        /// <summary>林舟雇的小渔船：船体、甲板、栏杆、驾驶舱、桅杆和一盏灯。船头朝 +Z。</summary>
        static void BuildBoat(Transform parent, Vector3 pos)
        {
            var root = new GameObject("HiredBoat").transform;
            root.SetParent(parent, false);
            root.position = pos;
            var hull = FPSSceneBuilder.Painted("BoatHull", new Color(0.78f, 0.78f, 0.74f), 0.1f, 0.45f);
            var trim = FPSSceneBuilder.Painted("BoatTrim", new Color(0.55f, 0.12f, 0.08f), 0.1f, 0.4f);
            var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Generated/Materials/Crate.mat");
            var glass = FPSSceneBuilder.Painted("BoatGlass", new Color(0.05f, 0.07f, 0.08f), 0.2f, 0.95f);

            void Part(string n, Vector3 p, Vector3 s, Material m, float yaw = 0)
            {
                var go = FPSSceneBuilder.Box(n, root, Vector3.zero, s, m);
                go.isStatic = false;
                go.transform.localPosition = p;
                go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            }
            Part("Hull", new Vector3(0, 0.55f, -0.3f), new Vector3(2.7f, 1.5f, 8.4f), hull);
            Part("Bow", new Vector3(0, 0.55f, 3.9f), new Vector3(1.9f, 1.5f, 1.9f), hull, 45f);
            Part("Stripe", new Vector3(0, 1.15f, -0.3f), new Vector3(2.74f, 0.18f, 8.44f), trim);
            Part("Deck", new Vector3(0, 1.32f, -0.3f), new Vector3(2.5f, 0.08f, 8.2f), wood);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 8; i++)
                    Part("Post", new Vector3(side * 1.25f, 1.8f, -4f + i * 1.05f), new Vector3(0.06f, 0.9f, 0.06f), wood);
                Part("Rail", new Vector3(side * 1.25f, 2.25f, -0.35f), new Vector3(0.08f, 0.06f, 7.4f), wood);
            }
            Part("Cabin", new Vector3(0, 2.3f, -3.1f), new Vector3(2.1f, 1.9f, 2.2f), hull);
            Part("CabinWindow", new Vector3(0, 2.6f, -1.99f), new Vector3(1.6f, 0.6f, 0.04f), glass);
            Part("Mast", new Vector3(0, 4.4f, 0.6f), new Vector3(0.12f, 6.2f, 0.12f), wood);

            var lamp = new GameObject("MastLamp").AddComponent<Light>();
            lamp.transform.SetParent(root, false);
            lamp.transform.localPosition = new Vector3(0, 3.6f, 0.75f);
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, 0.75f, 0.45f);
            lamp.intensity = 2.5f;
            lamp.range = 9f;
            lamp.shadows = LightShadows.Soft;
        }
    }
}
