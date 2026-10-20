using System.Collections.Generic;
using UnityEngine;

namespace Rishi.Story
{
    /// <summary>脚印路线：从 a 走到 b，左右脚交替、略微蛇行。返回每一步的位置（xz）和朝向（偏航角）。</summary>
    public static class Footprints
    {
        public static List<(Vector3 pos, float yaw, bool left)> Path(Vector3 a, Vector3 b, float stride, float stance, int seed)
        {
            var list = new List<(Vector3, float, bool)>();
            var d = b - a; d.y = 0f;
            float len = d.magnitude;
            if (len < 1e-3f || stride <= 0f) return list;
            var dir = d / len;
            var side = Vector3.Cross(Vector3.up, dir);
            var rng = new System.Random(seed);
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            int n = Mathf.FloorToInt(len / stride);
            for (int i = 0; i <= n; i++)
            {
                bool left = i % 2 == 0;
                float wander = Mathf.Sin(i * 0.35f) * 0.25f + (float)(rng.NextDouble() - 0.5) * 0.06f;
                var p = a + dir * (i * stride) + side * ((left ? -stance : stance) + wander);
                list.Add((p, yaw + (left ? -6f : 6f) + (float)(rng.NextDouble() - 0.5) * 8f, left));
            }
            return list;
        }
    }
}
