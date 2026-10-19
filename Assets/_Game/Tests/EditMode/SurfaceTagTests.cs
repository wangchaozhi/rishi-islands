using NUnit.Framework;
using Rishi.Core;

namespace Rishi.Tests
{
    public class SurfaceTagTests
    {
        [Test]
        public void TerrainLayerTable_CoversAllIslandLayers()
        {
            // IslandBuilder 画了 10 个地形图层，脚步声表必须一一对应
            Assert.AreEqual(10, SurfaceTag.TerrainLayerSurfaces.Length);
            foreach (var s in SurfaceTag.TerrainLayerSurfaces) Assert.IsFalse(string.IsNullOrEmpty(s));
        }
    }
}
