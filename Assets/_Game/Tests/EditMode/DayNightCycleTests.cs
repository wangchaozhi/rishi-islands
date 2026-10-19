using NUnit.Framework;
using Rishi.World;
using UnityEngine;

namespace Rishi.Tests
{
    public class DayNightCycleTests
    {
        GameObject root;
        DayNightCycle cycle;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("DayNight");
            cycle = root.AddComponent<DayNightCycle>();
            cycle.sun = new GameObject("Sun").AddComponent<Light>();
            cycle.sun.type = LightType.Directional;
            cycle.sun.transform.SetParent(root.transform);
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(root);

        [TestCase(12f, true)]
        [TestCase(7f, true)]
        [TestCase(0f, false)]
        [TestCase(3f, false)]
        public void SunIsUpDuringDay(float hour, bool up)
        {
            cycle.timeOfDay = hour;
            cycle.Apply(false);
            Assert.AreEqual(up, cycle.SunElevation > 0f, $"{hour}:00 elevation {cycle.SunElevation}");
        }

        [Test]
        public void MoonVisibleAtMidnight()
        {
            cycle.timeOfDay = 0f;
            cycle.Apply(false);
            Vector4 moon = Shader.GetGlobalVector("_IslandMoonDir");
            Assert.Greater(moon.y, 0.3f);
        }
    }
}
