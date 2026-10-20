using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Rishi.Core;
using Rishi.Inventory;
using Rishi.Weapons;

namespace Rishi.Builders
{
    /// <summary>
    /// 物品与武器：生成配置资产（已有的不覆盖，方便在 Inspector 里调数值）、程序化的砍刀 / 鱼枪枪模、鱼枪矛预制体，
    /// 以及场景里的可拾取物（砍刀在沉船旁，鱼枪在父亲营地，椰子在沙滩上）。
    /// </summary>
    public static class GameplaySetup
    {
        const string ItemDir = "Assets/_Game/Inventory/Data/Items";
        const string WeaponDir = "Assets/_Game/Weapons/Data";
        const int WeaponLayer = 8;

        static readonly Dictionary<string, ItemDef> items = new Dictionary<string, ItemDef>();
        static readonly Dictionary<string, WeaponDef> weapons = new Dictionary<string, WeaponDef>();

        public static ItemDef Item(string id) { EnsureAssets(); return items[id]; }
        public static WeaponDef Weapon(string id) { EnsureAssets(); return weapons[id]; }

        public static void EnsureAssets()
        {
            if (items.Count > 0 && AllAlive()) return;
            items.Clear();
            weapons.Clear();
            Directory.CreateDirectory(ItemDir);
            Directory.CreateDirectory(WeaponDir);

            DefItem("pistol", "手枪", ItemCategory.Weapon, 1, "枪", new Color(0.3f, 0.32f, 0.36f),
                "一把老式 9 毫米手枪，出海前从家里带上的。弹匣 12 发。\n在水下打不响。");
            DefItem("machete", "砍刀", ItemCategory.Weapon, 1, "刀", new Color(0.55f, 0.45f, 0.3f),
                "从沉船旁的沙里拔出来的砍刀，刀刃上有锈，但还很锋利。能劈开挡路的藤蔓，也能对付野兽。\n水下也能挥。");
            DefItem("speargun", "鱼枪", ItemCategory.Weapon, 1, "叉", new Color(0.22f, 0.42f, 0.52f),
                "父亲营地里的橡皮筋鱼枪。在水下又准又狠，岸上也能用，只是矛会往下掉。\n射出去的矛记得游过去拔回来。");
            DefItem("ammo_9mm", "9mm 子弹", ItemCategory.Ammo, 60, "弹", new Color(0.62f, 0.5f, 0.22f),
                "手枪用的子弹。省着点用。");
            DefItem("spear", "鱼枪矛", ItemCategory.Ammo, 12, "矛", new Color(0.45f, 0.55f, 0.6f),
                "不锈钢的鱼枪矛，尖端带倒钩。射中的矛可以拔回来再用。");
            DefItem("shell", "贝壳", ItemCategory.Treasure, 30, "贝", new Color(0.92f, 0.72f, 0.6f),
                "海底捡到的贝壳，花纹很漂亮。");
            DefItem("pearl", "珍珠", ItemCategory.Treasure, 20, "珠", new Color(0.82f, 0.82f, 0.9f),
                "从巨型砗磲里取出的珍珠，圆润，泛着淡淡的粉色光泽。");
            DefItem("gold_coin", "阿玛拉金币", ItemCategory.Treasure, 50, "币", new Color(0.85f, 0.65f, 0.2f),
                "沉船木箱里的古金币，一面刻着太阳，另一面刻着一只闭着的眼睛。");
            var coconut = DefItem("coconut", "椰子", ItemCategory.Consumable, 6, "椰", new Color(0.42f, 0.3f, 0.16f),
                "从椰子树下捡的青椰子，用砍刀劈开就能喝。");
            DefItem("flashlight", "手电筒", ItemCategory.Material, 1, "灯", new Color(0.55f, 0.42f, 0.22f),
                "父亲的老式黄铜手电筒，开关有点涩。按 F 开关，亮着会耗电。");
            var battery = DefItem("battery", "电池", ItemCategory.Consumable, 8, "池", new Color(0.3f, 0.36f, 0.3f),
                "一节 1 号电池。手电筒没电时会自动换上，也可以在这里直接装进去。");
            if (!battery.usable)
            {
                battery.usable = true;
                battery.useVerb = "装进手电筒";
                battery.useMessage = "手电筒换上了新电池。";
                EditorUtility.SetDirty(battery);
            }
            if (!coconut.usable)
            {
                coconut.usable = true;
                coconut.useVerb = "喝";
                coconut.useMessage = "椰汁清甜，你感觉好多了。";
                EditorUtility.SetDirty(coconut);
            }

            DefWeapon("pistol", "手枪", WeaponKind.Hitscan, w =>
            {
                w.ammo = items["ammo_9mm"]; w.damage = 34f; w.fireRate = 7f; w.magazineSize = 12; w.reloadTime = 1.4f; w.range = 150f;
                w.spread = 0.6f; w.recoilUp = 1.6f; w.recoilSide = 0.5f; w.usableUnderwater = false; w.usableOnLand = true;
                w.fireSound = "gun_shot"; w.reloadSound = "gun_reload"; w.emptySound = "gun_empty"; w.equipSound = "weapon_equip";
            });
            DefWeapon("machete", "砍刀", WeaponKind.Melee, w =>
            {
                w.ammo = null; w.damage = 45f; w.fireRate = 1.8f; w.magazineSize = 0; w.range = 2.3f; w.meleeRadius = 0.35f; w.meleeHitDelay = 0.14f;
                w.impactForce = 3f; w.spread = 0f; w.usableUnderwater = true; w.usableOnLand = true;
                w.fireSound = "machete_swing"; w.reloadSound = ""; w.emptySound = ""; w.equipSound = "machete_draw";
            });
            DefWeapon("speargun", "鱼枪", WeaponKind.Projectile, w =>
            {
                w.ammo = items["spear"]; w.damage = 70f; w.fireRate = 1.5f; w.magazineSize = 1; w.reloadTime = 1.3f; w.range = 60f;
                w.spread = 0.15f; w.recoilUp = 2.5f; w.recoilSide = 0.3f; w.usableUnderwater = true; w.usableOnLand = true;
                w.projectileSpeed = 34f; w.projectileSpeedUnderwater = 24f;
                w.fireSound = "speargun_fire"; w.reloadSound = "speargun_reload"; w.emptySound = "gun_empty"; w.equipSound = "weapon_equip";
            });
            AssetDatabase.SaveAssets();
        }

        static bool AllAlive()
        {
            foreach (var i in items.Values) if (i == null) return false;
            foreach (var w in weapons.Values) if (w == null) return false;
            return true;
        }

        static ItemDef DefItem(string id, string name, ItemCategory cat, int stack, string glyph, Color color, string desc)
        {
            string path = $"{ItemDir}/{id}.asset";
            var item = AssetDatabase.LoadAssetAtPath<ItemDef>(path);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ItemDef>();
                item.id = id;
                item.displayName = name;
                item.category = cat;
                item.maxStack = stack;
                item.iconGlyph = glyph;
                item.iconColor = color;
                item.description = desc;
                AssetDatabase.CreateAsset(item, path);
            }
            items[id] = item;
            return item;
        }

        static void DefWeapon(string id, string name, WeaponKind kind, System.Action<WeaponDef> init)
        {
            string path = $"{WeaponDir}/{id}.asset";
            var w = AssetDatabase.LoadAssetAtPath<WeaponDef>(path);
            if (w == null)
            {
                w = ScriptableObject.CreateInstance<WeaponDef>();
                w.id = id;
                w.displayName = name;
                w.kind = kind;
                w.item = items[id];
                init(w);
                AssetDatabase.CreateAsset(w, path);
            }
            weapons[id] = w;
        }

        // ================= 模型 =================

        static Material Steel => FPSSceneBuilder.Painted("MacheteSteel", new Color(0.58f, 0.57f, 0.55f), 0.85f, 0.55f);
        static Material DarkWood => FPSSceneBuilder.Painted("GripWood", new Color(0.16f, 0.1f, 0.07f), 0f, 0.35f);
        static Material Rubber => FPSSceneBuilder.Painted("Rubber", new Color(0.05f, 0.05f, 0.05f), 0f, 0.25f);
        static Material Teak => FPSSceneBuilder.Painted("SpeargunTeak", new Color(0.36f, 0.22f, 0.12f), 0f, 0.5f);
        static Material Alu => FPSSceneBuilder.Painted("SpeargunAlu", new Color(0.32f, 0.34f, 0.36f), 0.8f, 0.6f);

        static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material mat, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            return go;
        }

        static Mesh SaveMesh(Mesh m)
        {
            Directory.CreateDirectory(IslandBuilder.Gen + "/Weapons");
            string path = $"{IslandBuilder.Gen}/Weapons/{m.name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) { EditorUtility.CopySerialized(m, existing); return existing; }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>沿 +Z 的细圆管（用 MarineMeshes 的管子工具）。</summary>
        static Mesh Rod(string name, float length, float radius, int sides = 8, float tipLength = 0f)
        {
            var mb = new MarineMeshes.MB();
            var path = new List<Vector3> { Vector3.zero, Vector3.forward * length * 0.5f, Vector3.forward * length };
            mb.Tube(path, f => radius, f => Color.white, sides, null, tipLength <= 0f);
            if (tipLength > 0f)
            {
                var tip = new List<Vector3> { Vector3.forward * length, Vector3.forward * (length + tipLength * 0.5f), Vector3.forward * (length + tipLength) };
                mb.Tube(tip, f => Mathf.Lerp(radius * 2.2f, radius * 0.3f, f), f => Color.white, sides, null, true);
            }
            return SaveMesh(mb.Build(name));
        }

        /// <summary>砍刀刀身：刀刃在 YZ 平面里，刀背直、刀腹向前加宽，刀尖上翘。</summary>
        static Mesh BladeMesh()
        {
            var mb = new MarineMeshes.MB();
            const int seg = 12;
            const float len = 0.44f, thick = 0.0035f;
            for (int side = -1; side <= 1; side += 2)
            {
                int start = mb.v.Count;
                for (int i = 0; i <= seg; i++)
                {
                    float f = i / (float)seg;
                    float z = f * len;
                    float spine = 0.012f + 0.006f * f;                                  // 刀背微微上弯
                    float belly = -(0.032f + 0.022f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, f * 1.15f))) * (1f - Mathf.SmoothStep(0.85f, 1f, f)); // 刀腹
                    var n = new Vector3(side, 0, 0);
                    mb.Add(new Vector3(side * thick, spine, z), n, Color.white, new Vector2(f, 1));
                    mb.Add(new Vector3(side * thick * 0.2f, belly, z), n, Color.white, new Vector2(f, 0));
                }
                for (int i = 0; i < seg; i++)
                {
                    int a = start + i * 2;
                    if (side > 0) mb.Quad(a, a + 1, a + 3, a + 2); else mb.Quad(a, a + 2, a + 3, a + 1);
                }
            }
            return SaveMesh(mb.Build("MacheteBlade", recalcNormals: true));
        }

        /// <summary>砍刀模型（刀尖朝 +Z，刀刃朝 -Y）。</summary>
        public static GameObject Machete(Transform parent, string name = "Machete")
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var store = AssetDatabase.LoadAssetAtPath<GameObject>(StoreAssets.Machete + "/Machete01_prefab.prefab");
            if (store != null)
            {
                // Machete01（Nikolay Fedorov）：长轴沿 X，刀尖在 +X；转到刀尖朝 +Z、刀刃朝下，缩放到全长 0.6 米
                var m = (GameObject)PrefabUtility.InstantiatePrefab(store, root.transform);
                PrefabUtility.UnpackPrefabInstance(m, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                m.transform.localRotation = Quaternion.Euler(0, -90f, 0);
                m.transform.localPosition = Vector3.zero;
                var b = FPSSceneBuilder.Bounds(m);
                float len = Mathf.Max(b.size.x, b.size.z);
                if (len > 1e-4f) m.transform.localScale = store.transform.localScale * (0.6f / len);
                b = FPSSceneBuilder.Bounds(m);
                // 握把末端放在原点附近，和原来的程序化模型一致
                m.transform.position -= (b.center - root.transform.position) - root.transform.forward * 0.15f;
                foreach (var c in m.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                return root;
            }
            MeshObject("Blade", root.transform, BladeMesh(), Steel, new Vector3(0, 0, 0.02f), Quaternion.identity);
            MeshObject("Grip", root.transform, Rod("MacheteGrip", 0.13f, 0.014f, 8), DarkWood, new Vector3(0, -0.008f, -0.12f), Quaternion.identity);
            FPSSceneBuilder.Part("Guard", root.transform, new Vector3(0, -0.008f, 0.012f), new Vector3(0.02f, 0.05f, 0.012f), Rubber, false);
            return root;
        }

        /// <summary>鱼枪模型（枪口朝 +Z）。LoadedSpear 子物体是装着的矛，射出后隐藏。</summary>
        public static GameObject Speargun(Transform parent, out Transform muzzle, string name = "Speargun")
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            MeshObject("Barrel", root.transform, Rod("SpeargunBarrel", 0.62f, 0.014f, 10), Teak, new Vector3(0, 0, -0.12f), Quaternion.identity);
            FPSSceneBuilder.Part("Rail", root.transform, new Vector3(0, 0.016f, 0.19f), new Vector3(0.012f, 0.008f, 0.6f), Alu, false);
            FPSSceneBuilder.Part("Grip", root.transform, new Vector3(0, -0.06f, -0.05f), new Vector3(0.026f, 0.1f, 0.04f), Rubber, false);
            FPSSceneBuilder.Part("Trigger", root.transform, new Vector3(0, -0.025f, -0.01f), new Vector3(0.006f, 0.025f, 0.01f), Alu, false);
            FPSSceneBuilder.Part("Butt", root.transform, new Vector3(0, -0.005f, -0.15f), new Vector3(0.03f, 0.04f, 0.06f), Rubber, false);
            FPSSceneBuilder.Part("MuzzleBlock", root.transform, new Vector3(0, 0.006f, 0.5f), new Vector3(0.04f, 0.035f, 0.03f), Rubber, false);
            // 两根拉满的橡皮筋
            for (int s = -1; s <= 1; s += 2)
                MeshObject("Band" + (s < 0 ? "L" : "R"), root.transform, Rod("SpeargunBand", 0.34f, 0.006f, 6), Rubber,
                    new Vector3(s * 0.018f, 0.012f, 0.5f), Quaternion.LookRotation(new Vector3(-s * 0.018f, 0.012f, -0.34f).normalized));
            var spear = MeshObject("LoadedSpear", root.transform, Rod("SpearRod", 0.82f, 0.0035f, 6, 0.05f), Steel, new Vector3(0, 0.026f, -0.18f), Quaternion.identity);
            muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(root.transform, false);
            muzzle.localPosition = new Vector3(0, 0.026f, 0.7f);
            _ = spear;
            return root;
        }

        /// <summary>鱼枪矛预制体（投射物，命中后变成可拔出的物品）。</summary>
        public static GameObject SpearPrefab()
        {
            string dir = IslandBuilder.Gen + "/Prefabs";
            Directory.CreateDirectory(dir);
            var root = new GameObject("Spear");
            // 网格原点在矛尾，矛尖朝 +Z；整体往后挪，让物体原点在矛的中部
            var model = MeshObject("Rod", root.transform, Rod("SpearRod", 0.82f, 0.0035f, 6, 0.05f), Steel, new Vector3(0, 0, -0.45f), Quaternion.identity);
            model.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;
            var col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(0.1f, 0.1f, 0.95f);
            col.isTrigger = true;
            col.enabled = false;
            root.AddComponent<SpearProjectile>();
            var pickup = root.AddComponent<ItemPickup>();
            pickup.item = Item("spear");
            pickup.verb = "拔出";
            pickup.enabled = false;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{dir}/Spear.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ================= 玩家 =================

        /// <summary>在 WeaponHolder 下建三把武器的枪模，返回武器槽位。</summary>
        public static List<WeaponController.Slot> BuildViewModels(Transform holder, GameObject pistol, Transform pistolMuzzle)
        {
            EnsureAssets();
            var slots = new List<WeaponController.Slot>
            {
                new WeaponController.Slot { def = Weapon("pistol"), viewModel = pistol, muzzle = pistolMuzzle },
            };

            // 手枪：右手握把，左手从下面托住
            var pb = LocalBounds(pistol, holder);
            var pgrip = new Vector3(pb.center.x, pb.min.y + pb.size.y * 0.3f, pb.min.z + pb.size.z * 0.22f);
            ArmBuilder.Arm(pistol.transform, "RightArm", pistol.transform.InverseTransformPoint(holder.TransformPoint(pgrip + new Vector3(0.012f, -0.03f, -0.035f))),
                           pistol.transform.InverseTransformDirection(holder.TransformDirection(new Vector3(0.35f, -0.55f, -0.76f))),
                           pistol.transform.InverseTransformDirection(holder.TransformDirection(new Vector3(0.8f, 0.55f, 0f))), 1f, false, WeaponLayer);
            slots[0].leftArm = ArmBuilder.Arm(pistol.transform, "LeftArm", pistol.transform.InverseTransformPoint(holder.TransformPoint(pgrip + new Vector3(-0.038f, -0.045f, -0.015f))),
                           pistol.transform.InverseTransformDirection(holder.TransformDirection(new Vector3(-0.5f, -0.55f, -0.67f))),
                           pistol.transform.InverseTransformDirection(holder.TransformDirection(new Vector3(-0.85f, -0.2f, 0.1f))), 0.85f, true, WeaponLayer);

            // 砍刀：握在右手，刀尖朝前上方
            var machetePivot = new GameObject("MacheteVM").transform;
            machetePivot.SetParent(holder, false);
            machetePivot.localPosition = new Vector3(0.05f, -0.05f, 0.02f);
            machetePivot.localRotation = Quaternion.Euler(-55f, 50f, 0f);
            var machete = Machete(machetePivot);
            var mb = LocalBounds(machete, holder);
            // 刀柄是离摄像机最近的一端
            var mgrip = new Vector3(machetePivot.localPosition.x, machetePivot.localPosition.y, mb.min.z + 0.05f);
            ArmBuilder.Arm(machetePivot, "RightArm", machetePivot.InverseTransformPoint(holder.TransformPoint(mgrip + new Vector3(0.015f, -0.035f, -0.03f))),
                           machetePivot.InverseTransformDirection(holder.TransformDirection(new Vector3(0.3f, -0.6f, -0.74f))),
                           machetePivot.InverseTransformDirection(holder.TransformDirection(new Vector3(0.85f, 0.5f, 0f))), 1f, false, WeaponLayer);
            slots.Add(new WeaponController.Slot { def = Weapon("machete"), viewModel = machetePivot.gameObject });

            // 鱼枪：腰射姿态，枪身大部分伸出画面
            var gunPivot = new GameObject("SpeargunVM").transform;
            gunPivot.SetParent(holder, false);
            gunPivot.localPosition = new Vector3(-0.03f, -0.01f, -0.06f);
            gunPivot.localRotation = Quaternion.Euler(0f, 0f, 0f);
            var speargun = Speargun(gunPivot, out var spearMuzzle);
            var sb = LocalBounds(speargun, holder);
            var sgrip = new Vector3(sb.center.x, sb.min.y + 0.02f, Mathf.Max(sb.min.z, -0.05f) + 0.08f);
            ArmBuilder.Arm(gunPivot, "RightArm", gunPivot.InverseTransformPoint(holder.TransformPoint(sgrip + new Vector3(0.012f, -0.02f, -0.03f))),
                           gunPivot.InverseTransformDirection(holder.TransformDirection(new Vector3(0.35f, -0.55f, -0.76f))),
                           gunPivot.InverseTransformDirection(holder.TransformDirection(new Vector3(0.8f, 0.55f, 0f))), 1f, false, WeaponLayer);
            var sleft = ArmBuilder.Arm(gunPivot, "LeftArm", gunPivot.InverseTransformPoint(holder.TransformPoint(new Vector3(sb.center.x - 0.03f, sb.min.y + 0.005f, sgrip.z + 0.2f))),
                           gunPivot.InverseTransformDirection(holder.TransformDirection(new Vector3(-0.45f, -0.5f, -0.74f))),
                           gunPivot.InverseTransformDirection(holder.TransformDirection(new Vector3(-0.7f, -0.5f, 0f))), 0.8f, true, WeaponLayer);
            slots.Add(new WeaponController.Slot { def = Weapon("speargun"), viewModel = gunPivot.gameObject, muzzle = spearMuzzle, leftArm = sleft });

            foreach (var s in slots)
                foreach (var r in s.viewModel.GetComponentsInChildren<Renderer>())
                {
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    r.gameObject.layer = WeaponLayer;
                }
            return slots;
        }

        /// <summary>物体所有渲染器的包围盒，换算到 space 的局部坐标。</summary>
        static Bounds LocalBounds(GameObject go, Transform space)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            var b = new Bounds(space.InverseTransformPoint(rs[0].bounds.center), Vector3.zero);
            foreach (var r in rs)
            {
                var wb = r.bounds;
                for (int i = 0; i < 8; i++)
                    b.Encapsulate(space.InverseTransformPoint(wb.center + Vector3.Scale(wb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            }
            return b;
        }

        // ================= 场景里的拾取物 =================

        static void MakePickup(GameObject go, ItemDef item, int count, string verb, Vector3 colliderSize)
        {
            var box = go.AddComponent<BoxCollider>();
            box.size = colliderSize;
            var p = go.AddComponent<ItemPickup>();
            p.item = item;
            p.count = count;
            p.verb = verb;
            IslandStoryBuilder.NoteGlow(go.transform);
        }

        // ================= 手电筒 =================

        /// <summary>手电筒模型：最长的轴是筒身，截面大的一端是灯头，转成灯头朝 +Z、长约 length 米、中心在原点。</summary>
        public static GameObject FlashlightModel(Transform parent, string name, float length)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(FPSSceneBuilder.LoadModel("Models/vintage_flashlight"));
            model.transform.SetParent(root.transform, false);
            FPSSceneBuilder.AssignMaterial(model, FPSSceneBuilder.Pbr("Flashlight", "Models/vintage_flashlight", 1f));

            var pts = new List<Vector3>();
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
                foreach (var v in mf.sharedMesh.vertices)
                    pts.Add(root.transform.InverseTransformPoint(mf.transform.TransformPoint(v)));
            Vector3 min = pts[0], max = pts[0];
            foreach (var q in pts) { min = Vector3.Min(min, q); max = Vector3.Max(max, q); }
            var size = max - min;
            int axis = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
            float lo = min[axis], hi = max[axis], span = hi - lo;
            var center = (min + max) / 2f;
            // 两端各取 15% 的顶点，比较到轴心的最大半径
            float Radius(bool high)
            {
                float r = 0f;
                foreach (var q in pts)
                {
                    float a = q[axis];
                    if (high ? a < hi - span * 0.15f : a > lo + span * 0.15f) continue;
                    var d = q - center; d[axis] = 0f;
                    r = Mathf.Max(r, d.magnitude);
                }
                return r;
            }
            var dir = Vector3.zero;
            dir[axis] = Radius(true) >= Radius(false) ? 1f : -1f; // 灯头方向（模型坐标）
            var turn = Quaternion.FromToRotation(dir, Vector3.forward);
            model.transform.localRotation = turn * model.transform.localRotation; // 保留 FBX 根节点自带的轴向修正
            float scale = length / Mathf.Max(span, 1e-4f);
            model.transform.localScale *= scale;
            model.transform.localPosition = -(turn * center) * scale;
            foreach (var r in model.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            return root;
        }

        /// <summary>给玩家装上手电筒：摄像机下的聚光灯 + 左下角手里的模型（武器层） + Flashlight 组件。</summary>
        public static Flashlight SetupFlashlight(GameObject player, Camera cam, int weaponLayer)
        {
            EnsureAssets();
            var beam = new GameObject("FlashlightBeam").AddComponent<Light>();
            beam.transform.SetParent(cam.transform, false);
            beam.transform.localPosition = new Vector3(-0.18f, -0.16f, 0.3f);
            beam.transform.localRotation = Quaternion.Euler(1.5f, 2f, 0f); // 略微往准星方向收
            beam.type = LightType.Spot;
            beam.spotAngle = 56f;
            beam.innerSpotAngle = 24f;
            beam.range = 32f;
            beam.intensity = 45f;
            beam.color = new Color(1f, 0.92f, 0.78f);
            beam.shadows = LightShadows.Soft;
            beam.shadowNearPlane = 0.25f;
            beam.renderMode = LightRenderMode.ForcePixel;
            beam.cullingMask = ~(1 << weaponLayer); // 不照手里的模型（光源就在它旁边，会照成一片白）
            beam.enabled = false;

            var view = FlashlightModel(cam.transform, "FlashlightView", 0.13f);
            view.transform.localPosition = new Vector3(-0.2f, -0.19f, 0.42f);
            view.transform.localRotation = Quaternion.Euler(3f, 4f, -6f);
            // 左手握着手电（随手电一起显示）
            ArmBuilder.Arm(view.transform, "LeftArm", new Vector3(0.012f, -0.055f, -0.03f), new Vector3(-0.3f, -0.5f, -0.81f), new Vector3(-0.8f, 0.55f, 0f), 1f, true, weaponLayer);
            FPSSceneBuilder.SetLayerRecursive(view, weaponLayer);
            view.SetActive(false);

            var fl = player.AddComponent<Flashlight>();
            fl.inventory = player.GetComponent<PlayerInventory>();
            fl.flashlightItem = Item("flashlight");
            fl.batteryItem = Item("battery");
            fl.beam = beam;
            fl.viewModel = view;
            return fl;
        }

        static GameObject BatteryProp(Transform parent, Vector3 pos, float yaw, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.name = "Cell";
            body.transform.SetParent(go.transform, false);
            body.transform.localPosition = new Vector3(0, 0.017f, 0);
            body.transform.localRotation = Quaternion.Euler(0, 0, 90f);
            body.transform.localScale = new Vector3(0.033f, 0.03f, 0.033f);
            body.GetComponent<Renderer>().sharedMaterial = FPSSceneBuilder.Painted("BatteryCell", new Color(0.12f, 0.3f, 0.18f), 0.2f, 0.45f);
            return go;
        }

        public static void PlacePickups(Terrain terrain, Vector3 spawn)
        {
            EnsureAssets();
            var root = new GameObject("Pickups").transform;
            var wreck = GameObject.Find("Shipwreck");
            var camp = GameObject.Find("FatherCamp");

            if (wreck != null)
            {
                // 砍刀插在沉船边的沙里
                var toSpawn = (spawn - wreck.transform.position); toSpawn.y = 0; toSpawn.Normalize();
                var side = Vector3.Cross(Vector3.up, toSpawn);
                var p = IslandStoryBuilder.Ground(terrain, wreck.transform.position + toSpawn * 7f + side * 2.5f);
                var machete = Machete(root, "MachetePickup");
                machete.transform.SetPositionAndRotation(p + Vector3.up * 0.28f, Quaternion.Euler(70f, Random.Range(0f, 360f), 0f) * Quaternion.Euler(0, 0, 90f));
                MakePickup(machete, Item("machete"), 1, "拔出", new Vector3(0.12f, 0.12f, 0.6f));

                // 一盒子弹
                var ammoBox = new GameObject("AmmoBox");
                ammoBox.transform.SetParent(root);
                var ap = IslandStoryBuilder.Ground(terrain, wreck.transform.position + toSpawn * 6f - side * 2f);
                ammoBox.transform.SetPositionAndRotation(ap, Quaternion.Euler(0, Random.Range(0f, 360f), 0));
                FPSSceneBuilder.Part("Box", ammoBox.transform, new Vector3(0, 0.05f, 0), new Vector3(0.2f, 0.1f, 0.13f), FPSSceneBuilder.Painted("AmmoBox", new Color(0.3f, 0.33f, 0.22f), 0.3f, 0.3f), false);
                MakePickup(ammoBox, Item("ammo_9mm"), 12, "拾取", new Vector3(0.3f, 0.2f, 0.25f));

                // 一节电池滚在弹药盒旁边
                var bp1 = IslandStoryBuilder.Ground(terrain, wreck.transform.position + toSpawn * 6.2f - side * 1.3f);
                MakePickup(BatteryProp(root, bp1, Random.Range(0f, 360f), "BatteryWreck"), Item("battery"), 1, "捡起", new Vector3(0.12f, 0.08f, 0.12f));
            }

            if (camp != null)
            {
                // 鱼枪和一捆矛靠在营地的木箱边
                var gp = IslandStoryBuilder.Ground(terrain, camp.transform.TransformPoint(new Vector3(1.6f, 0, 0.7f)));
                var gun = Speargun(root, out _, "SpeargunPickup");
                gun.transform.SetPositionAndRotation(gp + Vector3.up * 0.05f, Quaternion.Euler(0, camp.transform.eulerAngles.y + 70f, 90f));
                MakePickup(gun, Item("speargun"), 1, "拾取", new Vector3(0.12f, 0.15f, 0.9f));

                var bundle = new GameObject("SpearBundle");
                bundle.transform.SetParent(root);
                var bp = IslandStoryBuilder.Ground(terrain, camp.transform.TransformPoint(new Vector3(1.9f, 0, 0.2f)));
                bundle.transform.SetPositionAndRotation(bp + Vector3.up * 0.03f, Quaternion.Euler(0, camp.transform.eulerAngles.y + 60f, 0));
                for (int i = 0; i < 4; i++)
                    MeshObject("Rod" + i, bundle.transform, Rod("SpearRod", 0.82f, 0.0035f, 6, 0.05f), Steel, new Vector3((i - 1.5f) * 0.012f, 0.004f + (i % 2) * 0.006f, -0.43f), Quaternion.identity);
                MakePickup(bundle, Item("spear"), 5, "拾取", new Vector3(0.15f, 0.1f, 0.95f));

                // 父亲的手电筒和两节电池，放在营地的地上
                var fp = IslandStoryBuilder.Ground(terrain, camp.transform.TransformPoint(new Vector3(-1.4f, 0, 1.1f)));
                var torch = FlashlightModel(root, "FlashlightPickup", 0.24f);
                torch.transform.SetPositionAndRotation(fp + Vector3.up * 0.045f, Quaternion.Euler(0, camp.transform.eulerAngles.y + 35f, 0));
                MakePickup(torch, Item("flashlight"), 1, "拾取", new Vector3(0.12f, 0.12f, 0.3f));
                var cp = IslandStoryBuilder.Ground(terrain, camp.transform.TransformPoint(new Vector3(-1.1f, 0, 1.3f)));
                MakePickup(BatteryProp(root, cp, camp.transform.eulerAngles.y + 80f, "BatteryCamp"), Item("battery"), 2, "捡起", new Vector3(0.14f, 0.08f, 0.14f));
            }

            // 神殿台阶前也有两节（父亲先到过这里）
            var ruin = GameObject.Find("SunTemple");
            if (ruin != null)
            {
                var rp = IslandStoryBuilder.Ground(terrain, ruin.transform.TransformPoint(new Vector3(2.2f, 0, 12f)));
                MakePickup(BatteryProp(root, rp, Random.Range(0f, 360f), "BatteryTemple"), Item("battery"), 2, "捡起", new Vector3(0.14f, 0.08f, 0.14f));
            }

            // 椰子：出生点附近的干沙滩上
            var coconutMat = FPSSceneBuilder.Painted("Coconut", new Color(0.36f, 0.42f, 0.16f), 0f, 0.35f);
            int placed = 0;
            for (int a = 0; a < 400 && placed < 7; a++)
            {
                var off = Random.insideUnitCircle * 30f;
                var p = new Vector3(spawn.x + off.x, 0, spawn.z + off.y);
                p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
                if (p.y < 0.8f || p.y > 3.2f || IslandStoryBuilder.InClearZone(p.x, p.z, -3f)) continue;
                var c = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                c.name = "Coconut";
                c.transform.SetParent(root);
                c.transform.position = p + Vector3.up * 0.1f;
                c.transform.localScale = new Vector3(0.22f, 0.2f, 0.24f);
                c.GetComponent<Renderer>().sharedMaterial = coconutMat;
                var pk = c.AddComponent<ItemPickup>();
                pk.item = Item("coconut");
                pk.verb = "捡起";
                placed++;
            }
        }
    }
}
