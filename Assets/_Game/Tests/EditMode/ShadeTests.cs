using NUnit.Framework;
using Rishi.Creatures;
using UnityEngine;

namespace Rishi.Tests
{
    public class ShadeTests
    {
        [Test]
        public void MoreShadesWithEachFragment()
        {
            Assert.AreEqual(1, ShadeSpawner.MaxShades(0));
            Assert.AreEqual(2, ShadeSpawner.MaxShades(1));
            Assert.AreEqual(4, ShadeSpawner.MaxShades(5));
        }

        [Test]
        public void WatchedOnlyWhenInView()
        {
            Assert.IsTrue(Shade.Watched(Vector3.zero, Vector3.forward, new Vector3(0, 0, 30), 36f));
            Assert.IsFalse(Shade.Watched(Vector3.zero, Vector3.forward, new Vector3(0, 0, -30), 36f));
            Assert.IsFalse(Shade.Watched(Vector3.zero, Vector3.forward, new Vector3(30, 0, 5), 36f));
        }
    }
}
