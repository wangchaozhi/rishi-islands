using System.Collections.Generic;
using System.Linq;
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
    /// 第一章内容：海滩沉船残骸（航海日志、撕下的书页）+ 丛林古神殿（符文石柱谜题、石门、石匣与太阳之心碎片）。
    /// 由 IslandBuilder.Build 调用。
    /// </summary>
    public static class IslandStoryBuilder
    {
        // 神殿位置（世界 xz）和平台高度在 IslandBuilder.HeightAt 里会被压平
        public static readonly Vector2 RuinCenter = new Vector2(-15f, -60f);
        public const float RuinFlatRadius = 15f;
        public const float RuinBlendRadius = 28f;
        /// <summary>神殿朝向：+Z（正面）朝南，对着沙滩方向。</summary>
        public const float RuinYaw = 180f;

        const int WeaponLayer = 8;
        static readonly int[] PillarStart = { 2, 3, 1 };   // 初始朝外的符文
        static readonly int[] PillarAnswer = { 0, 1, 2 };  // 从玩家视角左到右：海浪、太阳、群山
        static readonly string[] GlyphNames = { "wave", "sun", "mountain", "moon" };
        static readonly string[] GlyphLabels = { "海浪", "太阳", "群山", "月亮" };

        public static readonly List<(Vector2 center, float radius)> ClearZones = new List<(Vector2, float)>();

        // ================= 沉船 =================

        public static void BuildWreck(Terrain terrain, Vector3 spawn)
        {
            // 出生点往东南沿海找一段沙滩
            var dir = new Vector2(0.55f, -1f).normalized;
            Vector3 shore = FindShore(terrain, dir);
            var root = new GameObject("Shipwreck").transform;
            AudioSetup.Tag(root.gameObject, "wood");
            float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg + 90f; // 船身大致平行海岸线
            root.position = shore + new Vector3(dir.x, 0, dir.y) * 5f;
            root.rotation = Quaternion.Euler(0, yaw, 0);
            ClearZones.Add((new Vector2(root.position.x, root.position.z), 22f));

            // 船体：去掉帆和索具，侧倾、半埋进沙里
            var shipPrefab = FPSSceneBuilder.LoadModel("Models/ship_pinnace");
            var ship = (GameObject)PrefabUtility.InstantiatePrefab(shipPrefab);
            PrefabUtility.UnpackPrefabInstance(ship, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            ship.transform.SetParent(root, false);
            ship.transform.localRotation = Quaternion.Euler(-4f, 0, 24f) * shipPrefab.transform.localRotation;

            var mats = new Dictionary<string, Material>();
            foreach (var part in new[] { "hull", "deck", "details", "aft", "interior" })
                mats[part] = PbrPrefix($"ship_{part}", "Models/ship_pinnace", $"ship_pinnace_{part}_");
            var removed = new List<string>();
            foreach (var r in ship.GetComponentsInChildren<Renderer>().ToList())
            {
                var names = r.sharedMaterials.Select(m => m ? m.name.ToLower() : "").ToArray();
                if (names.All(n => n.Contains("sail") || n.Contains("rigging") || n.Contains("rope")) || r.name.ToLower().Contains("sail") || r.name.ToLower().Contains("rigging"))
                {
                    removed.Add(r.name);
                    Object.DestroyImmediate(r.gameObject);
                    continue;
                }
                r.sharedMaterials = names.Select(n => mats.FirstOrDefault(kv => n.Contains(kv.Key)).Value ?? mats["hull"]).ToArray();
                if (r is MeshRenderer mr) mr.meshLodSelectionBias = -2f;
            }
            var b = FPSSceneBuilder.Bounds(ship);
            float ground = terrain.SampleHeight(b.center) + terrain.transform.position.y;
            ship.transform.position += Vector3.up * (ground - b.min.y - 1.6f);
            b = FPSSceneBuilder.Bounds(ship);
            Debug.Log($"[Story] ship size {b.size}, removed parts: {string.Join(",", removed)}");

            // 碰撞：船体用一个压扁的盒子近似
            var hullCol = new GameObject("HullCollider").AddComponent<BoxCollider>();
            hullCol.transform.SetParent(root, false);
            hullCol.transform.rotation = ship.transform.rotation;
            hullCol.transform.position = b.center - Vector3.up * b.size.y * 0.2f;
            var local = Quaternion.Inverse(hullCol.transform.rotation) * b.size;
            hullCol.size = new Vector3(Mathf.Abs(local.x) * 0.55f, Mathf.Abs(local.y) * 0.45f, Mathf.Abs(local.z) * 0.85f);

            // 散落的货物
            var crateMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Generated/Materials/Crate.mat");
            Vector3 Beach(float right, float fwd) => Ground(terrain, root.position + root.right * right + root.forward * fwd);
            var crate1 = FPSSceneBuilder.PlaceModel("Models/wooden_crate_02", crateMat, root, Beach(-9f, 7f), yaw + 25f, staticCollider: true);
            FPSSceneBuilder.PlaceModel("Models/wooden_crate_02", crateMat, root, Beach(-10.4f, 6.2f), yaw - 10f, staticCollider: true);
            FPSSceneBuilder.PlaceModel("Models/wooden_crate_02", crateMat, root, Beach(-6f, 12f), yaw + 70f, staticCollider: true);
            var cannonMat = PbrPrefix("cannon", "Models/cannon_01", "cannon_01_");
            var cannon = FPSSceneBuilder.PlaceModel("Models/cannon_01", cannonMat, root, Beach(-4f, -9f), yaw + 140f, staticCollider: true);
            cannon.transform.rotation = Quaternion.Euler(0, 0, 12f) * cannon.transform.rotation;

            // 航海日志：放在箱子上
            var crateTop = FPSSceneBuilder.Bounds(crate1);
            var log = Book(root, new Vector3(crateTop.center.x, crateTop.max.y + 0.03f, crateTop.center.z), yaw + 15f);
            var logNote = log.AddComponent<NoteItem>();
            logNote.note = new NoteData { title = "晨星号船长日志（一）", body = CaptainLog1 };
            logNote.objectiveStage = 1;
            logNote.objectiveText = "这座岛上还有别人来过吗？去丛林边缘找找痕迹";

            // 日志第二页：被撕下来，压在礁石边
            var page = Paper(root, Beach(-12.5f, 1.5f) + Vector3.up * 0.02f, yaw - 30f);
            var pageNote = page.AddComponent<NoteItem>();
            pageNote.verb = "捡起";
            pageNote.note = new NoteData { title = "晨星号船长日志（二）· 撕下的一页", body = CaptainLog2 };
            NoteGlow(log.transform);
            NoteGlow(page.transform);
        }

        static Vector3 FindShore(Terrain terrain, Vector2 dir)
        {
            for (float t = 290f; t > 0; t -= 0.5f)
            {
                var p = new Vector3(dir.x * t, 0, dir.y * t);
                float h = terrain.SampleHeight(p) + terrain.transform.position.y;
                if (h > 0.4f) return new Vector3(p.x, h, p.z);
            }
            return Vector3.zero;
        }

        static GameObject Book(Transform parent, Vector3 pos, float yaw)
        {
            var mat = FPSSceneBuilder.Painted("Leather", new Color(0.28f, 0.14f, 0.07f), 0f, 0.35f);
            var go = new GameObject("CaptainLog");
            go.transform.SetParent(parent);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            FPSSceneBuilder.Part("Cover", go.transform, new Vector3(0, 0.025f, 0), new Vector3(0.24f, 0.05f, 0.32f), mat, true);
            var pagesMat = FPSSceneBuilder.Painted("OldPaper", new Color(0.86f, 0.8f, 0.64f), 0f, 0.1f);
            FPSSceneBuilder.Part("Pages", go.transform, new Vector3(0.008f, 0.025f, 0), new Vector3(0.235f, 0.04f, 0.3f), pagesMat, false);
            return go;
        }

        static GameObject Paper(Transform parent, Vector3 pos, float yaw)
        {
            var mat = FPSSceneBuilder.Painted("OldPaper", new Color(0.86f, 0.8f, 0.64f), 0f, 0.1f);
            var go = new GameObject("TornPage");
            go.transform.SetParent(parent);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(3f, yaw, -2f));
            FPSSceneBuilder.Part("Sheet", go.transform, new Vector3(0, 0.004f, 0), new Vector3(0.22f, 0.008f, 0.3f), mat, true);
            return go;
        }

        /// <summary>笔记上方一盏很弱的暖光，夜里也能找到。</summary>
        static void NoteGlow(Transform t)
        {
            var l = new GameObject("Glow").AddComponent<Light>();
            l.transform.SetParent(t, false);
            l.transform.localPosition = Vector3.up * 0.5f;
            l.type = LightType.Point;
            l.range = 1.6f;
            l.intensity = 0.35f;
            l.color = new Color(1f, 0.8f, 0.5f);
            l.shadows = LightShadows.None;
        }

        // ================= 父亲的营地 =================

        /// <summary>在出生点和神殿之间的丛林边缘搭一个营地：油布棚、篝火圈、木箱、录音笔。</summary>
        public static void BuildCamp(Terrain terrain, Vector3 spawn)
        {
            var temple = new Vector3(RuinCenter.x, 0, RuinCenter.y);
            Vector3 pos = Vector3.Lerp(spawn, temple, 0.35f);
            for (float t = 0.2f; t < 0.8f; t += 0.02f)
            {
                var p = Ground(terrain, Vector3.Lerp(spawn, temple, t));
                float slope = terrain.terrainData.GetSteepness((p.x - terrain.transform.position.x) / terrain.terrainData.size.x,
                                                               (p.z - terrain.transform.position.z) / terrain.terrainData.size.z);
                if (p.y > 4.5f && slope < 14f) { pos = p; break; }
            }
            pos = Ground(terrain, pos);
            ClearZones.Add((new Vector2(pos.x, pos.z), 10f));

            var root = new GameObject("FatherCamp").transform;
            AudioSetup.Tag(root.gameObject, "wood");
            float yaw = Mathf.Atan2(spawn.x - pos.x, spawn.z - pos.z) * Mathf.Rad2Deg; // 营地朝向沙滩
            root.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));

            var canvas = FPSSceneBuilder.Painted("Tarp", new Color(0.58f, 0.52f, 0.36f), 0f, 0.2f);
            var wood = FPSSceneBuilder.Painted("PoleWood", new Color(0.3f, 0.22f, 0.14f), 0f, 0.2f);
            var stone = FPSSceneBuilder.Pbr("RuinStone", "Textures/old_sandstone_02", 1f / 2f);
            var charcoal = FPSSceneBuilder.Painted("Charcoal", new Color(0.05f, 0.045f, 0.04f), 0f, 0.1f);

            // 油布棚：两根立柱 + 斜拉的油布 + 地铺
            PartAt(root, "PoleL", new Vector3(-1.4f, 0.9f, -1.2f), new Vector3(0.07f, 1.8f, 0.07f), wood, Quaternion.identity);
            PartAt(root, "PoleR", new Vector3(1.4f, 0.9f, -1.2f), new Vector3(0.07f, 1.8f, 0.07f), wood, Quaternion.identity);
            PartAt(root, "Tarp", new Vector3(0, 0.95f, -2.0f), new Vector3(3.2f, 0.03f, 2.1f), canvas, Quaternion.Euler(-50f, 0, 0));
            PartAt(root, "Bedroll", new Vector3(0, 0.05f, -2.1f), new Vector3(0.8f, 0.1f, 1.9f), canvas, Quaternion.Euler(0, 6f, 0));

            // 篝火圈（早已熄灭）
            for (int i = 0; i < 9; i++)
            {
                float a = i / 9f * Mathf.PI * 2;
                PartAt(root, "FireStone", new Vector3(Mathf.Cos(a) * 0.55f, 0.08f, 1.4f + Mathf.Sin(a) * 0.55f),
                       new Vector3(0.22f, 0.16f, 0.18f), stone, Quaternion.Euler(Random.Range(-10f, 10f), a * Mathf.Rad2Deg, 0));
            }
            PartAt(root, "Log1", new Vector3(0, 0.06f, 1.4f), new Vector3(0.12f, 0.1f, 0.7f), charcoal, Quaternion.Euler(0, 35f, 0));
            PartAt(root, "Log2", new Vector3(0, 0.08f, 1.4f), new Vector3(0.12f, 0.1f, 0.7f), charcoal, Quaternion.Euler(0, -40f, 0));

            // 木箱 + 录音笔
            var crateMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Generated/Materials/Crate.mat");
            var crate = FPSSceneBuilder.PlaceModel("Models/wooden_crate_02", crateMat, root, Ground(terrain, root.TransformPoint(new Vector3(1.9f, 0, 0.2f))) - Vector3.up * 0.05f, yaw + 12f, staticCollider: true);
            var ct = FPSSceneBuilder.Bounds(crate);
            var recorder = new GameObject("VoiceRecorder");
            recorder.transform.SetParent(root);
            recorder.transform.SetPositionAndRotation(new Vector3(ct.center.x, ct.max.y + 0.001f, ct.center.z), Quaternion.Euler(0, yaw + 70f, 0));
            var plastic = FPSSceneBuilder.Painted("RecorderBody", new Color(0.08f, 0.08f, 0.09f), 0.2f, 0.55f);
            FPSSceneBuilder.Part("Body", recorder.transform, new Vector3(0, 0.015f, 0), new Vector3(0.05f, 0.03f, 0.13f), plastic, true);
            var led = FPSSceneBuilder.Painted("RecorderLed", new Color(1f, 0.1f, 0.05f), 0f, 0.8f);
            led.SetColor("_EmissionColor", new Color(4f, 0.3f, 0.1f));
            FPSSceneBuilder.Part("Led", recorder.transform, new Vector3(0.012f, 0.031f, 0.045f), new Vector3(0.008f, 0.004f, 0.008f), led, false);
            var rec = recorder.AddComponent<NoteItem>();
            rec.verb = "播放";
            rec.note = new NoteData { title = "录音笔 #1 · 林远山", body = FatherTape1 };
            rec.objectiveStage = 2;
            rec.objectiveText = "沿着父亲的足迹，找到丛林深处的神殿";
            NoteGlow(recorder.transform);

            // 短波收音机：放在油布棚边的地上
            var radioPos = Ground(terrain, root.TransformPoint(new Vector3(-1.1f, 0, -0.9f)));
            var radio = new GameObject("ShortwaveRadio");
            radio.transform.SetParent(root);
            radio.transform.SetPositionAndRotation(radioPos, Quaternion.Euler(0, yaw + 160f, 0));
            var radioBody = FPSSceneBuilder.Painted("RadioBody", new Color(0.22f, 0.25f, 0.18f), 0.3f, 0.35f);
            var radioGrill = FPSSceneBuilder.Painted("RadioGrill", new Color(0.08f, 0.08f, 0.08f), 0.6f, 0.4f);
            FPSSceneBuilder.Part("Body", radio.transform, new Vector3(0, 0.09f, 0), new Vector3(0.32f, 0.18f, 0.12f), radioBody, true);
            FPSSceneBuilder.Part("Grill", radio.transform, new Vector3(-0.06f, 0.09f, 0.061f), new Vector3(0.14f, 0.12f, 0.004f), radioGrill, false);
            FPSSceneBuilder.Part("Dial", radio.transform, new Vector3(0.09f, 0.11f, 0.062f), new Vector3(0.06f, 0.06f, 0.006f), radioGrill, false);
            PartAt(radio.transform, "Antenna", new Vector3(0.13f, 0.38f, -0.03f), new Vector3(0.008f, 0.55f, 0.008f), radioGrill, Quaternion.Euler(0, 0, -18f));
            var radioNote = radio.AddComponent<NoteItem>();
            radioNote.verb = "收听";
            radioNote.sound = "radio_static";
            radioNote.note = new NoteData { title = "短波收音机", body = RadioText };
            NoteGlow(radio.transform);

            // 母亲的项链：扔在地铺上（第五岛反转的伏笔）
            var necklace = new GameObject("MotherNecklace");
            necklace.transform.SetParent(root);
            necklace.transform.SetPositionAndRotation(Ground(terrain, root.TransformPoint(new Vector3(0.15f, 0, -1.6f))) + Vector3.up * 0.11f, Quaternion.Euler(0, yaw + 40f, 0));
            var gold = FPSSceneBuilder.Painted("PendantGold", new Color(0.95f, 0.72f, 0.3f), 1f, 0.75f);
            gold.SetColor("_EmissionColor", new Color(0.25f, 0.16f, 0.04f));
            var silver = FPSSceneBuilder.Painted("ChainSilver", new Color(0.8f, 0.8f, 0.82f), 1f, 0.7f);
            PartAt(necklace.transform, "Pendant", new Vector3(0, 0.004f, 0), new Vector3(0.035f, 0.004f, 0.035f), gold, Quaternion.Euler(0, 45f, 0));
            PartAt(necklace.transform, "ChainA", new Vector3(-0.05f, 0.002f, 0.04f), new Vector3(0.002f, 0.002f, 0.12f), silver, Quaternion.Euler(0, 35f, 0));
            PartAt(necklace.transform, "ChainB", new Vector3(0.05f, 0.002f, 0.04f), new Vector3(0.002f, 0.002f, 0.12f), silver, Quaternion.Euler(0, -35f, 0));
            var necklaceNote = necklace.AddComponent<NoteItem>();
            necklaceNote.verb = "捡起";
            necklaceNote.hideOnRead = true;
            necklaceNote.note = new NoteData { title = "母亲的项链", body = NecklaceText };
            var nb = necklace.AddComponent<BoxCollider>();
            nb.size = new Vector3(0.3f, 0.1f, 0.3f);
            nb.center = new Vector3(0, 0.03f, 0);
            NoteGlow(necklace.transform);
            Debug.Log($"[Story] father camp at {pos}");
        }

        static void PartAt(Transform root, string name, Vector3 localPos, Vector3 size, Material mat, Quaternion localRot)
        {
            FPSSceneBuilder.Part(name, root, localPos, size, mat, true);
            root.GetChild(root.childCount - 1).localRotation = localRot;
        }

        public static NoteData Opening => new NoteData { title = "醒来", body = OpeningText };

        // ================= 神殿 =================

        public static void BuildRuin(Terrain terrain)
        {
            ClearZones.Add((RuinCenter, 22f));
            var c = new Vector3(RuinCenter.x, 0, RuinCenter.y);
            c.y = terrain.SampleHeight(c) + terrain.transform.position.y;
            var root = new GameObject("SunTemple").transform;
            AudioSetup.Tag(root.gameObject, "stone");
            root.SetPositionAndRotation(c, Quaternion.Euler(0, RuinYaw, 0));

            var wall = FPSSceneBuilder.Pbr("RuinWall", "Textures/mossy_sandstone", 1f / 2.5f);
            var floor = FPSSceneBuilder.Pbr("RuinFloor", "Textures/mossy_cobblestone", 1f / 2f);
            var stone = FPSSceneBuilder.Pbr("RuinStone", "Textures/old_sandstone_02", 1f / 2f);

            // 平台（向下多埋 1 米，地形起伏处不会悬空）、台阶
            L(root, "Platform", new Vector3(0, -0.15f, -1.5f), new Vector3(16, 1.7f, 18), floor);
            L(root, "Step", new Vector3(0, 0.17f, 8.1f), new Vector3(8, 0.35f, 1.2f), stone);
            const float top = 0.7f;

            // 正立面（中间留门洞 2.4 x 3.2）
            L(root, "FacadeLeft", new Vector3(-4.1f, top + 3f, -3f), new Vector3(5.8f, 6f, 1.6f), wall);
            L(root, "FacadeRight", new Vector3(4.1f, top + 3f, -3f), new Vector3(5.8f, 6f, 1.6f), wall);
            L(root, "Lintel", new Vector3(0, top + 3.2f + 1.4f, -3f), new Vector3(2.4f, 2.8f, 1.6f), wall);
            L(root, "Cornice", new Vector3(0, top + 6.2f, -3f), new Vector3(12.6f, 0.4f, 2.0f), stone);
            L(root, "Cornice2", new Vector3(0, top + 6.6f, -3.2f), new Vector3(9f, 0.5f, 1.6f), stone);

            // 石门（会沉入地下）
            var door = L(root, "StoneDoor", new Vector3(0, top + 1.6f, -3f), new Vector3(2.4f, 3.2f, 0.5f), stone);
            door.isStatic = false;

            // 内室
            L(root, "ChamberBack", new Vector3(0, top + 3f, -9.6f), new Vector3(8f, 6f, 1f), wall);
            L(root, "ChamberLeft", new Vector3(-3.6f, top + 3f, -6.3f), new Vector3(0.8f, 6f, 5.6f), wall);
            L(root, "ChamberRight", new Vector3(3.6f, top + 3f, -6.3f), new Vector3(0.8f, 6f, 5.6f), wall);
            L(root, "ChamberRoof", new Vector3(0, top + 6.3f, -6.3f), new Vector3(8f, 0.6f, 7.4f), wall);
            var altar = L(root, "Altar", new Vector3(0, top + 0.45f, -7.6f), new Vector3(1.9f, 0.9f, 1.1f), stone);

            var reveal = new GameObject("ChamberLight").AddComponent<Light>();
            reveal.transform.SetParent(root, false);
            reveal.transform.localPosition = new Vector3(0, top + 3.2f, -6f);
            reveal.type = LightType.Point;
            reveal.range = 9f;
            reveal.intensity = 0f;
            reveal.color = new Color(1f, 0.72f, 0.4f);
            reveal.shadows = LightShadows.Soft;

            // 柱廊（两根残断）+ 倒下的柱子
            float[] colX = { -6.2f, -4.4f, 4.4f, 6.2f };
            float[] colH = { 5.2f, 3.1f, 5.2f, 1.9f };
            for (int i = 0; i < 4; i++)
            {
                L(root, "Column", new Vector3(colX[i], top + colH[i] / 2, 2.2f), new Vector3(0.9f, colH[i], 0.9f), stone);
                L(root, "ColumnBase", new Vector3(colX[i], top + 0.15f, 2.2f), new Vector3(1.25f, 0.3f, 1.25f), stone);
                if (colH[i] > 5f) L(root, "Capital", new Vector3(colX[i], top + colH[i] + 0.2f, 2.2f), new Vector3(1.3f, 0.4f, 1.3f), stone);
            }
            var fallen = L(root, "FallenColumn", new Vector3(7.2f, top + 0.45f, 5.6f), new Vector3(0.9f, 0.9f, 3.2f), stone);
            fallen.transform.localRotation = Quaternion.Euler(0, 28f, 4f);

            // 符文石柱谜题
            var glyphMats = GlyphNames.Select(n => GlyphMaterial(n)).ToArray();
            var puzzle = new GameObject("PillarPuzzle").AddComponent<PillarPuzzle>();
            puzzle.transform.SetParent(root, false);
            puzzle.door = door.transform;
            puzzle.revealLight = reveal;
            var pillars = new List<GlyphPillar>();
            var glow = new List<Renderer>();
            // 玩家面朝神殿（-Z）时，左手边是 +X
            float[] px = { 2f, 0f, -2f };
            for (int i = 0; i < 3; i++)
            {
                L(root, "PillarBase", new Vector3(px[i], top + 0.15f, 4.2f), new Vector3(0.95f, 0.3f, 0.95f), stone);
                var rotor = new GameObject($"GlyphPillar{i + 1}");
                rotor.transform.SetParent(root, false);
                rotor.transform.localPosition = new Vector3(px[i], top + 0.3f, 4.2f);
                var body = FPSSceneBuilder.Box("Stone", rotor.transform, Vector3.zero, new Vector3(0.62f, 1.5f, 0.62f), stone);
                body.isStatic = false;
                body.transform.localPosition = new Vector3(0, 0.75f, 0);
                body.transform.localRotation = Quaternion.identity;
                var cap = FPSSceneBuilder.Box("Cap", rotor.transform, Vector3.zero, new Vector3(0.78f, 0.18f, 0.78f), stone);
                cap.isStatic = false;
                cap.transform.localPosition = new Vector3(0, 1.59f, 0);
                cap.transform.localRotation = Quaternion.identity;
                for (int f = 0; f < 4; f++)
                {
                    var q = GlyphQuad(rotor.transform, glyphMats[f], 0.48f);
                    var dir = Quaternion.Euler(0, f * 90f, 0) * Vector3.forward;
                    q.transform.localPosition = dir * 0.315f + Vector3.up * 0.85f;
                    q.transform.localRotation = Quaternion.LookRotation(-dir);
                    glow.Add(q.GetComponent<Renderer>());
                }
                var gp = rotor.AddComponent<GlyphPillar>();
                gp.rotor = rotor.transform;
                gp.faceGlyphs = GlyphLabels;
                gp.currentFace = PillarStart[i];
                rotor.transform.localRotation = Quaternion.Euler(0, -PillarStart[i] * 90f, 0); // 编辑器里也显示初始朝向
                gp.correctFace = PillarAnswer[i];
                gp.puzzle = puzzle;
                pillars.Add(gp);
            }
            puzzle.pillars = pillars.ToArray();
            puzzle.glowRenderers = glow.ToArray();

            // 壁画：门左侧（玩家视角）浮雕，海浪 → 太阳 → 群山
            var slab = L(root, "Mural", new Vector3(4.1f, top + 2.3f, -2.1f), new Vector3(4.4f, 1.3f, 0.2f), stone);
            var arrowMat = GlyphMaterial("arrow");
            var seq = new[] { (glyphMats[0], 1.65f), (arrowMat, 0.82f), (glyphMats[1], 0f), (arrowMat, -0.82f), (glyphMats[2], -1.65f) };
            foreach (var (m, x) in seq)
            {
                var q = GlyphQuad(slab.transform, m, 0.75f);
                q.transform.position = slab.transform.position + root.rotation * new Vector3(x, 0, 0.105f); // +X 是玩家左手边
                q.transform.rotation = root.rotation * Quaternion.LookRotation(Vector3.back);
                q.transform.localScale = new Vector3(0.75f / slab.transform.lossyScale.x, 0.75f / slab.transform.lossyScale.y, 1f);
            }
            var mural = slab.AddComponent<NoteItem>();
            mural.verb = "查看";
            mural.note = new NoteData { title = "神殿壁画", body = MuralText };

            // 石匣 + 太阳之心碎片
            var chestMat = PbrPrefix("chest", "Models/treasure_chest", "treasure_chest_");
            var altarTop = FPSSceneBuilder.Bounds(altar);
            var chestPrefab = FPSSceneBuilder.LoadModel("Models/treasure_chest");
            var chest = (GameObject)PrefabUtility.InstantiatePrefab(chestPrefab);
            PrefabUtility.UnpackPrefabInstance(chest, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            chest.transform.SetParent(root, false);
            chest.transform.localRotation = chestPrefab.transform.localRotation;
            FPSSceneBuilder.AssignMaterial(chest, chestMat);
            var cb = FPSSceneBuilder.Bounds(chest);
            chest.transform.position += new Vector3(altarTop.center.x - cb.center.x, altarTop.max.y - cb.min.y, altarTop.center.z - cb.center.z);
            cb = FPSSceneBuilder.Bounds(chest);
            Debug.Log($"[Story] chest size {cb.size}, parts: {string.Join(",", chest.GetComponentsInChildren<Renderer>().Select(r => r.name))}");

            var tc = chest.AddComponent<TreasureChest>();
            var box = chest.AddComponent<BoxCollider>();
            box.center = chest.transform.InverseTransformPoint(cb.center);
            var ls = chest.transform.lossyScale;
            var sz = Quaternion.Inverse(chest.transform.rotation) * cb.size;
            box.size = new Vector3(Mathf.Abs(sz.x / ls.x), Mathf.Abs(sz.y / ls.y), Mathf.Abs(sz.z / ls.z));
            tc.lid = chest.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name.ToLower().Contains("lid"));
            if (tc.lid != null) tc.lid = PivotAtBackEdge(tc.lid, root);
            tc.note = new NoteData { title = "石匣底部的刻文", body = ChestText };

            var frag = Fragment(chest.transform, cb);
            tc.fragment = frag;
            var chestLight = new GameObject("ChestGlow").AddComponent<Light>();
            chestLight.transform.SetParent(chest.transform, false);
            chestLight.transform.position = cb.center + Vector3.up * 0.4f;
            chestLight.type = LightType.Point;
            chestLight.range = 5f;
            chestLight.intensity = 0f;
            chestLight.color = new Color(1f, 0.75f, 0.35f);
            tc.glow = chestLight;

            // 船长日志（三）：散落在台阶上
            var log3 = Paper(root, root.TransformPoint(new Vector3(1.6f, 0.36f, 8.3f)), RuinYaw + 20f);
            var log3Note = log3.AddComponent<NoteItem>();
            log3Note.verb = "捡起";
            log3Note.note = new NoteData { title = "晨星号船长日志（三）", body = CaptainLog3 };
            NoteGlow(log3.transform);

            // 内室：环境声区域 + 混响
            var inner = new GameObject("ChamberAmbience").AddComponent<BoxCollider>();
            inner.transform.SetParent(root, false);
            inner.transform.localPosition = new Vector3(0, top + 2.5f, -6.3f);
            inner.size = new Vector3(6.4f, 5f, 5.6f);
            inner.isTrigger = true;
            inner.gameObject.AddComponent<AmbientZone>().zone = "temple";
            AudioSetup.ReverbZone(root, new Vector3(0, top + 2.5f, -6.3f), 4f, 9f, AudioReverbPreset.StoneCorridor);

            // 发现神殿的触发区
            var trig = new GameObject("TempleTrigger").AddComponent<BoxCollider>();
            trig.transform.SetParent(root, false);
            trig.transform.localPosition = new Vector3(0, 3f, 4f);
            trig.size = new Vector3(26f, 8f, 22f);
            trig.isTrigger = true;
            var ot = trig.gameObject.AddComponent<ObjectiveTrigger>();
            ot.objectiveStage = 3;
            ot.objectiveText = "解开神殿石门的机关";
            ot.toast = "你发现了一座被丛林吞没的古老神殿";
        }

        /// <summary>以箱盖底部后沿为转轴：建一个空节点作父级，旋转它就是开盖。</summary>
        static Transform PivotAtBackEdge(Transform lid, Transform templeRoot)
        {
            var r = lid.GetComponent<Renderer>();
            if (r == null) return lid;
            var b = r.bounds;
            // 神殿正面朝 +Z（本地），箱子开口朝玩家，后沿在 -Z 一侧
            Vector3 back = templeRoot.rotation * Vector3.back;
            var hinge = new GameObject("LidHinge").transform;
            hinge.SetParent(lid.parent, true);
            hinge.position = new Vector3(b.center.x, b.min.y, b.center.z) + back * b.extents.z;
            hinge.rotation = templeRoot.rotation;
            lid.SetParent(hinge, true);
            return hinge;
        }

        static Transform Fragment(Transform chest, Bounds chestBounds)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Art/Generated/Island/Meshes/SunShard.asset");
            if (mesh == null)
            {
                mesh = new Mesh { name = "SunShard" };
                var v = new[] { new Vector3(0, 0.16f, 0), new Vector3(0.05f, 0, 0), new Vector3(0, 0, 0.05f), new Vector3(-0.05f, 0, 0), new Vector3(0, 0, -0.05f), new Vector3(0, -0.09f, 0) };
                var t = new[] { 0, 2, 1, 0, 3, 2, 0, 4, 3, 0, 1, 4, 5, 1, 2, 5, 2, 3, 5, 3, 4, 5, 4, 1 };
                // 拆成平直着色
                var verts = t.Select(i => v[i]).ToArray();
                mesh.vertices = verts;
                mesh.triangles = Enumerable.Range(0, verts.Length).ToArray();
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, "Assets/Art/Generated/Island/Meshes/SunShard.asset");
            }
            var mat = FPSSceneBuilder.Painted("SunShard", new Color(1f, 0.72f, 0.25f), 0.2f, 0.9f);
            mat.SetColor("_EmissionColor", new Color(5f, 2.8f, 0.8f));
            EditorUtility.SetDirty(mat);

            var go = new GameObject("SunHeartFragment");
            go.transform.SetParent(chest, true);
            go.transform.position = chestBounds.center + Vector3.up * chestBounds.extents.y * 0.3f;
            go.transform.localScale = Vector3.one * 1.6f / Mathf.Max(0.0001f, chest.lossyScale.x);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            go.SetActive(false);
            return go.transform;
        }

        // ================= 工具 =================

        static GameObject L(Transform root, string name, Vector3 localPos, Vector3 size, Material mat)
        {
            var go = FPSSceneBuilder.Box(name, root, Vector3.zero, size, mat);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            return go;
        }

        static GameObject GlyphQuad(Transform parent, Material mat, float size)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.name = "Glyph";
            q.transform.SetParent(parent, false);
            q.transform.localScale = Vector3.one * size;
            var r = q.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return q;
        }

        static Material GlyphMaterial(string name)
        {
            var mat = MakeLit($"Glyph_{name}");
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Art/Ruin/glyph_{name}_albedo.png"));
            mat.SetColor("_BaseColor", new Color(0.16f, 0.12f, 0.08f));
            mat.SetFloat("_Smoothness", 0.25f);
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", 0.4f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetOverrideTag("RenderType", "TransparentCutout");
            mat.renderQueue = (int)RenderQueue.AlphaTest;
            mat.SetColor("_EmissionColor", Color.black);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material PbrPrefix(string name, string folder, string prefix)
        {
            var mat = MakeLit(name);
            string dir = $"Assets/Art/{folder}";
            mat.SetTexture("_BaseMap", FPSSceneBuilder.FindTex(dir, prefix + "albedo"));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_BumpMap", FPSSceneBuilder.FindTex(dir, prefix + "normal"));
            mat.EnableKeyword("_NORMALMAP");
            mat.SetTexture("_MetallicGlossMap", FPSSceneBuilder.FindTex(dir, prefix + "ms"));
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            mat.SetFloat("_Smoothness", 1f);
            mat.SetTexture("_OcclusionMap", FPSSceneBuilder.FindTex(dir, prefix + "ao"));
            mat.EnableKeyword("_OCCLUSIONMAP");
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material MakeLit(string name)
        {
            string path = $"Assets/Art/Generated/Island/Materials/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }

        static Vector3 Ground(Terrain terrain, Vector3 p)
        {
            p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
            return p;
        }

        public static bool InClearZone(float x, float z, float extra = 0f)
        {
            foreach (var (c, r) in ClearZones)
                if ((new Vector2(x, z) - c).sqrMagnitude < (r + extra) * (r + extra)) return true;
            return false;
        }

        // ================= 文本 =================

        const string OpeningText =
            "……咸水呛进喉咙，我咳醒过来。\n\n" +
            "天亮了。风暴过去了，老周和他的船都不见了。沙滩上只有我，和远处一艘比我们的船古老几百年的残骸。\n\n" +
            "落水的那一刻，我又看见了陈默。十年前，四十米深的那艘沉船里，他隔着舱壁敲了三下。我的气瓶只剩一百巴，我选择了上浮。\n\n" +
            "这十年，我再没下过四十米。\n\n" +
            "口袋里那封信还在，被海水泡得发皱。父亲的字迹只剩最后一行还能看清：\n\n" +
            "「别来找我。」\n\n" +
            "可我已经来了，爸。\n\n" +
            "—— 林舟";

        const string RadioText =
            "（你拧开开关。一阵刺耳的杂音之后，传来断断续续的英语新闻。）\n\n" +
            "「……天文学家称，今日清晨，南太平洋部分海域观测到一次短暂的日偏食，持续约四十秒。这次日食没有出现在任何天文预报中……专家认为可能是观测误差……」\n\n" +
            "（杂音。有那么一两秒，你好像听见杂音底下有人在低声说话。）";

        const string NecklaceText =
            "一条细细的银链，坠子是一枚磨得发亮的金色圆片，上面刻着一个像太阳、又像眼睛的图案。\n\n" +
            "这是母亲的项链。她去世以后，父亲一直戴着它，十五年没摘下来过。\n\n" +
            "现在它被扔在营地的地铺上，链扣断了。\n\n" +
            "圆片背面刻着几个你看不懂的符号，和沉船日志里画的那些很像。";

        const string CaptainLog1 =
            "1721年3月14日\n\n" +
            "风暴持续了三天三夜。晨星号的龙骨撞上暗礁时，我听见整艘船在哀鸣。\n\n" +
            "我们终究找到了它——那座任何海图上都不存在的岛。土著向导说，岛上曾住着「太阳的子民」，" +
            "他们把「太阳之心」劈成七块，藏进七座岛上的神殿。\n\n" +
            "水手们说这是诅咒。可我在丛林边缘看见了石头砌成的台阶。\n\n" +
            "明早我会带上剩下的人往岛的深处走。愿上帝……或者随便什么神，保佑我们。\n\n" +
            "—— 船长 埃利亚斯·范德维尔";

        const string CaptainLog2 =
            "1721年3月17日\n\n" +
            "又少了两个人。范·霍恩和小汤姆。他们守夜，天亮时只剩两盏还在燃烧的灯，和两串脚印——脚印一直走进了海里。\n\n" +
            "向导说了更多关于神殿的事。他用炭笔在这页纸上画了三个符号：一道浪，一轮日，一座山。他说门只为懂得太阳一生的人打开——\n" +
            "「它从海中升起，在天顶燃烧，最后沉入群山。」\n\n" +
            "他还说了一句话，我不愿意写下来。\n\n" +
            "……我还是写下来吧：「不要让灯熄灭。它们在光外面等着。」";

        const string CaptainLog3 =
            "3月19日……还是20日？\n\n" +
            "神殿的门开了。石匣就在里面。彼得想把它撬开，向导扑上去拦住他，跪在地上哭，说打开它，太阳就会缺一块。\n\n" +
            "我们最后把门重新封上了。\n\n" +
            "可是那天晚上，太阳还是落得很快。太快了。\n\n" +
            "灯灭了。灯灭了。灯灭了。\n\n" +
            "它们没有脸。\n\n" +
            "我听见彼得在林子里喊我的名字。可我亲眼看着他走进了海里。\n\n" +
            "不要回应。不要回应。不要回应。\n\n" +
            "—— E.V.";

        const string FatherTape1 =
            "（录音开始。海浪声，篝火噼啪作响。）\n\n" +
            "「……第十二天。补给只够再撑一周了。\n\n" +
            "晨星号的残骸证明了我的推断：三百年前确实有人到过这里。船长的日志我找到了两页，第三页应该在神殿附近。\n\n" +
            "阿玛拉人的文字，和我在博物馆里破译的那批石板完全一致。如果壁画说的是真的，七块碎片合在一起，就能……\n\n" +
            "（长时间的停顿）\n\n" +
            "……阿晴，你从来不肯告诉我你的家乡在哪儿。你只说，那是一片太阳会生病的海。\n\n" +
            "我想，我终于找到了。\n\n" +
            "小舟，如果你听到这段录音，说明你没听我的话。你从小就不听我的话。\n\n" +
            "回去吧。趁天还亮着。」\n\n" +
            "（录音的最后几秒，背景里有一个不属于篝火的声音：像很多人在远处低声说话。）";

        const string MuralText =
            "石壁上的浮雕已经风化，但依然能辨认出三个符号，由箭头连在一起：\n\n" +
            "海浪 → 太阳 → 群山\n\n" +
            "浮雕下方，三根刻着符文的石柱静静立在门前，每根石柱的四面各刻着一个符号。";

        const string ChestText =
            "碎片在你掌心微微发烫，像一颗仍在跳动的心脏。\n\n" +
            "石匣底部刻着一行古老的文字，旁边是一个潮水退去的图案：\n\n" +
            "「第二块心，沉睡在海水退去之处。」";
    }
}
