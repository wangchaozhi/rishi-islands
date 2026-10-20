using NUnit.Framework;
using UnityEngine;
using Rishi.Vehicles;

namespace Rishi.Tests
{
    public class BoatTests
    {
        [Test]
        public void Hull_FloatsAtDraft_OrRestsOnGround()
        {
            Assert.AreEqual(-0.22f, Boat.HullHeight(0f, -3f, 0.22f), 1e-4f, "深水里浮着，船底在水面下吃水深度处");
            Assert.AreEqual(0.4f, Boat.HullHeight(0f, 0.4f, 0.22f), 1e-4f, "沙滩上搁着");
        }

        [Test]
        public void Runs_Aground_OnlyWhenGroundAheadRisesAboveWaterline()
        {
            Assert.IsFalse(Boat.Blocked(-2f, -3f, 0f, 0.22f), "深水往前开");
            Assert.IsTrue(Boat.Blocked(0.3f, -0.5f, 0f, 0.22f), "前面是沙滩，冲上岸搁浅");
            Assert.IsFalse(Boat.Blocked(-0.05f, 0.1f, 0f, 0.22f), "搁在沙滩上往水里推可以");
        }

        [Test]
        public void Repair_ListsMissingMaterials()
        {
            Assert.AreEqual("木板 ×2、绳子 ×1", Boat.Missing(1, 3, 0, 1));
            Assert.AreEqual("绳子 ×1", Boat.Missing(3, 3, 0, 1));
            Assert.AreEqual("", Boat.Missing(4, 3, 1, 1));
        }

        [Test]
        public void SetRepaired_SwapsBrokenAndRepairedParts()
        {
            var go = new GameObject("Boat");
            var broken = new GameObject("Broken"); broken.transform.SetParent(go.transform);
            var fixedParts = new GameObject("Repaired"); fixedParts.transform.SetParent(go.transform);
            var boat = go.AddComponent<Boat>();
            boat.brokenParts = broken;
            boat.repairedParts = fixedParts;
            boat.SetRepaired(false);
            Assert.IsTrue(broken.activeSelf); Assert.IsFalse(fixedParts.activeSelf);
            boat.SetRepaired(true);
            Assert.AreEqual("上船", boat.Prompt);
            Assert.IsFalse(broken.activeSelf); Assert.IsTrue(fixedParts.activeSelf);
            Object.DestroyImmediate(go);
        }
    }
}
