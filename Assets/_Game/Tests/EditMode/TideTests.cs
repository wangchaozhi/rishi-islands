using NUnit.Framework;
using UnityEngine;
using Rishi.Core;
using Rishi.World;

namespace Rishi.Tests
{
    public class TideTests
    {
        static TideConfig Config()
        {
            var c = ScriptableObject.CreateInstance<TideConfig>();
            c.amplitude = 0.8f; c.periodHours = 12.42f; c.firstHighTideHour = 10.1f;
            return c;
        }

        [Test]
        public void SeaLevel_PeaksAtHighTide_AndBottomsHalfAPeriodLater()
        {
            var c = Config();
            Assert.AreEqual(0.8f, c.LevelAt(10.1f), 1e-3f);
            Assert.AreEqual(-0.8f, c.LevelAt(10.1f + 6.21f), 1e-3f);
            Assert.AreEqual(0.8f, c.LevelAt(10.1f + 12.42f * 3), 1e-3f, "三个周期后又是高潮");
            Object.DestroyImmediate(c);
        }

        [Test]
        public void MorningOfDayOne_IsRising_AfternoonIsFalling()
        {
            var c = Config();
            Assert.IsTrue(c.RisingAt(7f), "早上 7 点醒来时在涨潮");
            Assert.IsFalse(c.RisingAt(13f), "过了 10 点的高潮就开始退");
            Assert.IsTrue(c.RisingAt(18f), "傍晚低潮之后又涨");
            Object.DestroyImmediate(c);
        }

        [Test]
        public void Controller_WritesSeaLevelAndTide01()
        {
            var go = new GameObject("Tide");
            var dn = new GameObject("DayNight").AddComponent<DayNightCycle>();
            dn.timeOfDay = 10.1f;
            var tide = go.AddComponent<TideController>();
            tide.config = Config();
            tide.dayNight = dn;
            tide.Apply();
            Assert.AreEqual(0.8f, GameSignals.SeaLevel, 1e-3f);
            Assert.AreEqual(1f, GameSignals.Tide01, 1e-3f);
            Object.DestroyImmediate(tide.config);
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(dn.gameObject);
            GameSignals.ResetAll();
        }
    }
}
