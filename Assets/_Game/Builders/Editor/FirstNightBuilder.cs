using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Rishi.Story;
using Rishi.World;

namespace Rishi.Builders
{
    /// <summary>
    /// 第一夜事件的场景物件：从海里走上来、通到沉船的湿脚印，林边的彼得（18 世纪水手服），沉船舱里的敲击声音源，
    /// 以及串起整个事件的 FirstNightEvent。脚印和彼得默认隐藏，入夜后由事件打开。
    /// </summary>
    public static class FirstNightBuilder
    {
        const string Tex = "Assets/Art/Generated/Textures/footprint_wet.png";
        const string MatDir = "Assets/Art/Generated/Materials";

        public static FirstNightEvent Build(Terrain terrain, Vector3 spawn, DayNightCycle cycle)
        {
            var root = new GameObject("FirstNight");
            var ev = root.AddComponent<FirstNightEvent>();
            ev.dayNight = cycle;
            var wreck = GameObject.Find("Shipwreck").transform;
            ev.wreck = wreck;

            ev.footprints = BuildFootprints(root.transform, terrain, wreck.position, spawn);
            ev.peter = BuildPeter(root.transform, terrain, spawn);

            var knock = new GameObject("KnockInHull").AddComponent<AudioSource>();
            knock.transform.SetParent(root.transform, true);
            knock.transform.position = wreck.position + Vector3.up * 1.2f;
            knock.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Final/knock_three.wav") ?? AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Placeholder/knock_three.wav");
            knock.playOnAwake = false;
            knock.spatialBlend = 1f;
            knock.rolloffMode = AudioRolloffMode.Logarithmic;
            knock.minDistance = 4f;
            knock.maxDistance = 90f; // 整片沙滩都听得见
            knock.dopplerLevel = 0f;
            ev.knockSource = knock;
            return ev;
        }

        // ---------------- 湿脚印 ----------------

        static float H(Terrain t, Vector3 p) => t.SampleHeight(p) + t.transform.position.y;

        /// <summary>从水边（沉船旁靠出生点一侧 22 米的海岸）斜着走上沙滩，到沉船船身边。</summary>
        static GameObject BuildFootprints(Transform parent, Terrain terrain, Vector3 wreck, Vector3 spawn)
        {
            var go = new GameObject("WetFootprints");
            go.transform.SetParent(parent, true);
            var seaDir = new Vector3(wreck.x, 0, wreck.z).normalized; // 岛大致以原点为中心
            var along = Vector3.ProjectOnPlane(spawn - wreck, Vector3.up).normalized;
            var start = wreck + along * 22f;
            for (float t = 0; t < 40f; t += 0.5f)
            {
                var p = start + seaDir * t;
                if (H(terrain, p) < 0.05f) { start = p; break; }
            }
            // 终点在船身靠陆地的一侧：脚印从浪里斜着走上干沙滩，绕到船边
            var end = wreck - seaDir * 5f + along * 2.5f;
            var mat = Material();
            int i = 0;
            foreach (var (pos, yaw, left) in Footprints.Path(start, end, 0.68f, 0.11f, 1700))
            {
                var p = pos;
                float h = H(terrain, p);
                if (h < 0.12f) continue; // 还在水里的那几步被浪冲掉了
                p.y = h + 0.012f;
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.name = left ? "L" + i : "R" + i;
                q.transform.SetParent(go.transform, true);
                q.transform.SetPositionAndRotation(p, Quaternion.Euler(90f, yaw, 0f));
                q.transform.localScale = new Vector3(left ? -0.14f : 0.14f, 0.31f, 1f); // 左脚镜像；比真脚略大，远处也看得出
                var r = q.GetComponent<Renderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                i++;
            }
            Debug.Log($"[FirstNight] 湿脚印 {i} 个，从 {start} 到 {end}");
            return go;
        }

        /// <summary>湿脚印材质：深色、很光滑（反着手电的光），形状来自程序画的脚印贴图。</summary>
        static Material Material()
        {
            string path = $"{MatDir}/WetFootprint.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = m == null;
            if (created) m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetTexture("_BaseMap", FootprintTexture());
            m.SetColor("_BaseColor", new Color(0.05f, 0.042f, 0.032f, 0.95f)); // 湿沙几乎是黑的，在干沙上很显眼
            m.SetFloat("_Smoothness", 0.9f);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent - 10;
            if (created) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>程序画一只右脚的脚印：前掌、足弓、脚跟、五个脚趾，边缘柔和。</summary>
        static Texture2D FootprintTexture()
        {
            if (!File.Exists(Tex))
            {
                const int w = 64, h = 128;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                float Blob(float x, float y, float cx, float cy, float rx, float ry)
                {
                    float dx = (x - cx) / rx, dy = (y - cy) / ry;
                    return Mathf.Clamp01(1.4f - Mathf.Sqrt(dx * dx + dy * dy) * 1.4f);
                }
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                        float a = Blob(u, v, 0.5f, 0.15f, 0.26f, 0.13f);                 // 脚跟
                        a = Mathf.Max(a, Blob(u, v, 0.58f, 0.42f, 0.17f, 0.2f) * 0.7f);   // 足弓外侧
                        a = Mathf.Max(a, Blob(u, v, 0.52f, 0.66f, 0.33f, 0.14f));         // 前掌
                        float[] tx = { 0.36f, 0.5f, 0.62f, 0.72f, 0.8f }, ty = { 0.86f, 0.88f, 0.85f, 0.81f, 0.76f }, tr = { 0.09f, 0.07f, 0.065f, 0.06f, 0.055f };
                        for (int k = 0; k < 5; k++) a = Mathf.Max(a, Blob(u, v, tx[k], ty[k], tr[k], tr[k] * 0.9f));
                        tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.SmoothStep(0f, 1f, a)));
                    }
                File.WriteAllBytes(Tex, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(Tex);
                if (AssetImporter.GetAtPath(Tex) is TextureImporter ti) { ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.SaveAndReimport(); }
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(Tex);
        }

        // ---------------- 彼得 ----------------

        /// <summary>林边的人影：长到膝盖的深色水手外套、三角帽、苍白的脸，垂着手站着。位置在出生点和营地之间的丛林边缘。</summary>
        static Peter BuildPeter(Transform parent, Terrain terrain, Vector3 spawn)
        {
            var camp = GameObject.Find("FatherCamp").transform.position;
            var dir = Vector3.ProjectOnPlane(camp - spawn, Vector3.up).normalized;
            var side = Vector3.Cross(Vector3.up, dir);
            var pos = spawn + dir * 22f + side * 9f;
            for (float t = 14f; t < 45f; t += 1f) // 走到林子边上（地势开始抬高的地方）
            {
                var p = spawn + dir * t + side * 9f;
                if (H(terrain, p) > 4.2f) { pos = p; break; }
            }
            pos.y = H(terrain, pos);

            var go = new GameObject("Peter");
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(-dir));
            var coat = Paint("PeterCoat", new Color(0.05f, 0.06f, 0.09f), 0.15f);
            var shirt = Paint("PeterShirt", new Color(0.32f, 0.3f, 0.26f), 0.1f);
            var skin = Paint("PeterSkin", new Color(0.42f, 0.4f, 0.37f), 0.25f); // 泡过海水、发灰的皮肤
            var leather = Paint("PeterBoots", new Color(0.04f, 0.03f, 0.025f), 0.35f);
            Part(go.transform, PrimitiveType.Capsule, new Vector3(0, 1.08f, 0), new Vector3(0.46f, 0.6f, 0.34f), coat);           // 外套
            Part(go.transform, PrimitiveType.Cube, new Vector3(0, 1.28f, 0.11f), new Vector3(0.16f, 0.32f, 0.06f), shirt);       // 衬衣前襟
            Part(go.transform, PrimitiveType.Capsule, new Vector3(-0.27f, 1.12f, 0.02f), new Vector3(0.11f, 0.33f, 0.11f), coat); // 袖子
            Part(go.transform, PrimitiveType.Capsule, new Vector3(0.27f, 1.12f, 0.02f), new Vector3(0.11f, 0.33f, 0.11f), coat);
            Part(go.transform, PrimitiveType.Sphere, new Vector3(-0.28f, 0.76f, 0.03f), Vector3.one * 0.075f, skin);             // 手
            Part(go.transform, PrimitiveType.Sphere, new Vector3(0.28f, 0.76f, 0.03f), Vector3.one * 0.075f, skin);
            Part(go.transform, PrimitiveType.Capsule, new Vector3(-0.1f, 0.3f, 0), new Vector3(0.12f, 0.3f, 0.12f), leather);    // 靴子
            Part(go.transform, PrimitiveType.Capsule, new Vector3(0.1f, 0.3f, 0), new Vector3(0.12f, 0.3f, 0.12f), leather);
            Part(go.transform, PrimitiveType.Sphere, new Vector3(0, 1.7f, 0.01f), new Vector3(0.2f, 0.24f, 0.21f), skin);         // 头
            Part(go.transform, PrimitiveType.Cylinder, new Vector3(0, 1.84f, 0), new Vector3(0.36f, 0.02f, 0.36f), coat);         // 三角帽帽檐
            Part(go.transform, PrimitiveType.Cylinder, new Vector3(0, 1.9f, 0), new Vector3(0.2f, 0.06f, 0.2f), coat);            // 帽顶
            for (int k = 0; k < 3; k++)                                                                                          // 三个翻起的角
            {
                var c = Part(go.transform, PrimitiveType.Cube, Vector3.zero, new Vector3(0.2f, 0.07f, 0.02f), coat);
                float a = k * 120f;
                c.transform.localPosition = new Vector3(Mathf.Sin(a * Mathf.Deg2Rad) * 0.15f, 1.88f, Mathf.Cos(a * Mathf.Deg2Rad) * 0.15f);
                c.transform.localRotation = Quaternion.Euler(-20f, a, 0f);
            }
            var peter = go.AddComponent<Peter>();
            Debug.Log($"[FirstNight] 彼得站在 {pos}");
            return peter;
        }

        static GameObject Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
        {
            var g = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = mat;
            return g;
        }

        static Material Paint(string name, Color c, float smooth) => FPSSceneBuilder.Painted(name, c, 0f, smooth);
    }
}
