using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rishi.Builders
{
    /// <summary>
    /// 程序化海洋生物网格。颜色写在顶点色里，顶点色 alpha 是 Island/Marine 着色器的形变权重
    /// （植物：根 0 → 梢 1；鱼：头 0 → 尾 1）。
    /// </summary>
    public static class MarineMeshes
    {
        // ---------------- 通用网格构建 ----------------
        public class MB
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector3> n = new List<Vector3>();
            public readonly List<Color> c = new List<Color>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> t = new List<int>();

            public int Add(Vector3 pos, Vector3 normal, Color color, Vector2 tex = default)
            {
                v.Add(pos); n.Add(normal); c.Add(color); uv.Add(tex);
                return v.Count - 1;
            }

            public void Tri(int a, int b, int d) { t.Add(a); t.Add(b); t.Add(d); }
            public void Quad(int a, int b, int d, int e) { Tri(a, b, d); Tri(a, d, e); }

            /// <summary>沿一条路径的锥形管（截面 sides 边），返回最后一圈的起始索引。</summary>
            public void Tube(IList<Vector3> path, Func<float, float> radius, Func<float, Color> color, int sides, Func<float, float> weight = null, bool capEnd = true)
            {
                int rings = path.Count;
                int start = v.Count;
                for (int i = 0; i < rings; i++)
                {
                    float f = i / (rings - 1f);
                    Vector3 dir = (path[Mathf.Min(i + 1, rings - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
                    Vector3 side = Vector3.Cross(dir, Mathf.Abs(dir.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
                    Vector3 up = Vector3.Cross(side, dir);
                    float r = radius(f);
                    var col = color(f);
                    col.a = weight != null ? weight(f) : 0f;
                    for (int s = 0; s < sides; s++)
                    {
                        float a = s / (float)sides * Mathf.PI * 2f;
                        Vector3 nn = side * Mathf.Cos(a) + up * Mathf.Sin(a);
                        Add(path[i] + nn * r, nn, col, new Vector2(s / (float)sides, f));
                    }
                }
                for (int i = 0; i < rings - 1; i++)
                    for (int s = 0; s < sides; s++)
                    {
                        int a = start + i * sides + s, b = start + i * sides + (s + 1) % sides;
                        Quad(a, a + sides, b + sides, b);
                    }
                if (capEnd)
                {
                    var tipCol = color(1f);
                    tipCol.a = weight != null ? weight(1f) : 0f;
                    Vector3 dir = (path[rings - 1] - path[rings - 2]).normalized;
                    int tip = Add(path[rings - 1] + dir * radius(1f), dir, tipCol, new Vector2(0.5f, 1f));
                    int last = start + (rings - 1) * sides;
                    for (int s = 0; s < sides; s++) Tri(last + s, tip, last + (s + 1) % sides);
                }
            }

            public Mesh Build(string name, bool recalcNormals = false)
            {
                var m = new Mesh { name = name };
                if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                m.SetVertices(v);
                m.SetNormals(n);
                m.SetColors(c);
                m.SetUVs(0, uv);
                m.SetTriangles(t, 0);
                if (recalcNormals) m.RecalculateNormals();
                m.RecalculateBounds();
                return m;
            }
        }

        static float Hash(float x, float y) => Mathf.Repeat(Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f, 1f);
        static float Noise3(Vector3 p) => (Mathf.PerlinNoise(p.x + p.y * 0.37f, p.z - p.y * 0.61f) + Mathf.PerlinNoise(p.z + 13.1f, p.x + p.y + 7.7f)) * 0.5f;
        static Color Mul(Color a, float k) => new Color(a.r * k, a.g * k, a.b * k, a.a);

        // ---------------- 鱼 ----------------
        public class FishShape
        {
            public float length = 0.2f;
            public float width = 0.035f;   // 半宽
            public float height = 0.05f;   // 半高
            public float tailSpread = 1f;  // 尾鳍张开程度
            public float tailFork = 0.6f;  // 0 = 截形尾，1 = 深叉尾
            public float dorsal = 0.5f;    // 背鳍高度（相对 height）
            public float dorsalLength = 0.45f;
            public float pectoral = 0.35f;
            public float noseBlunt = 0.5f;
            public Func<float, float, float, Color> color; // (u: 尾 0 → 头 1, side: 背 1 → 腹 -1, around 0..1) → 颜色
            public Color fin = new Color(0.6f, 0.6f, 0.6f);
            public Color eye = new Color(0.02f, 0.02f, 0.02f);
        }

        public static Mesh Fish(string name, FishShape s)
        {
            var mb = new MB();
            const int rings = 26, sides = 18;
            float L = s.length;
            float zTail = -0.36f * L, zNose = 0.5f * L;
            int start = 0;
            for (int i = 0; i < rings; i++)
            {
                float u = i / (rings - 1f);
                float z = Mathf.Lerp(zTail, zNose, u);
                // 体形：中段最粗，往尾柄收细，鼻端圆钝
                float env = Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Lerp(0.07f, 0.97f, u)), Mathf.Lerp(0.55f, 0.85f, s.noseBlunt));
                env *= Mathf.Lerp(0.32f, 1f, Mathf.SmoothStep(0f, 0.45f, u));
                float w = s.width * env, h = s.height * env;
                float yOff = -0.08f * s.height * Mathf.Sin(Mathf.PI * u); // 腹部略往下鼓
                for (int k = 0; k < sides; k++)
                {
                    float a = k / (float)sides * Mathf.PI * 2f;
                    float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                    var p = new Vector3(ca * w, sa * h + yOff, z);
                    var nrm = new Vector3(ca / Mathf.Max(w, 1e-4f), sa / Mathf.Max(h, 1e-4f), 0).normalized;
                    var col = s.color(u, sa, k / (float)sides);
                    col.a = Mathf.Pow(1f - u, 1.4f);
                    mb.Add(p, nrm, col, new Vector2(u, k / (float)sides));
                }
            }
            for (int i = 0; i < rings - 1; i++)
                for (int k = 0; k < sides; k++)
                {
                    int a = start + i * sides + k, b = start + i * sides + (k + 1) % sides;
                    mb.Quad(a, b, b + sides, a + sides);
                }
            // 封口：尾柄和鼻尖
            var tailCol = s.color(0f, 0f, 0f); tailCol.a = 1f;
            int tailCenter = mb.Add(new Vector3(0, 0, zTail - 0.005f), Vector3.back, tailCol);
            for (int k = 0; k < sides; k++) mb.Tri(tailCenter, (k + 1) % sides, k);
            var noseCol = s.color(1f, 0.3f, 0f); noseCol.a = 0f;
            int nose = mb.Add(new Vector3(0, -0.05f * s.height, zNose + 0.01f * L), Vector3.forward, noseCol);
            int lastRing = (rings - 1) * sides;
            for (int k = 0; k < sides; k++) mb.Tri(lastRing + k, lastRing + (k + 1) % sides, nose);

            // 尾鳍（叉尾），双面渲染
            Color fin = s.fin;
            Color finTail = fin; finTail.a = 1f;
            float th = s.height * 0.85f * s.tailSpread;
            float zt = zTail - 0.2f * L;
            Vector3 n0 = Vector3.right;
            int p0 = mb.Add(new Vector3(0, s.height * 0.12f, zTail + 0.01f), n0, finTail);
            int p1 = mb.Add(new Vector3(0, -s.height * 0.12f, zTail + 0.01f), n0, finTail);
            int up = mb.Add(new Vector3(0, th, zt), n0, finTail);
            int dn = mb.Add(new Vector3(0, -th, zt), n0, finTail);
            int notch = mb.Add(new Vector3(0, 0, Mathf.Lerp(zt, zTail - 0.02f * L, s.tailFork)), n0, finTail);
            mb.Tri(p0, up, notch); mb.Tri(p0, notch, p1); mb.Tri(p1, notch, dn);

            // 背鳍
            if (s.dorsal > 0.01f)
            {
                float zb0 = Mathf.Lerp(zTail, zNose, 0.62f), zb1 = Mathf.Lerp(zTail, zNose, 0.62f - s.dorsalLength);
                float top = s.height * 0.9f;
                Color fd = fin; fd.a = 0.35f;
                int d0 = mb.Add(new Vector3(0, top, zb0), n0, fd);
                int d1 = mb.Add(new Vector3(0, top * 0.85f, zb1), n0, fd);
                int d2 = mb.Add(new Vector3(0, top + s.height * s.dorsal, Mathf.Lerp(zb0, zb1, 0.35f)), n0, fd);
                int d3 = mb.Add(new Vector3(0, top + s.height * s.dorsal * 0.5f, zb1 - 0.03f * L), n0, fd);
                mb.Quad(d0, d2, d3, d1);
                // 臀鳍
                int a0 = mb.Add(new Vector3(0, -top * 0.9f, Mathf.Lerp(zTail, zNose, 0.35f)), n0, fd);
                int a1 = mb.Add(new Vector3(0, -top * 0.8f, Mathf.Lerp(zTail, zNose, 0.12f)), n0, fd);
                int a2 = mb.Add(new Vector3(0, -top - s.height * s.dorsal * 0.55f, Mathf.Lerp(zTail, zNose, 0.18f)), n0, fd);
                mb.Tri(a0, a1, a2);
            }
            // 胸鳍
            for (int side = -1; side <= 1; side += 2)
            {
                float zp = Mathf.Lerp(zTail, zNose, 0.7f);
                float x = s.width * 0.75f * side;
                Color fp = fin; fp.a = 0.1f;
                var nn = new Vector3(0, 1, 0);
                int q0 = mb.Add(new Vector3(x, -s.height * 0.2f, zp), nn, fp);
                int q1 = mb.Add(new Vector3(x, -s.height * 0.35f, zp - 0.05f * L), nn, fp);
                int q2 = mb.Add(new Vector3(x + side * s.height * s.pectoral, -s.height * 0.45f, zp - 0.12f * L), nn, fp);
                mb.Tri(q0, q1, q2);
            }
            // 眼睛：两侧小圆盘（黑瞳 + 浅色眼圈）
            for (int side = -1; side <= 1; side += 2)
            {
                float u = 0.86f;
                float z = Mathf.Lerp(zTail, zNose, u);
                float env = Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Lerp(0.07f, 0.97f, u)), Mathf.Lerp(0.55f, 0.85f, s.noseBlunt));
                var center = new Vector3(side * s.width * env * 0.93f + side * 0.0015f, s.height * env * 0.25f, z);
                float r = s.height * 0.16f;
                var nn = new Vector3(side, 0, 0);
                var eyeCol = s.eye; eyeCol.a = 0f;
                var ringCol = new Color(0.85f, 0.8f, 0.6f, 0f);
                int cIdx = mb.Add(center + nn * 0.001f, nn, eyeCol);
                int first = mb.v.Count;
                for (int k = 0; k < 10; k++)
                {
                    float a = k / 10f * Mathf.PI * 2f;
                    mb.Add(center + new Vector3(0, Mathf.Sin(a), Mathf.Cos(a)) * r, nn, k % 2 == 0 ? eyeCol : eyeCol);
                }
                int ringStart = mb.v.Count;
                for (int k = 0; k < 10; k++)
                {
                    float a = k / 10f * Mathf.PI * 2f;
                    mb.Add(center + new Vector3(0, Mathf.Sin(a), Mathf.Cos(a)) * r * 1.5f - nn * 0.0008f, nn, ringCol);
                }
                for (int k = 0; k < 10; k++)
                {
                    int a = first + k, b = first + (k + 1) % 10;
                    if (side > 0) mb.Tri(cIdx, b, a); else mb.Tri(cIdx, a, b);
                    int ra = ringStart + k, rb = ringStart + (k + 1) % 10;
                    if (side > 0) mb.Quad(a, b, rb, ra); else mb.Quad(a, ra, rb, b);
                }
            }
            return mb.Build(name);
        }

        /// <summary>鹞鲼：翅膀沿 ±Y 建模（模型绕 Z 转 90° 放平后，着色器的左右摆动就成了上下扇翅）。</summary>
        public static Mesh EagleRay(string name, float span)
        {
            var mb = new MB();
            const int ny = 30, nz = 16;
            float S = span;
            for (int side = 0; side < 2; side++) // 0 = 背面（+X），1 = 腹面（-X）
            {
                int start = mb.v.Count;
                for (int iy = 0; iy <= ny; iy++)
                {
                    float sv = iy / (float)ny * 2f - 1f; // -1..1 翼展
                    float a = Mathf.Abs(sv);
                    float front = 0.32f * Mathf.Pow(1f - a, 0.75f) * S + 0.02f * S;
                    float back = -0.22f * Mathf.Pow(1f - a, 1.3f) * S - 0.12f * a * a * S;
                    for (int iz = 0; iz <= nz; iz++)
                    {
                        float f = iz / (float)nz;
                        float z = Mathf.Lerp(back, front, f) - 0.1f * a * a * S;
                        float thick = 0.07f * S * Mathf.Pow(1f - a, 1.6f) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(f * 0.95f + 0.03f));
                        float x = side == 0 ? thick : -thick * 0.6f;
                        var p = new Vector3(x, sv * S * 0.5f, z);
                        Color col;
                        if (side == 0)
                        {
                            col = new Color(0.07f, 0.08f, 0.1f);
                            if (Hash(Mathf.Floor(sv * 14f), Mathf.Floor(f * 9f)) > 0.82f && a < 0.85f) col = new Color(0.85f, 0.85f, 0.8f); // 白色斑点
                        }
                        else col = new Color(0.9f, 0.9f, 0.88f);
                        col.a = a;
                        mb.Add(p, side == 0 ? Vector3.right : Vector3.left, col, new Vector2(f, (sv + 1) * 0.5f));
                    }
                }
                for (int iy = 0; iy < ny; iy++)
                    for (int iz = 0; iz < nz; iz++)
                    {
                        int a = start + iy * (nz + 1) + iz;
                        int b = a + nz + 1;
                        if (side == 0) mb.Quad(a, b, b + 1, a + 1); else mb.Quad(a, a + 1, b + 1, b);
                    }
            }
            // 头部突起和长尾
            var tail = new List<Vector3>();
            for (int i = 0; i < 8; i++) tail.Add(new Vector3(0, 0, -0.2f * S - i * 0.14f * S));
            mb.Tube(tail, f => 0.012f * S * (1f - f * 0.9f), f => new Color(0.08f, 0.08f, 0.1f), 5, f => 0.3f);
            var head = new List<Vector3> { new Vector3(0.01f * S, 0, 0.2f * S), new Vector3(0.015f * S, 0, 0.33f * S), new Vector3(0.01f * S, 0, 0.42f * S) };
            mb.Tube(head, f => 0.05f * S * (1f - f * 0.6f), f => new Color(0.08f, 0.09f, 0.11f), 8);
            var mesh = mb.Build(name);
            return mesh;
        }

        // ---------------- 海草 / 海带 ----------------
        public static Mesh SeagrassPatch(string name, int seed, float size = 2.2f, int blades = 170)
        {
            var rnd = new System.Random(seed);
            float R() => (float)rnd.NextDouble();
            var mb = new MB();
            for (int b = 0; b < blades; b++)
            {
                // 簇状分布
                float ang = R() * Mathf.PI * 2f, rad = Mathf.Sqrt(R()) * size * 0.5f;
                var basePos = new Vector3(Mathf.Cos(ang) * rad, 0, Mathf.Sin(ang) * rad);
                float h = Mathf.Lerp(0.3f, 1.05f, R() * R() * 0.5f + R() * 0.5f);
                float w = Mathf.Lerp(0.011f, 0.022f, R());
                float lean = R() * Mathf.PI * 2f;
                var leanDir = new Vector3(Mathf.Cos(lean), 0, Mathf.Sin(lean));
                var widthDir = Vector3.Cross(leanDir, Vector3.up).normalized;
                float bend = Mathf.Lerp(0.1f, 0.45f, R());
                bool dead = R() < 0.12f;
                Color baseC = new Color(0.07f, 0.16f, 0.06f), tipC = dead ? new Color(0.45f, 0.38f, 0.16f) : new Color(0.3f, 0.48f, 0.14f);
                tipC = Color.Lerp(tipC, new Color(0.22f, 0.36f, 0.1f), R() * 0.5f);
                const int seg = 5;
                int start = mb.v.Count;
                for (int i = 0; i <= seg; i++)
                {
                    float f = i / (float)seg;
                    var p = basePos + Vector3.up * h * f + leanDir * bend * h * f * f;
                    var col = Color.Lerp(baseC, tipC, Mathf.Pow(f, 0.7f));
                    col.a = f;
                    float ww = w * (1f - f * 0.6f);
                    // 法线偏向上方，让叶片在水下光照里不会忽明忽暗
                    var nrm = Vector3.Lerp(Vector3.Cross(widthDir, (leanDir * 2f * bend * f + Vector3.up).normalized), Vector3.up, 0.6f).normalized;
                    mb.Add(p - widthDir * ww, nrm, col, new Vector2(0, f));
                    mb.Add(p + widthDir * ww, nrm, col, new Vector2(1, f));
                }
                for (int i = 0; i < seg; i++)
                {
                    int a = start + i * 2;
                    mb.Quad(a, a + 2, a + 3, a + 1);
                }
            }
            return mb.Build(name);
        }

        public static Mesh Kelp(string name, int seed, float height)
        {
            var rnd = new System.Random(seed);
            float R() => (float)rnd.NextDouble();
            var mb = new MB();
            // 主茎：轻微弯曲的细带
            var stipe = new List<Vector3>();
            int segs = Mathf.Max(6, Mathf.RoundToInt(height * 2.2f));
            float wob = R() * 10f;
            for (int i = 0; i <= segs; i++)
            {
                float f = i / (float)segs;
                stipe.Add(new Vector3(Mathf.Sin(f * 5f + wob) * 0.12f * f, f * height, Mathf.Cos(f * 4f + wob) * 0.1f * f));
            }
            mb.Tube(stipe, f => Mathf.Lerp(0.022f, 0.012f, f), f => Color.Lerp(new Color(0.25f, 0.18f, 0.06f), new Color(0.42f, 0.33f, 0.1f), f), 5, f => f, false);
            // 叶片：沿茎每隔一段长出一片，带气囊，交替朝向
            float y = 0.6f;
            int idx = 0;
            while (y < height - 0.3f)
            {
                float f = y / height;
                Vector3 at = new Vector3(Mathf.Sin(f * 5f + wob) * 0.12f * f, y, Mathf.Cos(f * 4f + wob) * 0.1f * f);
                float ang = idx * 2.4f + R() * 0.8f;
                var dir = new Vector3(Mathf.Cos(ang), 0.55f + R() * 0.4f, Mathf.Sin(ang)).normalized;
                float len = Mathf.Lerp(0.55f, 1.05f, R()) * Mathf.Lerp(0.8f, 1.1f, f);
                float wid = Mathf.Lerp(0.07f, 0.12f, R());
                var widthDir = Vector3.Cross(dir, Vector3.up).normalized;
                if (widthDir.sqrMagnitude < 0.01f) widthDir = Vector3.right;
                var bladeN = Vector3.Cross(widthDir, dir).normalized;
                // 气囊
                var bladder = new List<Vector3> { at + dir * 0.02f, at + dir * 0.06f, at + dir * 0.1f };
                mb.Tube(bladder, t => 0.022f * Mathf.Sin(Mathf.PI * Mathf.Lerp(0.15f, 0.85f, t)), t => new Color(0.5f, 0.4f, 0.14f), 6, t => f, false);
                const int bs = 6;
                int start = mb.v.Count;
                Vector3 bladeStart = at + dir * 0.1f;
                for (int i = 0; i <= bs; i++)
                {
                    float t = i / (float)bs;
                    var p = bladeStart + dir * len * t + Vector3.down * 0.12f * t * t; // 叶梢下垂
                    float w = wid * Mathf.Sin(Mathf.PI * Mathf.Lerp(0.12f, 1f, t)) * (1f + 0.25f * Mathf.Sin(t * 18f)); // 波浪边
                    var col = Color.Lerp(new Color(0.36f, 0.3f, 0.08f), new Color(0.55f, 0.46f, 0.14f), t);
                    col.a = Mathf.Clamp01(f + t * 0.15f);
                    mb.Add(p - widthDir * w, bladeN, col, new Vector2(0, t));
                    mb.Add(p + widthDir * w, bladeN, col, new Vector2(1, t));
                }
                for (int i = 0; i < bs; i++)
                {
                    int a = start + i * 2;
                    mb.Quad(a, a + 2, a + 3, a + 1);
                }
                y += Mathf.Lerp(0.28f, 0.45f, R());
                idx++;
            }
            return mb.Build(name);
        }

        // ---------------- 珊瑚 ----------------
        public static Mesh Staghorn(string name, int seed, Color baseColor, Color tipColor)
        {
            var rnd = new System.Random(seed);
            float R() => (float)rnd.NextDouble();
            var mb = new MB();
            void Branch(Vector3 from, Vector3 dir, float len, float radius, int depth)
            {
                var path = new List<Vector3>();
                Vector3 p = from;
                int segs = 4;
                for (int i = 0; i <= segs; i++)
                {
                    path.Add(p);
                    dir = (dir + new Vector3(R() - 0.5f, R() * 0.3f, R() - 0.5f) * 0.25f).normalized;
                    p += dir * len / segs;
                }
                bool leaf = depth == 0;
                mb.Tube(path, f => Mathf.Lerp(radius, radius * 0.7f, f), f => leaf ? Color.Lerp(baseColor, tipColor, Mathf.SmoothStep(0.4f, 1f, f)) : baseColor, 6, null, leaf);
                if (depth <= 0) return;
                int kids = 2 + (R() > 0.6f ? 1 : 0);
                for (int k = 0; k < kids; k++)
                {
                    var nd = Quaternion.AngleAxis(R() * 360f, dir) * Quaternion.AngleAxis(25f + R() * 25f, Vector3.Cross(dir, Vector3.up + Vector3.right * 0.01f)) * dir;
                    nd = Vector3.Lerp(nd, Vector3.up, 0.25f).normalized;
                    Branch(p, nd, len * Mathf.Lerp(0.6f, 0.85f, R()), radius * 0.75f, depth - 1);
                }
            }
            int trunks = 5 + rnd.Next(4);
            for (int i = 0; i < trunks; i++)
            {
                float a = i / (float)trunks * Mathf.PI * 2f + R();
                var d = new Vector3(Mathf.Cos(a) * 0.6f, 1f, Mathf.Sin(a) * 0.6f).normalized;
                Branch(new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.05f, d, Mathf.Lerp(0.16f, 0.26f, R()), 0.022f, 2);
            }
            return mb.Build(name);
        }

        public static Mesh BrainCoral(string name, int seed, float radius, Color ridge, Color groove)
        {
            var mb = new MB();
            const int lat = 22, lon = 44;
            float off = seed * 3.7f;
            for (int i = 0; i <= lat; i++)
            {
                float th = i / (float)lat * Mathf.PI * 0.5f; // 只要上半球
                for (int j = 0; j <= lon; j++)
                {
                    float ph = j / (float)lon * Mathf.PI * 2f;
                    var dir = new Vector3(Mathf.Cos(ph) * Mathf.Sin(Mathf.PI * 0.5f - th), Mathf.Cos(Mathf.PI * 0.5f - th), Mathf.Sin(ph) * Mathf.Sin(Mathf.PI * 0.5f - th));
                    // 迷宫状沟回：扭曲后的正弦条纹
                    var q = dir * 3.2f + Vector3.one * off;
                    float warp = Noise3(q) * 9f;
                    float stripes = Mathf.Abs(Mathf.Sin((dir.x * 7f + dir.z * 5f + warp) * 2.2f));
                    float g = Mathf.SmoothStep(0.55f, 0.95f, stripes);
                    float r = radius * (1f - 0.045f * g) * (0.92f + 0.16f * Noise3(dir * 1.3f + Vector3.one * off));
                    var p = new Vector3(dir.x * r, dir.y * r * 0.68f, dir.z * r);
                    var col = Color.Lerp(ridge, groove, g);
                    col = Mul(col, Mathf.Lerp(0.75f, 1f, dir.y)); // 底部暗一点
                    col.a = 0f;
                    mb.Add(p, dir, col, new Vector2(j / (float)lon, i / (float)lat));
                }
            }
            for (int i = 0; i < lat; i++)
                for (int j = 0; j < lon; j++)
                {
                    int a = i * (lon + 1) + j, b = a + lon + 1;
                    mb.Quad(a, b, b + 1, a + 1);
                }
            var m = mb.Build(name, recalcNormals: true);
            return m;
        }

        public static Mesh TableCoral(string name, int seed, float radius, Color top, Color rim)
        {
            var rnd = new System.Random(seed);
            float R() => (float)rnd.NextDouble();
            var mb = new MB();
            float stemH = Mathf.Lerp(0.18f, 0.32f, R());
            mb.Tube(new List<Vector3> { Vector3.zero, Vector3.up * stemH * 0.5f, Vector3.up * stemH }, f => Mathf.Lerp(0.07f, 0.05f, f), f => Mul(top, 0.7f), 7, null, false);
            const int rings = 8, segs = 40;
            float phase = R() * 10f;
            for (int face = 0; face < 2; face++)
            {
                int start = mb.v.Count;
                for (int i = 0; i <= rings; i++)
                {
                    float f = i / (float)rings;
                    for (int j = 0; j <= segs; j++)
                    {
                        float a = j / (float)segs * Mathf.PI * 2f;
                        float rr = radius * f * (1f + 0.12f * Mathf.Sin(a * 7f + phase) * f + 0.06f * Mathf.Sin(a * 17f) * f);
                        float y = stemH + f * f * 0.12f * radius + (face == 1 ? -0.025f : 0f) + 0.03f * Mathf.Sin(a * 5f + phase) * f;
                        var col = Color.Lerp(top, rim, Mathf.SmoothStep(0.75f, 1f, f));
                        if (face == 1) col = Mul(col, 0.55f);
                        col.a = 0f;
                        mb.Add(new Vector3(Mathf.Cos(a) * rr, y, Mathf.Sin(a) * rr), face == 0 ? Vector3.up : Vector3.down, col);
                    }
                }
                for (int i = 0; i < rings; i++)
                    for (int j = 0; j < segs; j++)
                    {
                        int a = start + i * (segs + 1) + j, b = a + segs + 1;
                        if (face == 0) mb.Quad(a, b, b + 1, a + 1); else mb.Quad(a, a + 1, b + 1, b);
                    }
            }
            return mb.Build(name, recalcNormals: true);
        }

        public static Mesh FanCoral(string name, int seed, float height, Color color)
        {
            var rnd = new System.Random(seed);
            float R() => (float)rnd.NextDouble();
            var mb = new MB();
            // 在 XY 平面里生长的网状分枝
            void Grow(Vector2 from, float angle, float len, float width, int depth)
            {
                Vector2 dir = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
                Vector2 to = from + dir * len;
                Vector2 side = new Vector2(dir.y, -dir.x) * width;
                Color c0 = color, c1 = Color.Lerp(color, Color.white, 0.12f);
                c0.a = Mathf.Clamp01(from.y / height); c1.a = Mathf.Clamp01(to.y / height);
                int a = mb.Add(new Vector3(from.x - side.x, from.y - side.y, 0), Vector3.forward, c0);
                int b = mb.Add(new Vector3(from.x + side.x, from.y + side.y, 0), Vector3.forward, c0);
                int c = mb.Add(new Vector3(to.x + side.x * 0.8f, to.y + side.y * 0.8f, 0), Vector3.forward, c1);
                int d = mb.Add(new Vector3(to.x - side.x * 0.8f, to.y - side.y * 0.8f, 0), Vector3.forward, c1);
                mb.Quad(a, d, c, b);
                if (depth <= 0 || to.y > height) return;
                int kids = R() > 0.35f ? 2 : 1;
                for (int k = 0; k < kids; k++)
                {
                    float na = angle + (kids == 1 ? (R() - 0.5f) * 0.4f : (k == 0 ? -1f : 1f) * Mathf.Lerp(0.25f, 0.55f, R()));
                    na = Mathf.Clamp(na, -1.2f, 1.2f);
                    Grow(to, na, len * Mathf.Lerp(0.82f, 0.98f, R()), width * 0.85f, depth - 1);
                }
            }
            Grow(Vector2.zero, 0f, height * 0.16f, 0.012f, 7);
            return mb.Build(name);
        }

        public static Mesh TubeSponge(string name, int seed, Color color)
        {
            var rnd = new System.Random(seed);
            float R() => (float)rnd.NextDouble();
            var mb = new MB();
            int tubes = 3 + rnd.Next(4);
            for (int i = 0; i < tubes; i++)
            {
                float a = R() * Mathf.PI * 2f, d = R() * 0.12f;
                var b = new Vector3(Mathf.Cos(a) * d, 0, Mathf.Sin(a) * d);
                float h = Mathf.Lerp(0.25f, 0.65f, R());
                float r = Mathf.Lerp(0.045f, 0.08f, R());
                var lean = new Vector3(Mathf.Cos(a), 4f, Mathf.Sin(a)).normalized;
                var path = new List<Vector3>();
                for (int k = 0; k <= 5; k++) path.Add(b + lean * h * k / 5f);
                Color c = Color.Lerp(color, Mul(color, 0.8f), R());
                mb.Tube(path, f => r * (1f + 0.12f * f), f => Color.Lerp(Mul(c, 0.8f), Color.Lerp(c, Color.white, 0.15f), f), 10, null, false);
                // 内壁（暗）
                var inner = new List<Vector3>(path);
                inner.Reverse();
                mb.Tube(inner, f => r * (1f + 0.12f * (1f - f)) * 0.82f, f => Mul(c, 0.3f), 10, null, false);
            }
            return mb.Build(name);
        }

        public static Mesh Anemone(string name, int seed, Color tentacle, Color tip)
        {
            var rnd = new System.Random(seed);
            float R() => (float)rnd.NextDouble();
            var mb = new MB();
            mb.Tube(new List<Vector3> { Vector3.zero, Vector3.up * 0.05f, Vector3.up * 0.09f }, f => Mathf.Lerp(0.13f, 0.11f, f), f => Mul(tentacle, 0.55f), 12, null, false);
            int count = 70 + rnd.Next(30);
            for (int i = 0; i < count; i++)
            {
                float a = R() * Mathf.PI * 2f, rr = Mathf.Sqrt(R()) * 0.11f;
                var basePos = new Vector3(Mathf.Cos(a) * rr, 0.09f, Mathf.Sin(a) * rr);
                var outward = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                float len = Mathf.Lerp(0.1f, 0.22f, R());
                var path = new List<Vector3>();
                for (int k = 0; k <= 4; k++)
                {
                    float f = k / 4f;
                    path.Add(basePos + (Vector3.up * (1f - f * 0.3f) + outward * (rr / 0.11f) * f * 0.9f).normalized * len * f);
                }
                mb.Tube(path, f => Mathf.Lerp(0.009f, 0.006f, f), f => Color.Lerp(tentacle, tip, Mathf.SmoothStep(0.6f, 1f, f)), 4, f => 0.25f + f * 0.75f, true);
            }
            return mb.Build(name);
        }

        public static Mesh Urchin(string name, int seed)
        {
            var rnd = new System.Random(seed);
            float R() => (float)rnd.NextDouble();
            var mb = new MB();
            Color body = new Color(0.08f, 0.04f, 0.08f), spine = new Color(0.12f, 0.06f, 0.14f);
            var core = new List<Vector3> { Vector3.zero, Vector3.up * 0.05f, Vector3.up * 0.09f };
            mb.Tube(core, f => 0.06f * Mathf.Sin(Mathf.PI * Mathf.Lerp(0.2f, 0.85f, f)) + 0.01f, f => body, 10, null, true);
            for (int i = 0; i < 60; i++)
            {
                var d = UnityEngine.Random.onUnitSphere;
                d = new Vector3((float)(R() * 2 - 1), Mathf.Abs((float)(R() * 2 - 1)) * 0.9f + 0.1f, (float)(R() * 2 - 1)).normalized;
                var start = Vector3.up * 0.045f + d * 0.05f;
                mb.Tube(new List<Vector3> { start, start + d * 0.12f, start + d * 0.2f }, f => 0.004f * (1f - f), f => spine, 3, null, true);
            }
            return mb.Build(name);
        }

        public static Mesh Starfish(string name, int seed, float radius, Color color)
        {
            var rnd = new System.Random(seed);
            float R() => (float)rnd.NextDouble();
            var mb = new MB();
            const int segs = 60, rings = 6;
            float tw = R() * 0.3f;
            int start = mb.v.Count;
            for (int i = 0; i <= rings; i++)
            {
                float f = i / (float)rings;
                for (int j = 0; j <= segs; j++)
                {
                    float a = j / (float)segs * Mathf.PI * 2f;
                    float arm = Mathf.Pow(Mathf.Abs(Mathf.Cos(a * 2.5f + tw)), 3f);
                    float r = radius * f * Mathf.Lerp(0.3f, 1f, arm);
                    float h = 0.022f * (1f - f * f) * Mathf.Lerp(0.6f, 1f, arm) + 0.004f;
                    var col = Mul(color, Mathf.Lerp(0.8f, 1.05f, Hash(i, j)));
                    col.a = 0f;
                    mb.Add(new Vector3(Mathf.Cos(a) * r, h, Mathf.Sin(a) * r), Vector3.up, col);
                }
            }
            for (int i = 0; i < rings; i++)
                for (int j = 0; j < segs; j++)
                {
                    int a = start + i * (segs + 1) + j, b = a + segs + 1;
                    mb.Quad(a, b, b + 1, a + 1);
                }
            return mb.Build(name, recalcNormals: true);
        }

        /// <summary>扇贝壳：放射状肋纹的扇形。</summary>
        public static Mesh Scallop(string name, Color color)
        {
            var mb = new MB();
            const int rings = 8, segs = 24;
            for (int i = 0; i <= rings; i++)
            {
                float f = i / (float)rings;
                for (int j = 0; j <= segs; j++)
                {
                    float a = Mathf.Lerp(-1.25f, 1.25f, j / (float)segs);
                    float rib = Mathf.Abs(Mathf.Sin(j * Mathf.PI * 0.5f)) * 0.006f * f;
                    float r = 0.07f * f;
                    var p = new Vector3(Mathf.Sin(a) * r, 0.025f * Mathf.Sin(Mathf.PI * f * 0.9f) * Mathf.Cos(a * 0.8f) + rib, Mathf.Cos(a) * r - 0.03f);
                    var col = Color.Lerp(Mul(color, 0.8f), color, f);
                    if (i % 3 == 0) col = Mul(col, 0.88f);
                    col.a = 0f;
                    mb.Add(p, Vector3.up, col);
                }
            }
            for (int i = 0; i < rings; i++)
                for (int j = 0; j < segs; j++)
                {
                    int a = i * (segs + 1) + j, b = a + segs + 1;
                    mb.Quad(a, b, b + 1, a + 1);
                }
            return mb.Build(name, recalcNormals: true);
        }

        /// <summary>海螺：螺旋收尖的纺锤体，开口处粉色。</summary>
        public static Mesh Conch(string name)
        {
            var mb = new MB();
            var path = new List<Vector3>();
            for (int i = 0; i <= 10; i++) path.Add(new Vector3(0, 0.02f, -0.07f + i * 0.016f));
            mb.Tube(path, f => 0.035f * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Lerp(0.12f, 0.95f, f)), 0.8f) * (1f + 0.15f * Mathf.Sin(f * 40f)),
                f => Color.Lerp(new Color(0.93f, 0.85f, 0.7f), new Color(0.8f, 0.62f, 0.45f), Mathf.Abs(Mathf.Sin(f * 22f))), 10, null, true);
            var lip = new List<Vector3> { new Vector3(0.025f, 0.02f, -0.02f), new Vector3(0.04f, 0.02f, 0.01f), new Vector3(0.03f, 0.02f, 0.04f) };
            mb.Tube(lip, f => 0.014f, f => new Color(0.95f, 0.6f, 0.55f), 6, null, true);
            return mb.Build(name);
        }

        /// <summary>巨型砗磲的一半壳（波浪边的碗），mantle = true 时是内部彩色外套膜。</summary>
        public static Mesh ClamHalf(string name, float sizeX, float sizeZ, bool top, Color shell, Color mantle)
        {
            var mb = new MB();
            const int rings = 10, segs = 32;
            for (int face = 0; face < 2; face++)
            {
                int start = mb.v.Count;
                for (int i = 0; i <= rings; i++)
                {
                    float f = i / (float)rings;
                    for (int j = 0; j <= segs; j++)
                    {
                        float a = Mathf.Lerp(0f, Mathf.PI, j / (float)segs);
                        float wave = 0.03f * Mathf.Sin(a * 9f) * f; // 波浪状的壳缘
                        float x = Mathf.Cos(a) * sizeX * f, z = -sizeZ * 0.5f + Mathf.Sin(a) * sizeZ * f;
                        float y = (0.16f * (1f - f * f) * sizeX + wave) * (top ? 1f : -1f) * (face == 0 ? 1f : 0.85f);
                        Color col;
                        if (face == 0) col = Color.Lerp(Mul(shell, 0.75f), shell, f) * (0.9f + 0.1f * Mathf.Sin(a * 9f));
                        else
                        {
                            // 内侧：外套膜，蓝绿色带亮斑
                            col = Color.Lerp(mantle, Color.Lerp(mantle, new Color(0.2f, 0.9f, 0.85f), 0.5f), Mathf.Abs(Mathf.Sin(a * 13f + f * 9f)));
                            if (Hash(i, j) > 0.85f) col = Color.Lerp(col, Color.white, 0.6f);
                        }
                        col.a = 0f;
                        mb.Add(new Vector3(x, y, z), (top ? Vector3.up : Vector3.down) * (face == 0 ? 1f : -1f), col);
                    }
                }
                for (int i = 0; i < rings; i++)
                    for (int j = 0; j < segs; j++)
                    {
                        int a = start + i * (segs + 1) + j, b = a + segs + 1;
                        bool flip = (face == 0) ^ !top;
                        if (flip) mb.Quad(a, b, b + 1, a + 1); else mb.Quad(a, a + 1, b + 1, b);
                    }
            }
            return mb.Build(name, recalcNormals: true);
        }
    }
}
