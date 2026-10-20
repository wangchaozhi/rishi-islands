using NUnit.Framework;
using Rishi.World;
using UnityEngine;

namespace Rishi.Tests
{
    public class NightLengthTests
    {
        DayNightConfig c;
        [SetUp] public void SetUp() => c = ScriptableObject.CreateInstance<DayNightConfig>();
        [TearDown] public void TearDown() => Object.DestroyImmediate(c);

        [Test]
        public void NightsGetLongerWithFragments()
        {
            Assert.AreEqual(1f, c.NightSpeed(0), 1e-5f);
            Assert.Less(c.NightSpeed(1), c.NightSpeed(0));
            Assert.Less(c.NightSpeed(5), c.NightSpeed(1));
            // 7 块碎片时，夜晚在现实时间里大约是一开始的 3.45 倍长
            Assert.AreEqual(1f / 3.45f, c.NightSpeed(7), 1e-3f);
        }

        [Test]
        public void FogThickensMostAtNight()
        {
            Assert.AreEqual(1f, c.FragmentFog(0, 1f), 1e-5f);
            Assert.Greater(c.FragmentFog(3, 0f), 1f);
            Assert.Greater(c.FragmentFog(3, 1f), c.FragmentFog(3, 0f));
        }
    }
}
