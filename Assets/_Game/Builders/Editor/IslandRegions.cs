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
    /// 晨星岛的地貌分区：北岸海崖、东岸红树林沼泽、西侧溪谷、中央主峰。
    /// 地形高度、地表绘制、植被和模型摆放都用这里的分区权重，保证彼此一致。
    /// 坐标为相对岛中心的世界 xz（米），高度为海平面以上（米）。
    /// </summary>
    public static class IslandRegions
    {
        /// <summary>溪流路线：从主峰西侧山腰一路流到西海岸。</summary>
        public static readonly Vector2[] RiverPath =
        {
            new Vector2(18, 62), new Vector2(-12, 52), new Vector2(-45, 44), new Vector2(-80, 30),
            new Vector2(-115, 18), new Vector2(-150, 8), new Vector2(-190, 2), new Vector2(-235, -4), new Vector2(-285, -8),
        };
        public const float RiverSourceBed = 26f;
        public const float RiverHalfWidth = 3.2f;   // 水面半宽
        public const float RiverValleyWidth = 16f;  // 河谷影响范围

        public struct Info
        {
            public float cliff;      // 北岸海崖 0~1
            public float swamp;      // 东岸沼泽 0~1
            public float riverDist;  // 到溪流中线的距离（米）
            public float riverT;     // 在溪流上的位置 0 = 源头，1 = 入海口
        }

        public static Info At(float x, float z)
        {
            float len = Mathf.Sqrt(x * x + z * z) + 1f;
            float nx = x / len, nz = z / len;
            var info = new Info
            {
                cliff = SmoothStep(0.3f, 0.72f, nz),
                swamp = SmoothStep(0.5f, 0.82f, nx) * (1f - SmoothStep(0.35f, 0.62f, Mathf.Abs(nz))),
            };
            RiverDistance(new Vector2(x, z), out info.riverDist, out info.riverT);
            return info;
        }

        static float[] bedTable;

        /// <summary>瀑布落差（米）。</summary>
        public const float WaterfallHeight = 4f;
        /// <summary>瀑布在溪流上的位置（t：0 = 源头，1 = 入海口），PrepareRiver 之后有效；没有合适位置时为 -1。</summary>
        public static float WaterfallT { get; private set; } = -1f;

        /// <summary>
        /// 按未刻河槽的地形算出河床：沿路线每 2 米取地面高度减 2.2 米，往下游只降不升，入海口略低于海平面。
        /// 生成地形高度前调用一次。
        /// </summary>
        public static void PrepareRiver(System.Func<float, float, float> baseHeight)
        {
            const int n = 400;
            bedTable = new float[n];
            float total = 0;
            for (int i = 0; i < RiverPath.Length - 1; i++) total += Vector2.Distance(RiverPath[i], RiverPath[i + 1]);
            for (int k = 0; k < n; k++)
            {
                Vector2 p = PointAt(k / (n - 1f), total);
                float bed = baseHeight(p.x, p.y) - 2.2f;
                if (k > 0) bed = Mathf.Min(bed, bedTable[k - 1]);
                bedTable[k] = Mathf.Max(bed, -0.8f);
            }

            // 瀑布：在中游（t 0.2~0.6）找天然最陡的一段，把它下游的河床都压到瀑布顶下方 WaterfallHeight 米，
            // 形成一道跌水和一段逐渐变浅的峡谷。河床仍然只降不升，水面不会埋进地形。
            const int span = 12;
            int best = -1;
            float bestDrop = 0f;
            for (int k = Mathf.Max(span, (int)(n * 0.2f)); k <= Mathf.Min(n - 1 - span, (int)(n * 0.6f)); k++)
            {
                float drop = bedTable[k - span] - bedTable[k + span];
                if (drop > bestDrop) { bestDrop = drop; best = k; }
            }
            WaterfallT = -1f;
            if (best >= 0 && bedTable[best] - WaterfallHeight > 0.5f)
            {
                float cap = bedTable[best] - WaterfallHeight;
                for (int k = best + 1; k < n; k++) bedTable[k] = Mathf.Min(bedTable[k], cap);
                WaterfallT = best / (n - 1f);
            }
        }

        static Vector2 PointAt(float t, float total)
        {
            float target = t * total, acc = 0;
            for (int i = 0; i < RiverPath.Length - 1; i++)
            {
                float len = Vector2.Distance(RiverPath[i], RiverPath[i + 1]);
                if (acc + len >= target) return Vector2.Lerp(RiverPath[i], RiverPath[i + 1], (target - acc) / len);
                acc += len;
            }
            return RiverPath[RiverPath.Length - 1];
        }

        /// <summary>溪床高度（t：0 = 源头，1 = 入海口）。</summary>
        public static float RiverBed(float t)
        {
            if (bedTable == null) return Mathf.Lerp(RiverSourceBed, -0.8f, Mathf.Pow(t, 0.8f));
            float f = Mathf.Clamp01(t) * (bedTable.Length - 1);
            int i = Mathf.Min((int)f, bedTable.Length - 2);
            return Mathf.Lerp(bedTable[i], bedTable[i + 1], f - i);
        }

        public static void RiverDistance(Vector2 p, out float dist, out float t)
        {
            dist = float.MaxValue;
            t = 0;
            float total = 0, acc = 0;
            for (int i = 0; i < RiverPath.Length - 1; i++) total += Vector2.Distance(RiverPath[i], RiverPath[i + 1]);
            for (int i = 0; i < RiverPath.Length - 1; i++)
            {
                Vector2 a = RiverPath[i], b = RiverPath[i + 1];
                float segLen = Vector2.Distance(a, b);
                float k = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / (segLen * segLen));
                float d = Vector2.Distance(p, a + (b - a) * k);
                if (d < dist)
                {
                    dist = d;
                    t = (acc + k * segLen) / total;
                }
                acc += segLen;
            }
        }

        /// <summary>在基础地形上叠加分区地貌，返回新的高度。coast：1 = 内陆，0 = 外海。</summary>
        public static float Apply(float x, float z, float h, float coast, float inlandBonus, bool carveRiver = true)
        {
            var info = At(x, z);

            // 北岸海崖：用陡峭的崖壁代替沙滩缓坡，崖顶是一片高台
            if (info.cliff > 0f)
            {
                float shelf = Mathf.Lerp(-16f, 8.5f, SmoothStep(0.29f, 0.38f, coast)) + SmoothStep(0.38f, 0.7f, coast) * 2f; // 矮海岸崖，不是高台
                float cliffH = shelf + inlandBonus;
                h = Mathf.Lerp(h, Mathf.Max(h, cliffH), info.cliff);
            }

            // 东岸沼泽：低平的泥滩，夹着浅水洼
            if (info.swamp > 0f)
            {
                float pools = (Fbm(x * 0.045f + 3f, z * 0.045f + 9f) - 0.5f) * 2.4f;
                float target = 0.5f + pools;
                float w = info.swamp * SmoothStep(0.22f, 0.36f, coast) * (1f - SmoothStep(0.7f, 0.88f, coast));
                h = Mathf.Lerp(h, target, w);
            }

            // 溪谷：沿溪流刻出 U 形河槽
            if (carveRiver && info.riverDist < RiverValleyWidth)
            {
                float bed = RiverBed(info.riverT);
                float profile = bed + Mathf.Pow(info.riverDist / RiverValleyWidth, 2f) * (RiverValleyWidth * 0.5f);
                float w = 1f - SmoothStep(RiverHalfWidth * 0.6f, RiverValleyWidth, info.riverDist);
                h = Mathf.Lerp(h, Mathf.Min(h, profile), w);
            }
            return h;
        }

        public static float SmoothStep(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3 - 2 * t);
        }

        static float Fbm(float x, float y)
        {
            float s = 0, a = 0.5f, norm = 0;
            for (int i = 0; i < 4; i++)
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
