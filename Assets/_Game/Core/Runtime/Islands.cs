using UnityEngine;

namespace Rishi.Core
{
    /// <summary>
    /// 场景里有不止一座岛（每座岛一块地形）。按位置找对应的地形：在哪块地形的范围里就用哪块，都不在就用最近的。
    /// 运行时代码不要用 Terrain.activeTerrain（只会返回其中一块）。
    /// </summary>
    public static class Islands
    {
        public static Terrain TerrainAt(Vector3 p)
        {
            Terrain best = null;
            float bestD = float.MaxValue;
            foreach (var t in Terrain.activeTerrains)
            {
                if (t == null || t.terrainData == null) continue;
                var o = t.transform.position;
                var s = t.terrainData.size;
                float dx = Mathf.Max(o.x - p.x, 0f, p.x - (o.x + s.x));
                float dz = Mathf.Max(o.z - p.z, 0f, p.z - (o.z + s.z));
                float d = dx * dx + dz * dz;
                if (d < bestD) { bestD = d; best = t; }
            }
            return best;
        }

        /// <summary>这个位置的地面高度（世界坐标 y）；场景里没有地形时返回 fallback。</summary>
        public static float GroundY(Vector3 p, float fallback = -40f)
        {
            var t = TerrainAt(p);
            return t != null ? t.SampleHeight(p) + t.transform.position.y : fallback;
        }
    }
}
