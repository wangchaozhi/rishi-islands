using NUnit.Framework;
using UnityEngine;
using Rishi.Core;

namespace Rishi.Tests
{
    public class IslandsTests
    {
        GameObject a, b;

        [TearDown]
        public void TearDown()
        {
            if (a != null) Object.DestroyImmediate(a);
            if (b != null) Object.DestroyImmediate(b);
        }

        static GameObject MakeTerrain(string name, Vector3 origin, float height)
        {
            var data = new TerrainData { heightmapResolution = 33 };
            data.size = new Vector3(100f, 50f, 100f);
            var heights = new float[33, 33];
            for (int z = 0; z < 33; z++) for (int x = 0; x < 33; x++) heights[z, x] = height / 50f;
            data.SetHeights(0, 0, heights);
            var go = Terrain.CreateTerrainGameObject(data);
            go.name = name;
            go.transform.position = origin;
            return go;
        }

        [Test]
        public void TerrainAt_PicksTheIslandUnderThePoint_OrTheNearest()
        {
            a = MakeTerrain("Main", new Vector3(-50f, -20f, -50f), 25f);   // 地面 y = 5
            b = MakeTerrain("Atoll", new Vector3(500f, -20f, 0f), 18f);    // 地面 y = -2
            Assert.AreEqual(a.GetComponent<Terrain>(), Islands.TerrainAt(new Vector3(10f, 0f, 10f)));
            Assert.AreEqual(b.GetComponent<Terrain>(), Islands.TerrainAt(new Vector3(550f, 0f, 50f)));
            Assert.AreEqual(b.GetComponent<Terrain>(), Islands.TerrainAt(new Vector3(450f, 0f, 50f)), "两岛之间的海面上用较近的那座");
            Assert.AreEqual(5f, Islands.GroundY(new Vector3(0f, 0f, 0f)), 0.05f);
            Assert.AreEqual(-2f, Islands.GroundY(new Vector3(540f, 0f, 30f)), 0.05f);
        }
    }
}
