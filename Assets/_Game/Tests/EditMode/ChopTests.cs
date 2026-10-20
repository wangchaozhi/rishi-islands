using NUnit.Framework;
using Rishi.World;
using UnityEngine;

namespace Rishi.Tests
{
    public class ChopTests
    {
        [Test]
        public void ThicketFallsAfterEnoughBlowsAndStopsBlocking()
        {
            var go = new GameObject("Thicket");
            var col = go.AddComponent<BoxCollider>();
            var c = go.AddComponent<Choppable>();
            c.hits = 3;
            Assert.IsFalse(c.Chop(Vector3.zero, Vector3.forward));
            Assert.IsFalse(c.Chop(Vector3.zero, Vector3.forward));
            Assert.AreEqual(1, c.Remaining);
            Assert.IsTrue(c.Chop(Vector3.zero, Vector3.forward));
            Assert.IsTrue(c.Cut);
            Assert.IsFalse(col.enabled);
            Assert.IsFalse(c.Chop(Vector3.zero, Vector3.forward)); // 已经倒了
            Object.DestroyImmediate(go);
        }
    }
}
