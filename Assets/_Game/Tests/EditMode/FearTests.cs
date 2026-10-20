using NUnit.Framework;
using Rishi.Player;

namespace Rishi.Tests
{
    public class FearTests
    {
        [Test]
        public void RisesInTheDark()
        {
            float f = 0f;
            for (int i = 0; i < 90; i++) f = PlayerFear.Step(f, 0f, 1f, 0.35f, 90f, 25f);
            Assert.AreEqual(1f, f, 1e-4f);
        }

        [Test]
        public void FallsInTheLight()
        {
            float f = 1f;
            for (int i = 0; i < 25; i++) f = PlayerFear.Step(f, 1f, 1f, 0.35f, 90f, 25f);
            Assert.AreEqual(0f, f, 1e-4f);
        }

        [Test]
        public void DimLightRisesSlowerThanPitchBlack()
        {
            float dim = PlayerFear.Step(0f, 0.25f, 1f, 0.35f, 90f, 25f);
            float black = PlayerFear.Step(0f, 0f, 1f, 0.35f, 90f, 25f);
            Assert.Greater(black, dim);
            Assert.Greater(dim, 0f);
        }

        [Test]
        public void FlashlightOrFireCountsAsLightAtNight()
        {
            Assert.Less(PlayerFear.EstimateLight(0f, 0f, 0f, false), 0.35f);
            Assert.GreaterOrEqual(PlayerFear.EstimateLight(0f, 0.75f, 0f, false), 0.35f);
            Assert.GreaterOrEqual(PlayerFear.EstimateLight(0f, 0f, 0.8f, false), 0.35f);
            Assert.Less(PlayerFear.EstimateLight(0.5f, 0f, 0f, true), PlayerFear.EstimateLight(0.5f, 0f, 0f, false));
        }
    }
}
