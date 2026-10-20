using NUnit.Framework;
using Rishi.Creatures;

namespace Rishi.Tests
{
    public class BeastTests
    {
        [Test]
        public void AnimalsOnlyAttackWhenCloseOrProvoked()
        {
            Assert.IsFalse(Creature.ShouldAggro(false, 15f, 9f));
            Assert.IsTrue(Creature.ShouldAggro(false, 6f, 9f));
            Assert.IsTrue(Creature.ShouldAggro(true, 30f, 9f));
        }

        [Test]
        public void EclipseFormAfterSunset()
        {
            Assert.IsFalse(Beast.IsNight(0.3f));
            Assert.IsFalse(Beast.IsNight(0f));
            Assert.IsTrue(Beast.IsNight(-0.2f));
        }

        [Test]
        public void BoarChargesFromMidRange()
        {
            Assert.IsFalse(BoarCharge.InChargeRange(2f, 5f, 14f));
            Assert.IsTrue(BoarCharge.InChargeRange(9f, 5f, 14f));
            Assert.IsFalse(BoarCharge.InChargeRange(20f, 5f, 14f));
        }
    }
}
