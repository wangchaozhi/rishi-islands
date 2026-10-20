using NUnit.Framework;
using Rishi.Story;
using UnityEngine;

namespace Rishi.Tests
{
    public class FirstNightTests
    {
        [Test]
        public void FootprintsAlternateAndSpanThePath()
        {
            var path = Footprints.Path(Vector3.zero, new Vector3(0, 0, 10), 0.7f, 0.11f, 1);
            Assert.AreEqual(15, path.Count);
            for (int i = 1; i < path.Count; i++) Assert.AreNotEqual(path[i - 1].left, path[i].left);
            Assert.Greater(path[path.Count - 1].pos.z, 9f);
            Assert.AreEqual(0f, path[0].yaw, 10f);
        }

        [Test]
        public void PeterVanishesWhenLitOrApproached()
        {
            Assert.IsTrue(Peter.ShouldVanish(true, 30f, 9f));
            Assert.IsTrue(Peter.ShouldVanish(false, 5f, 9f));
            Assert.IsFalse(Peter.ShouldVanish(false, 20f, 9f));
            Assert.IsTrue(Peter.InBeam(Vector3.zero, Vector3.forward, new Vector3(0, 0, 20), 32f, 28f));
            Assert.IsFalse(Peter.InBeam(Vector3.zero, Vector3.forward, new Vector3(0, 0, 40), 32f, 28f));
        }
    }
}
