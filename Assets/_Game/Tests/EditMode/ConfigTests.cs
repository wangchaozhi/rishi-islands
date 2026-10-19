using NUnit.Framework;
using Rishi.Audio;
using Rishi.World;
using UnityEngine;

namespace Rishi.Tests
{
    public class ConfigTests
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

        [Test]
        public void DayNightUsesConfigAsset()
        {
            var config = ScriptableObject.CreateInstance<DayNightConfig>();
            config.sunMaxIntensity = 5f;
            cycle.config = config;
            cycle.timeOfDay = 12f;
            cycle.Apply(false);
            Assert.AreEqual(5f, cycle.sun.intensity, 0.01f);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void DayNightFallsBackToDefaults()
        {
            var defaults = ScriptableObject.CreateInstance<DayNightConfig>();
            cycle.timeOfDay = 12f;
            cycle.Apply(false);
            Assert.AreEqual(defaults.sunMaxIntensity, cycle.sun.intensity, 0.01f);
            Object.DestroyImmediate(defaults);
        }

        [Test]
        public void SoundLibraryFindsById()
        {
            var lib = ScriptableObject.CreateInstance<SoundLibrary>();
            lib.sounds.Add(new SoundLibrary.Sound { id = "gun_shot", volume = 0.85f });
            Assert.AreEqual(0.85f, lib.Find("gun_shot").volume);
            Assert.IsNull(lib.Find("missing"));
            Assert.IsNull(lib.Find(null));
            Object.DestroyImmediate(lib);
        }

        [Test]
        public void StormIsNightWithLightning()
        {
            var storm = WeatherProfile.CreateStorm();
            Assert.IsTrue(storm.forceTimeOfDay >= 0f && (storm.forceTimeOfDay < 5f || storm.forceTimeOfDay > 20f));
            Assert.IsTrue(storm.lightning);
            Assert.Greater(storm.windBoost, 0f);
            Object.DestroyImmediate(storm);
        }
    }
}
