using NUnit.Framework;
using Rishi.Weather;
using Rishi.World;
using UnityEngine;

namespace Rishi.Tests
{
    public class WeatherTests
    {
        WeatherConfig config;

        [SetUp] public void SetUp() => config = WeatherSystem.CreateDefaultConfig();

        [TearDown]
        public void TearDown()
        {
            foreach (var e in config.weathers) Object.DestroyImmediate(e.profile);
            Object.DestroyImmediate(config);
        }

        int Index(string id) => config.weathers.FindIndex(e => e.profile.name == "Weather_" + id);

        [Test]
        public void FogIsMoreLikelyAtNight()
        {
            int fog = Index("Fog");
            float day = config.Weights(false, 0)[fog] / Sum(config.Weights(false, 0));
            float night = config.Weights(true, 0)[fog] / Sum(config.Weights(true, 0));
            Assert.Greater(night, day);
        }

        [Test]
        public void FragmentsMakeBadWeatherMoreLikely()
        {
            int storm = Index("Thunderstorm"), clear = Index("Clear");
            var w0 = config.Weights(false, 0);
            var w3 = config.Weights(false, 3);
            Assert.Greater(w3[storm] / Sum(w3), w0[storm] / Sum(w0));
            Assert.Less(w3[clear] / Sum(w3), w0[clear] / Sum(w0));
        }

        [Test]
        public void PickNeverRepeatsCurrent()
        {
            var current = config.weathers[Index("Rain")].profile;
            for (int i = 0; i <= 20; i++)
                Assert.AreNotSame(current, config.Pick(i % 2 == 0, i % 4, current, i / 20f * 0.999f));
        }

        [Test]
        public void BlendHitsEndpoints()
        {
            var a = WeatherSystem.Values.From(config.weathers[Index("Clear")].profile);
            var b = WeatherSystem.Values.From(config.weathers[Index("Thunderstorm")].profile);
            Assert.AreEqual(a.rain, WeatherSystem.Values.Lerp(a, b, 0f).rain, 1e-5f);
            Assert.AreEqual(b.rain, WeatherSystem.Values.Lerp(a, b, 1f).rain, 1e-5f);
            Assert.IsTrue(WeatherSystem.Values.Lerp(a, b, 1f).lightning);
            Assert.IsFalse(WeatherSystem.Values.Lerp(a, b, 0.2f).lightning);
        }

        static float Sum(float[] w) { float s = 0; foreach (var x in w) s += x; return s; }
    }
}
