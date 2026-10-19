using UnityEngine;

namespace Rishi.Core
{
    /// <summary>
    /// 标记碰撞体的地表类型（stone / wood / metal / sand / grass / leaves / water），给脚步声和命中音效用。
    /// 挂在父物体上即可作用于所有子碰撞体。地形不需要挂，按地表图层自动判断。
    /// </summary>
    public class SurfaceTag : MonoBehaviour
    {
        public string surface = "stone";

        /// <summary>地形图层顺序对应的地表（与 IslandBuilder 的图层顺序一致）。</summary>
        public static readonly string[] TerrainLayerSurfaces = { "wetsand", "sand", "grass", "leaves", "stone", "wetsand", "stone", "stone", "stone", "stone" };

        public static string At(RaycastHit hit)
        {
            var tag = hit.collider.GetComponentInParent<SurfaceTag>();
            if (tag != null) return tag.surface;
            if (hit.collider is TerrainCollider tc) return TerrainSurface(tc.GetComponent<Terrain>(), hit.point);
            return "stone";
        }

        public static string TerrainSurface(Terrain terrain, Vector3 world)
        {
            var data = terrain.terrainData;
            Vector3 local = world - terrain.transform.position;
            int ar = data.alphamapResolution;
            int x = Mathf.Clamp((int)(local.x / data.size.x * (ar - 1)), 0, ar - 1);
            int z = Mathf.Clamp((int)(local.z / data.size.z * (ar - 1)), 0, ar - 1);
            var a = data.GetAlphamaps(x, z, 1, 1);
            int best = 0;
            for (int l = 1; l < a.GetLength(2); l++)
                if (a[0, 0, l] > a[0, 0, best]) best = l;
            return best < TerrainLayerSurfaces.Length ? TerrainLayerSurfaces[best] : "stone";
        }

        /// <summary>命中音效 id。</summary>
        public static string ImpactSound(string surface) => surface switch
        {
            "metal" => "impact_metal",
            "wood" => "impact_wood",
            "stone" => "impact_stone",
            _ => "impact_dirt",
        };
    }
}
