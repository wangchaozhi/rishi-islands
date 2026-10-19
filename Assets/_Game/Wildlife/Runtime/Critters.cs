using UnityEngine;
using Rishi.Audio;
using Rishi.Core;
using Rishi.World;

namespace Rishi.Wildlife
{
    /// <summary>小动物的程序化模型（运行时用基础体拼装，材质与贴图缓存复用）。</summary>
    public static class Critters
    {
        static Material crabShell, crabLeg, fishMat, batMat, bodyDark;
        static Material[] wingMats;

        static GameObject Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat, Vector3 euler = default)
        {
            var go = GameObject.CreatePrimitive(type);
            var col = go.GetComponent<Collider>();
            if (Application.isPlaying) Object.Destroy(col); else Object.DestroyImmediate(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.transform.localRotation = Quaternion.Euler(euler);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        public static GameObject Crab()
        {
            crabShell ??= WildlifeDirector.LitMat(new Color(0.78f, 0.32f, 0.12f), 0.55f);
            crabLeg ??= WildlifeDirector.LitMat(new Color(0.85f, 0.45f, 0.2f), 0.4f);
            var root = new GameObject("Crab");
            var body = Part(PrimitiveType.Sphere, root.transform, new Vector3(0, 0.05f, 0), new Vector3(0.17f, 0.07f, 0.13f), crabShell);
            Part(PrimitiveType.Sphere, body.transform, new Vector3(0.18f, 0.6f, 0.35f), new Vector3(0.12f, 0.35f, 0.12f), crabLeg);   // 眼柄
            Part(PrimitiveType.Sphere, body.transform, new Vector3(-0.18f, 0.6f, 0.35f), new Vector3(0.12f, 0.35f, 0.12f), crabLeg);
            Part(PrimitiveType.Sphere, root.transform, new Vector3(0.09f, 0.05f, 0.1f), new Vector3(0.06f, 0.04f, 0.07f), crabShell); // 钳子
            Part(PrimitiveType.Sphere, root.transform, new Vector3(-0.09f, 0.05f, 0.1f), new Vector3(0.06f, 0.04f, 0.07f), crabShell);
            var legs = new GameObject("Legs").transform;
            legs.SetParent(root.transform, false);
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 3; i++)
                {
                    var pivot = new GameObject("Leg").transform;
                    pivot.SetParent(legs, false);
                    pivot.localPosition = new Vector3(side * 0.07f, 0.05f, -0.04f + i * 0.04f);
                    pivot.localRotation = Quaternion.Euler(0, side * (90 + (i - 1) * 25), 0);
                    Part(PrimitiveType.Cube, pivot, new Vector3(0, -0.02f, 0.06f), new Vector3(0.012f, 0.012f, 0.12f), crabLeg, new Vector3(25, 0, 0));
                }
            return root;
        }

        public static GameObject Butterfly(int variant)
        {
            if (wingMats == null)
            {
                wingMats = new Material[3];
                var colors = new[] { (new Color(0.1f, 0.45f, 1f), new Color(0.02f, 0.05f, 0.15f)),   // 大闪蝶：亮蓝
                                     (new Color(1f, 0.55f, 0.1f), new Color(0.08f, 0.04f, 0.02f)),   // 君主斑蝶：橙黑
                                     (new Color(1f, 0.95f, 0.6f), new Color(0.35f, 0.3f, 0.1f)) };   // 粉蝶：淡黄
                for (int i = 0; i < 3; i++) wingMats[i] = WildlifeDirector.CutoutMat(WingTexture(colors[i].Item1, colors[i].Item2, i), Color.white);
                bodyDark = WildlifeDirector.LitMat(new Color(0.05f, 0.04f, 0.03f));
            }
            var root = new GameObject("Butterfly");
            Part(PrimitiveType.Capsule, root.transform, Vector3.zero, new Vector3(0.012f, 0.025f, 0.012f), bodyDark, new Vector3(90, 0, 0));
            for (int side = -1; side <= 1; side += 2)
            {
                var hinge = new GameObject(side < 0 ? "WingL" : "WingR").transform;
                hinge.SetParent(root.transform, false);
                var q = Part(PrimitiveType.Quad, hinge, new Vector3(side * 0.045f, 0, 0), new Vector3(0.09f * side, 0.085f, 1f), wingMats[variant], new Vector3(90, 0, 0));
                q.name = "Wing";
            }
            return root;
        }

        /// <summary>程序化蝶翼：上下两片翅膀的轮廓、深色翅缘和白斑。u = 0 在身体一侧。</summary>
        static Texture2D WingTexture(Color main, Color edge, int seed)
        {
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            var rnd = new System.Random(seed);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = x / (float)n, v = y / (float)n * 2 - 1;
                    // 前翅（v>0）大，后翅（v<0）小而圆
                    float r = v > 0 ? 0.95f - 0.25f * v * v : 0.75f - 0.5f * v * v;
                    float d = Mathf.Sqrt(u * u * 0.9f + v * v * 0.5f);
                    float inside = d < r && u > 0.02f ? 1 : 0;
                    float edgeK = Mathf.SmoothStep(r - 0.18f, r - 0.05f, d);
                    Color c = Color.Lerp(main * (0.75f + 0.35f * (1 - d)), edge, edgeK);
                    if (edgeK > 0.6f && (x + y * 3) % 17 < 3) c = Color.Lerp(c, Color.white, 0.8f); // 翅缘白斑
                    c.a = inside;
                    px[y * n + x] = c;
                }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        public static GameObject Fish(Transform parent)
        {
            fishMat ??= WildlifeDirector.LitMat(new Color(0.55f, 0.62f, 0.66f), 0.85f);
            var root = new GameObject("Fish");
            root.transform.SetParent(parent, false);
            Part(PrimitiveType.Sphere, root.transform, Vector3.zero, new Vector3(0.05f, 0.07f, 0.24f), fishMat);
            Part(PrimitiveType.Cube, root.transform, new Vector3(0, 0, -0.13f), new Vector3(0.005f, 0.07f, 0.06f), fishMat, new Vector3(0, 0, 0));
            return root;
        }

        public static GameObject Bat()
        {
            batMat ??= WildlifeDirector.LitMat(new Color(0.06f, 0.05f, 0.05f), 0.1f);
            var root = new GameObject("Bat");
            Part(PrimitiveType.Sphere, root.transform, Vector3.zero, new Vector3(0.07f, 0.06f, 0.12f), batMat);
            for (int side = -1; side <= 1; side += 2)
            {
                var hinge = new GameObject(side < 0 ? "WingL" : "WingR").transform;
                hinge.SetParent(root.transform, false);
                Part(PrimitiveType.Cube, hinge, new Vector3(side * 0.16f, 0, -0.02f), new Vector3(0.3f, 0.004f, 0.12f), batMat, new Vector3(0, side * -12f, 0));
            }
            return root;
        }
    }

    // ================= 行为 =================

    /// <summary>螃蟹：在沙滩上横着走走停停；玩家靠近会横着逃开，然后钻进沙里。</summary>
    public class Crab : Critter
    {
        Vector3 target;
        float wait;
        bool fleeing;
        float burrow = -1f;
        Transform legs;

        public override void Init(Terrain t, Transform p)
        {
            base.Init(t, p);
            legs = transform.Find("Legs");
            transform.rotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
            PickTarget();
        }

        void PickTarget()
        {
            var d = Random.insideUnitCircle * 2.5f;
            target = transform.position + new Vector3(d.x, 0, d.y);
            wait = Random.Range(0.4f, 3f);
        }

        void Update()
        {
            if (burrow >= 0f)
            {
                burrow += Time.deltaTime;
                transform.position += Vector3.down * Time.deltaTime * 0.25f;
                if (burrow > 0.7f) Done = true;
                return;
            }

            Vector3 away = transform.position - player.position;
            away.y = 0;
            if (!fleeing && away.magnitude < 4.5f)
            {
                fleeing = true;
                wait = 0;
                Vector3 side = Vector3.Cross(Vector3.up, away.normalized) * Random.Range(-1.5f, 1.5f);
                target = transform.position + away.normalized * 4f + side;
                AudioManager.Play("crab_click", transform.position, 0.8f);
            }

            if (wait > 0 && !fleeing)
            {
                wait -= Time.deltaTime;
                if (wait <= 0) PickTarget();
                Wiggle(0.15f);
                return;
            }

            Vector3 move = target - transform.position;
            move.y = 0;
            float speed = fleeing ? 3.4f : 0.9f;
            if (move.magnitude < 0.1f)
            {
                if (fleeing) burrow = 0f;
                else { wait = Random.Range(0.5f, 3f); PickTarget(); }
                return;
            }
            Vector3 dir = move.normalized;
            Vector3 next = transform.position + dir * Mathf.Min(speed * Time.deltaTime, move.magnitude);
            if (Ground(next) < 0.05f && !fleeing) { PickTarget(); return; } // 不往海里走
            next.y = Ground(next) + 0.02f;
            transform.position = next;
            // 横着走：身体朝向和移动方向垂直
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(Vector3.Cross(Vector3.up, dir)), 10f * Time.deltaTime);
            Wiggle(fleeing ? 1f : 0.6f);
        }

        void Wiggle(float amount)
        {
            if (legs == null) return;
            int i = 0;
            foreach (Transform leg in legs)
            {
                float a = Mathf.Sin(Time.time * 28f + i * 1.7f) * 25f * amount;
                leg.localRotation = Quaternion.Euler(a, leg.localEulerAngles.y, 0);
                i++;
            }
        }
    }

    /// <summary>蝴蝶：在一个锚点附近不规则地飞，翅膀扇动；玩家太近会飞走。</summary>
    public class Butterfly : Critter
    {
        Vector3 anchor;
        float seed;
        Transform wingL, wingR;
        Vector3 last;

        public override void Init(Terrain t, Transform p)
        {
            base.Init(t, p);
            anchor = transform.position;
            seed = Random.Range(0f, 100f);
            wingL = transform.Find("WingL");
            wingR = transform.Find("WingR");
            last = transform.position;
        }

        void Update()
        {
            float t = Time.time * 0.35f + seed;
            Vector3 away = transform.position - player.position;
            if (away.magnitude < 2.2f) anchor += (away.normalized + Vector3.up * 0.3f) * 3f * Time.deltaTime;

            var offs = new Vector3((Mathf.PerlinNoise(t, seed) - 0.5f) * 7f, 0, (Mathf.PerlinNoise(seed, t) - 0.5f) * 7f);
            var p = anchor + offs;
            p.y = Ground(p) + 0.6f + Mathf.PerlinNoise(t * 1.7f, seed + 3) * 1.8f;
            transform.position = Vector3.Lerp(transform.position, p, 3f * Time.deltaTime);

            Vector3 v = transform.position - last;
            last = transform.position;
            if (v.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(new Vector3(v.x, 0, v.z).normalized + Vector3.up * 0.01f), 6f * Time.deltaTime);

            float flap = Mathf.Sin(Time.time * 17f + seed) * 60f + 15f;
            if (wingL) wingL.localRotation = Quaternion.Euler(0, 0, -flap);
            if (wingR) wingR.localRotation = Quaternion.Euler(0, 0, flap);
        }
    }

    /// <summary>浅水鱼群：在浅水里漫游，玩家靠近会一起转向逃开，偶尔有鱼跃出水面的声音。</summary>
    public class FishSchool : Critter
    {
        readonly Transform[] fish = new Transform[10];
        readonly Vector3[] offsets = new Vector3[10];
        Vector3 velocity;
        Vector3 target;
        float splashTimer;

        public override void Init(Terrain t, Transform p)
        {
            base.Init(t, p);
            for (int i = 0; i < fish.Length; i++)
            {
                fish[i] = Critters.Fish(transform).transform;
                offsets[i] = new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(-0.15f, 0.15f), Random.Range(-1.2f, 1.2f));
            }
            PickTarget();
            splashTimer = Random.Range(6f, 20f);
        }

        void PickTarget()
        {
            for (int i = 0; i < 8; i++)
            {
                var d = Random.insideUnitCircle * 10f;
                var p = transform.position + new Vector3(d.x, 0, d.y);
                float h = Ground(p);
                if (h < -0.7f && h > -6f) { target = p; return; }
            }
            target = transform.position;
        }

        void Update()
        {
            Vector3 desired = target - transform.position;
            desired.y = 0;
            Vector3 away = transform.position - player.position;
            away.y = 0;
            bool scared = away.magnitude < 4.5f;
            if (scared) desired = away.normalized * 4f;
            else if (desired.magnitude < 1f) PickTarget();

            float speed = scared ? 3.5f : 1.1f;
            velocity = Vector3.Lerp(velocity, desired.normalized * speed, 2f * Time.deltaTime);
            Vector3 next = transform.position + velocity * Time.deltaTime;
            float depth = -Ground(next);
            if (depth < 0.6f) { PickTarget(); velocity = -velocity * 0.5f; return; } // 太浅了掉头
            next.y = -Mathf.Clamp(depth * 0.45f, 0.25f, 1.2f);
            transform.position = next;

            Quaternion face = velocity.sqrMagnitude > 0.01f ? Quaternion.LookRotation(velocity) : transform.rotation;
            for (int i = 0; i < fish.Length; i++)
            {
                float t = Time.time * 0.6f + i * 3.1f;
                Vector3 jitter = new Vector3(Mathf.PerlinNoise(t, i) - 0.5f, (Mathf.PerlinNoise(i, t) - 0.5f) * 0.3f, Mathf.PerlinNoise(t + 9, i) - 0.5f);
                fish[i].localPosition = Vector3.Lerp(fish[i].localPosition, Quaternion.Inverse(transform.rotation) * (face * (offsets[i] + jitter)), 3f * Time.deltaTime);
                fish[i].rotation = face * Quaternion.Euler(0, Mathf.Sin(Time.time * (scared ? 22f : 9f) + i) * 12f, 0);
            }

            splashTimer -= Time.deltaTime;
            if (splashTimer <= 0)
            {
                splashTimer = Random.Range(8f, 25f);
                AudioManager.Play("fish_splash", new Vector3(transform.position.x, 0, transform.position.z), 0.6f);
            }
        }
    }

    /// <summary>蝙蝠：黄昏和夜里在玩家头顶的树冠上方不规则地盘旋，偶尔尖叫。</summary>
    public class Bat : Critter
    {
        float radius, speed, height, phase, squeak;
        Transform wingL, wingR;

        public override void Init(Terrain t, Transform p)
        {
            base.Init(t, p);
            radius = Random.Range(6f, 16f);
            speed = Random.Range(0.6f, 1.1f) * (Random.value < 0.5f ? 1 : -1);
            height = Random.Range(9f, 17f);
            phase = Random.Range(0, Mathf.PI * 2);
            squeak = Random.Range(3f, 12f);
            wingL = transform.Find("WingL");
            wingR = transform.Find("WingR");
            transform.position = Target(0);
        }

        Vector3 Target(float t)
        {
            float a = phase + t * speed;
            float wob = Mathf.PerlinNoise(t * 0.8f, phase) - 0.5f;
            var c = player.position + new Vector3(Mathf.Cos(a) * radius * (1 + wob), 0, Mathf.Sin(a) * radius * (1 - wob));
            c.y = Mathf.Max(Ground(c), 0) + height + Mathf.Sin(t * 2.3f + phase) * 2f;
            return c;
        }

        void Update()
        {
            var p = Target(Time.time);
            var v = p - transform.position;
            transform.position = Vector3.Lerp(transform.position, p, 4f * Time.deltaTime);
            if (v.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(v.normalized);
            float flap = Mathf.Sin(Time.time * 24f + phase) * 55f;
            if (wingL) wingL.localRotation = Quaternion.Euler(0, 0, -flap);
            if (wingR) wingR.localRotation = Quaternion.Euler(0, 0, flap);

            squeak -= Time.deltaTime;
            if (squeak <= 0)
            {
                squeak = Random.Range(4f, 14f);
                AudioManager.Play("bat_squeak", transform.position, 0.5f);
            }
        }
    }
}
