using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Rendering;
using Rishi.Audio;
using Rishi.Core;
using Rishi.Story;
using Rishi.Vehicles;

namespace Rishi.Builders
{
    /// <summary>
    /// 小艇：沉船旁的海滩上搁着一条船底破了洞的木划艇（程序化船体，带两支桨和船尾的小马达）。
    /// 修船要 3 块木板（沉船周围的沙滩上）和 1 捆绳子（父亲营地）。修好后目标变成「驶向环礁」，
    /// 第一次上船播放 CG06 启航，在环礁上下船推进目标。
    /// </summary>
    public static class BoatBuilder
    {
        const float Length = 4.2f, HalfBeam = 0.75f, Depth = 0.6f;

        public static Boat Build(Terrain terrain, Vector3 spawn)
        {
            var wreck = GameObject.Find("Shipwreck");
            if (wreck == null) { Debug.LogWarning("[Boat] 场景里没有沉船，跳过小艇"); return null; }
            var w = wreck.transform.position;

            // 沉船往出生点方向的海滩上找个位置：船头朝下坡（海）的方向，往外 40 米的航线不能碰到沉船
            var toSpawn = spawn - w; toSpawn.y = 0; toSpawn.Normalize();
            var wreckBounds = FPSSceneBuilder.Bounds(wreck);
            wreckBounds.Expand(new Vector3(6f, 20f, 6f));
            float H(Vector3 p) => IslandStoryBuilder.Ground(terrain, p).y;
            Vector3 pos = w, n = Vector3.forward;
            for (float along = 14f; along < 60f; along += 2f)
            {
                var basePos = w + toSpawn * along;
                var grad = new Vector3(H(basePos + Vector3.right * 2f) - H(basePos - Vector3.right * 2f), 0, H(basePos + Vector3.forward * 2f) - H(basePos - Vector3.forward * 2f));
                n = grad.sqrMagnitude > 1e-6f ? -grad.normalized : new Vector3(basePos.x, 0, basePos.z).normalized; // 下坡 = 朝海
                // 找水线
                Vector3 water = basePos; bool found = false;
                for (float t = -10f; t < 40f; t += 0.25f)
                {
                    var p = basePos + n * t;
                    if (H(p) < -0.05f) { water = p; found = true; break; }
                }
                if (!found) continue;
                bool clear = true;
                for (float t = -3f; t < 40f && clear; t += 1f)
                {
                    var p = water + n * t; p.y = 0f;
                    if (wreckBounds.Contains(p)) clear = false;
                }
                pos = IslandStoryBuilder.Ground(terrain, water - n * 0.7f);
                if (clear) break;
            }
            float yaw = Mathf.Atan2(n.x, n.z) * Mathf.Rad2Deg;

            var boat = Model(pos, yaw);
            boat.plankItem = GameplaySetup.Item("plank");
            boat.ropeItem = GameplaySetup.Item("rope");
            boat.SetRepaired(false);
            IslandStoryBuilder.ClearZones.Add((new Vector2(pos.x, pos.z), 4f));

            PlaceMaterials(terrain, wreck.transform, spawn, boat.transform.position);

            // 目标：修好 → 驶向环礁 → 登上环礁
            var story = new GameObject("BoatObjectives").transform;
            story.SetParent(boat.transform, false);
            var repaired = story.gameObject.AddComponent<ObjectiveStep>();
            repaired.objectiveStage = 9;
            repaired.objectiveText = "坐上小艇（按 E），驶向东北方海上的环礁";
            UnityEventTools.AddPersistentListener(boat.onRepaired, repaired.Apply);
            var landed = new GameObject("Landed").AddComponent<ObjectiveStep>();
            landed.transform.SetParent(story, false);
            landed.objectiveStage = 10;
            landed.objectiveText = "登上了环礁。退潮的时候，看看水下露出来的东西（开发中）";
            UnityEventTools.AddPersistentListener(boat.onReachAtoll, landed.Apply);

            BuildDepartureCutscene(boat);
            Debug.Log($"[Boat] 小艇放在 {boat.transform.position}，朝向 {yaw:0}°");
            return boat;
        }

        // ================= 模型 =================

        static Boat Model(Vector3 pos, float yaw)
        {
            var paint = FPSSceneBuilder.Painted("DinghyPaint", new Color(0.2f, 0.38f, 0.4f), 0f, 0.35f);
            var inner = FPSSceneBuilder.Painted("DinghyWood", new Color(0.42f, 0.3f, 0.18f), 0f, 0.25f);
            var dark = FPSSceneBuilder.Painted("DinghyHole", new Color(0.03f, 0.025f, 0.02f), 0f, 0.1f);
            var patch = FPSSceneBuilder.Painted("DinghyPatch", new Color(0.55f, 0.42f, 0.26f), 0f, 0.3f);
            var rope = FPSSceneBuilder.Painted("Rope", new Color(0.62f, 0.52f, 0.34f), 0f, 0.15f);
            var metal = FPSSceneBuilder.Painted("MotorBody", new Color(0.12f, 0.13f, 0.14f), 0.4f, 0.5f);
            var motorPaint = FPSSceneBuilder.Painted("MotorCowl", new Color(0.62f, 0.12f, 0.08f), 0.1f, 0.55f);

            var root = new GameObject("Dinghy");
            root.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            AudioSetup.Tag(root, "wood");

            var hull = new GameObject("Hull");
            hull.transform.SetParent(root.transform, false);
            hull.AddComponent<MeshFilter>().sharedMesh = HullMesh();
            var hr = hull.AddComponent<MeshRenderer>();
            hr.sharedMaterials = new[] { paint, inner };
            hr.shadowCastingMode = ShadowCastingMode.TwoSided;
            // 船舱口的深度遮罩：海面不会画进船里
            var mask = new GameObject("WaterMask");
            mask.transform.SetParent(root.transform, false);
            mask.AddComponent<MeshFilter>().sharedMesh = MaskMesh();
            var mr = mask.AddComponent<MeshRenderer>();
            mr.sharedMaterial = IslandBuilder.MakeMaterial("DinghyWaterMask", "Island/DepthMask");
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;

            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0, Depth * 0.5f, 0);
            col.size = new Vector3(HalfBeam * 2f, Depth, Length * 0.92f);

            // 座板
            foreach (var z in new[] { -1.2f, -0.2f, 0.9f })
                FPSSceneBuilder.Part("Thwart", root.transform, new Vector3(0, 0.4f, z), new Vector3(HalfBeam * 2f * (z > 0.5f ? 0.82f : 0.9f), 0.04f, 0.24f), inner, false);

            // 桨：桨架在中间座板两侧，桨杆朝后斜放
            var oars = new List<Transform>();
            for (int side = -1; side <= 1; side += 2)
            {
                var pivot = new GameObject(side < 0 ? "OarL" : "OarR").transform;
                pivot.SetParent(root.transform, false);
                pivot.localPosition = new Vector3(side * (HalfBeam - 0.04f), Depth + 0.02f, -0.2f);
                pivot.localRotation = Quaternion.Euler(0, side * -70f, side * 10f);
                FPSSceneBuilder.Part("Shaft", pivot, new Vector3(side * 0.6f, 0, 0), new Vector3(2.0f, 0.04f, 0.04f), inner, false);
                FPSSceneBuilder.Part("Blade", pivot, new Vector3(side * 1.45f, 0, 0), new Vector3(0.5f, 0.02f, 0.16f), inner, false);
                oars.Add(pivot);
            }

            // 船尾的小马达
            var motor = new GameObject("Motor").transform;
            motor.SetParent(root.transform, false);
            motor.localPosition = new Vector3(0, Depth, -Length / 2f - 0.08f);
            FPSSceneBuilder.Part("Cowl", motor, new Vector3(0, 0.2f, -0.12f), new Vector3(0.26f, 0.32f, 0.36f), motorPaint, false);
            FPSSceneBuilder.Part("Clamp", motor, new Vector3(0, 0.02f, 0.02f), new Vector3(0.16f, 0.12f, 0.1f), metal, false);
            FPSSceneBuilder.Part("Shaft", motor, new Vector3(0, -0.45f, -0.12f), new Vector3(0.07f, 0.9f, 0.09f), metal, false);
            FPSSceneBuilder.Part("Tiller", motor, new Vector3(0, 0.12f, 0.32f), new Vector3(0.04f, 0.04f, 0.5f), metal, false);
            var prop = new GameObject("Propeller").transform;
            prop.SetParent(motor, false);
            prop.localPosition = new Vector3(0, -0.88f, -0.2f);
            for (int b = 0; b < 3; b++)
            {
                var blade = new GameObject("Blade" + b).transform;
                blade.SetParent(prop, false);
                blade.localRotation = Quaternion.Euler(0, 0, b * 120f);
                FPSSceneBuilder.Part("B", blade, new Vector3(0, 0.07f, 0), new Vector3(0.05f, 0.13f, 0.012f), metal, false);
            }

            // 修好之前：船底一侧的破洞；旁边是撬下来的碎板
            var broken = new GameObject("Broken").transform;
            broken.SetParent(root.transform, false);
            var hole = new GameObject("Hole").transform;
            hole.SetParent(broken, false);
            hole.localPosition = new Vector3(HalfBeam * 0.86f, 0.2f, 0.3f);
            hole.localRotation = Quaternion.Euler(0, 0, 32f);
            FPSSceneBuilder.Part("Gap", hole, Vector3.zero, new Vector3(0.05f, 0.22f, 0.55f), dark, false);
            FPSSceneBuilder.Part("Gap2", broken, new Vector3(0.05f, 0.06f, 0.35f), new Vector3(0.5f, 0.02f, 0.3f), dark, false);
            var shard = new GameObject("Shard").transform;
            shard.SetParent(broken, false);
            shard.localPosition = new Vector3(HalfBeam + 0.45f, 0.03f, 0.1f);
            shard.localRotation = Quaternion.Euler(0, 25f, 6f);
            FPSSceneBuilder.Part("S", shard, Vector3.zero, new Vector3(0.12f, 0.03f, 0.7f), paint, false);

            // 修好之后：钉上去的木板补丁，船头系着绳子
            var repaired = new GameObject("Repaired").transform;
            repaired.SetParent(root.transform, false);
            var patchT = new GameObject("Patch").transform;
            patchT.SetParent(repaired, false);
            patchT.localPosition = new Vector3(HalfBeam * 0.9f, 0.2f, 0.3f);
            patchT.localRotation = Quaternion.Euler(0, 0, 32f);
            for (int i = 0; i < 3; i++)
                FPSSceneBuilder.Part("Plank" + i, patchT, new Vector3(0.01f, -0.1f + i * 0.1f, 0.02f * i), new Vector3(0.03f, 0.09f, 0.72f), patch, false);
            FPSSceneBuilder.Part("InnerPatch", repaired, new Vector3(0.05f, 0.07f, 0.35f), new Vector3(0.56f, 0.025f, 0.4f), patch, false);
            for (int i = 0; i < 4; i++)
            {
                var wrap = new GameObject("RopeWrap" + i).transform;
                wrap.SetParent(repaired, false);
                wrap.localPosition = new Vector3(0, Depth + 0.1f, Length / 2f - 0.35f);
                wrap.localRotation = Quaternion.Euler(90f, i * 45f, 0);
                FPSSceneBuilder.Part("R", wrap, Vector3.zero, new Vector3(0.18f, 0.02f, 0.18f), rope, false);
            }

            // 座位：玩家坐在后座板上，靠着马达的舵柄
            var seat = new GameObject("Seat").transform;
            seat.SetParent(root.transform, false);
            seat.localPosition = new Vector3(0, 0.55f, -1.15f);

            var boat = root.AddComponent<Boat>();
            boat.seat = seat;
            boat.oars = oars.ToArray();
            boat.propeller = prop;
            boat.brokenParts = broken.gameObject;
            boat.repairedParts = repaired.gameObject;
            boat.halfLength = Length / 2f;
            boat.motorSource = Loop(root.transform, "boat_motor", new Vector3(0, Depth, -Length / 2f), 3f, 70f);
            boat.waterSource = Loop(root.transform, "boat_water", new Vector3(0, 0.2f, 0.8f), 1.5f, 18f);
            boat.waterSource.playOnAwake = true;
            return boat;
        }

        static AudioSource Loop(Transform parent, string id, Vector3 localPos, float minDist, float maxDist)
        {
            var go = new GameObject("Audio_" + id);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var src = go.AddComponent<AudioSource>();
            src.clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Audio/Final/{id}.wav") ?? AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Audio/Placeholder/{id}.wav");
            src.loop = true;
            src.playOnAwake = false;
            src.volume = 0f;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = minDist;
            src.maxDistance = maxDist;
            src.dopplerLevel = 0f;
            return src;
        }

        /// <summary>
        /// 船体网格：外壳（船漆）+ 内壳（木色）+ 船舷沿 + 船尾板。龙骨底部在 y = 0，船头朝 +Z。
        /// 两个子网格：0 外侧，1 内侧。
        /// </summary>
        static Mesh HullMesh()
        {
            const string path = IslandBuilder.Gen + "/Meshes/DinghyHull.asset";
            const int nz = 28, nt = 18;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var outerTris = new List<int>();
            var innerTris = new List<int>();

            Vector3 Point(float u, float th, bool innerShell)
            {
                float half = HalfBeam * Mathf.Pow(Mathf.Max(Mathf.Sin(Mathf.PI * (0.2f + 0.8f * u)), 0f), 0.55f);
                float top = Depth + 0.2f * Mathf.Pow(u, 2.5f);
                float bottom = 0.25f * Mathf.Pow(Mathf.Max(0f, (u - 0.72f) / 0.28f), 2f); // 船头的龙骨往上翘
                if (innerShell) { half = Mathf.Max(half * 0.94f - 0.025f, 0f); bottom += 0.05f; }
                float c = Mathf.Cos(th), s = Mathf.Sin(th);
                float x = half * Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), 0.7f);
                float y = top - (top - bottom) * Mathf.Pow(Mathf.Max(s, 0f), 0.6f); // sin(π) 会是很小的负数，Pow 出 NaN
                return new Vector3(x, y, -Length / 2f + Length * u);
            }

            int Grid(bool innerShell)
            {
                int start = verts.Count;
                for (int i = 0; i < nz; i++)
                    for (int j = 0; j < nt; j++)
                    {
                        float u = i / (nz - 1f), th = j / (nt - 1f) * Mathf.PI;
                        verts.Add(Point(u, th, innerShell));
                        uvs.Add(new Vector2(u * Length, th));
                    }
                var tris = innerShell ? innerTris : outerTris;
                for (int i = 0; i < nz - 1; i++)
                    for (int j = 0; j < nt - 1; j++)
                    {
                        int a = start + i * nt + j, b = a + 1, c = a + nt, d = c + 1;
                        // 外壳法线朝外，内壳法线朝船舱里
                        if (innerShell) { tris.Add(a); tris.Add(b); tris.Add(c); tris.Add(b); tris.Add(d); tris.Add(c); }
                        else { tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(b); tris.Add(c); tris.Add(d); }
                    }
                return start;
            }
            int outer = Grid(false), inner = Grid(true);

            // 船舷沿：外壳顶边连到内壳顶边（左右两侧）
            foreach (int j in new[] { 0, nt - 1 })
                for (int i = 0; i < nz - 1; i++)
                {
                    int s0 = verts.Count;
                    verts.Add(verts[outer + i * nt + j]); verts.Add(verts[outer + (i + 1) * nt + j]);
                    verts.Add(verts[inner + i * nt + j]); verts.Add(verts[inner + (i + 1) * nt + j]);
                    for (int k = 0; k < 4; k++) uvs.Add(Vector2.zero);
                    if (j == 0) { innerTris.AddRange(new[] { s0, s0 + 2, s0 + 1, s0 + 1, s0 + 2, s0 + 3 }); }
                    else { innerTris.AddRange(new[] { s0, s0 + 1, s0 + 2, s0 + 1, s0 + 3, s0 + 2 }); }
                }

            // 船尾板：外侧一面（朝 -Z）、内侧一面（朝 +Z）
            int ob = verts.Count;
            var oc = Vector3.zero;
            for (int j = 0; j < nt; j++) { verts.Add(verts[outer + j]); uvs.Add(Vector2.zero); oc += verts[outer + j]; }
            verts.Add(new Vector3(0, Depth * 0.9f, -Length / 2f)); uvs.Add(Vector2.zero);
            int ocI = verts.Count - 1;
            for (int j = 0; j < nt - 1; j++) { outerTris.Add(ocI); outerTris.Add(ob + j); outerTris.Add(ob + j + 1); }
            int ib = verts.Count;
            for (int j = 0; j < nt; j++) { verts.Add(verts[inner + j] + Vector3.forward * 0.02f); uvs.Add(Vector2.zero); }
            verts.Add(new Vector3(0, Depth * 0.9f, -Length / 2f + 0.02f)); uvs.Add(Vector2.zero);
            int icI = verts.Count - 1;
            for (int j = 0; j < nt - 1; j++) { innerTris.Add(icI); innerTris.Add(ib + j + 1); innerTris.Add(ib + j); }

            var mesh = new Mesh { name = "DinghyHull" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(outerTris, 0);
            mesh.SetTriangles(innerTris, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            IslandBuilder.SaveAsset(mesh, path);
            return mesh;
        }

        /// <summary>船舷高度的平面，形状和船舱口一样（比内壳稍窄一点，不露边）。</summary>
        static Mesh MaskMesh()
        {
            const string path = IslandBuilder.Gen + "/Meshes/DinghyWaterMask.asset";
            const int nz = 28;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i < nz; i++)
            {
                float u = i / (nz - 1f);
                float half = Mathf.Max(HalfBeam * Mathf.Pow(Mathf.Max(Mathf.Sin(Mathf.PI * (0.2f + 0.8f * u)), 0f), 0.55f) * 0.94f - 0.04f, 0f);
                float top = Depth + 0.2f * Mathf.Pow(u, 2.5f) - 0.02f;
                float z = -Length / 2f + 0.03f + (Length - 0.06f) * u;
                verts.Add(new Vector3(-half, top, z));
                verts.Add(new Vector3(half, top, z));
                if (i > 0)
                {
                    int a = (i - 1) * 2, b = a + 1, c = a + 2, d = a + 3;
                    tris.AddRange(new[] { a, c, b, b, c, d });
                }
            }
            var mesh = new Mesh { name = "DinghyWaterMask" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            IslandBuilder.SaveAsset(mesh, path);
            return mesh;
        }

        // ================= 修船材料 =================

        static void PlaceMaterials(Terrain terrain, Transform wreck, Vector3 spawn, Vector3 boatPos)
        {
            var root = new GameObject("BoatParts").transform;
            var wood = FPSSceneBuilder.Painted("DinghyPatch", new Color(0.55f, 0.42f, 0.26f), 0f, 0.3f);
            var w = wreck.position;
            var toSpawn = spawn - w; toSpawn.y = 0; toSpawn.Normalize();
            var side = Vector3.Cross(Vector3.up, toSpawn);
            // 三块船板：一块在沉船船身旁，一块冲上了沙滩高处，一块在小艇和沉船之间
            var spots = new[] { w - side * 4.5f + toSpawn * 2f, w + toSpawn * 5f + side * 7f, Vector3.Lerp(w, boatPos, 0.55f) + side * 2.5f };
            for (int i = 0; i < spots.Length; i++)
            {
                var p = IslandStoryBuilder.Ground(terrain, spots[i]);
                if (p.y < 0.3f) p = IslandStoryBuilder.Ground(terrain, spots[i] - new Vector3(p.x, 0, p.z).normalized * 4f); // 别落在水里
                var plank = new GameObject("PlankPickup" + i);
                plank.transform.SetParent(root);
                plank.transform.SetPositionAndRotation(p + Vector3.up * 0.03f, Quaternion.Euler(0, 30f + i * 55f, i == 1 ? 8f : 0f));
                FPSSceneBuilder.Part("Board", plank.transform, Vector3.zero, new Vector3(0.2f, 0.035f, 1.1f), wood, false);
                GameplaySetup.MakePickup(plank, GameplaySetup.Item("plank"), 1, "捡起", new Vector3(0.3f, 0.12f, 1.15f));
            }

            // 一捆绳子在父亲营地的木箱旁
            var camp = GameObject.Find("FatherCamp");
            if (camp != null)
            {
                var rp = IslandStoryBuilder.Ground(terrain, camp.transform.TransformPoint(new Vector3(2.1f, 0, -0.9f)));
                var coil = new GameObject("RopePickup");
                coil.transform.SetParent(root);
                coil.transform.SetPositionAndRotation(rp, Quaternion.Euler(0, 20f, 0));
                var rope = FPSSceneBuilder.Painted("Rope", new Color(0.62f, 0.52f, 0.34f), 0f, 0.15f);
                for (int i = 0; i < 6; i++)
                {
                    var wrap = new GameObject("Loop" + i).transform;
                    wrap.SetParent(coil.transform, false);
                    wrap.localPosition = new Vector3(0, 0.03f + i * 0.012f, 0);
                    wrap.localRotation = Quaternion.Euler(0, i * 30f, 0);
                    FPSSceneBuilder.Part("R", wrap, Vector3.zero, new Vector3(0.34f - i * 0.012f, 0.022f, 0.34f - i * 0.012f), rope, false);
                }
                GameplaySetup.MakePickup(coil, GameplaySetup.Item("rope"), 1, "捡起", new Vector3(0.4f, 0.15f, 0.4f));
            }
        }

        // ================= CG06 启航 =================

        static void BuildDepartureCutscene(Boat boat)
        {
            var b = boat.transform;
            float Yaw(Vector3 d) => Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float Pitch(Vector3 d) => -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            Vector3 Look(Vector3 from, Vector3 to) { var d = to - from; return new Vector3(Pitch(d), Yaw(d), 0); }

            var boatTop = b.position + Vector3.up * 0.8f;
            // 1：从岸上低机位看小艇，后面是大海
            var a1 = b.position - b.forward * 5.5f + b.right * 2.2f + Vector3.up * 1.6f;
            var a2 = b.position - b.forward * 4.2f + b.right * 1.2f + Vector3.up * 1.4f;
            // 2：从船后低处越过船头看向开阔的海面（环礁在岛的另一边，先划出海湾）
            var sea = Vector3.ProjectOnPlane(b.forward, Vector3.up).normalized;
            var c1 = b.position - sea * 3.2f + b.right * 0.5f + Vector3.up * 1.7f;
            var c2 = b.position - sea * 2.0f + b.right * 0.3f + Vector3.up * 1.5f;
            var horizon = b.position + sea * 400f + Vector3.up * 3f;
            // 3：海上高处回望：小艇、沙滩和后面的雨林
            var d1 = b.position + sea * 26f + b.right * 12f + Vector3.up * 13f;
            var d2 = d1 + sea * 6f + Vector3.up * 3f;
            var cg06 = CutsceneBuilder.Create("CG06_SetSail", new List<CutsceneBuilder.Shot>
            {
                new CutsceneBuilder.Shot { start = 0f, duration = 3.4f, fromPos = a1, toPos = a2, fromEuler = Look(a1, boatTop), toEuler = Look(a2, boatTop + b.forward * 1.5f), fromFov = 50f, toFov = 46f },
                new CutsceneBuilder.Shot { start = 3.4f, duration = 4.2f, fromPos = c1, toPos = c2, fromEuler = Look(c1, horizon), toEuler = Look(c2, horizon), fromFov = 42f, toFov = 36f },
                new CutsceneBuilder.Shot { start = 7.6f, duration = 3.6f, fromPos = d1, toPos = d2, fromEuler = Look(d1, b.position), toEuler = Look(d2, b.position - sea * 6f), fromFov = 55f, toFov = 52f },
            }, new List<(float, float, string)>
            {
                (0.6f, 2.8f, "父亲的笔记里写着：第二块心，在「海水退去之处」。"),
                (4.0f, 3.2f, "东北方的海上，有一圈白色的浪花。"),
                (8.0f, 2.8f, "……爸，等我。"),
            }, new List<(float, float, float, float)> { (0f, 0.6f, 1f, 0f), (10.6f, 0.6f, 0f, 1f) }, false);
            UnityEventTools.AddPersistentListener(boat.onFirstBoard, cg06.Play);
        }
    }
}
