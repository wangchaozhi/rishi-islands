using NUnit.Framework;
using Rishi.Core;
using Rishi.Creatures;
using UnityEngine;

namespace Rishi.Tests
{
    public class CreatureTests
    {
        [Test]
        public void ConeChecksRangeAndAngle()
        {
            Assert.IsTrue(Perception.InCone(Vector3.zero, Vector3.forward, new Vector3(0, 0, 10), 20f, 30f));
            Assert.IsFalse(Perception.InCone(Vector3.zero, Vector3.forward, new Vector3(0, 0, 25), 20f, 30f));
            Assert.IsFalse(Perception.InCone(Vector3.zero, Vector3.forward, new Vector3(10, 0, 2), 20f, 30f));
            Assert.IsFalse(Perception.InCone(Vector3.zero, Vector3.forward, new Vector3(0, 0, -5), 20f, 60f));
        }

        [Test]
        public void HearingScalesWithRadius()
        {
            Assert.IsTrue(Perception.CanHear(Vector3.zero, new Vector3(50, 0, 0), 70f, 1f));
            Assert.IsFalse(Perception.CanHear(Vector3.zero, new Vector3(20, 0, 0), 12f, 1f));
            Assert.IsTrue(Perception.CanHear(Vector3.zero, new Vector3(20, 0, 0), 12f, 2f));
        }

        [Test]
        public void TakesDamageAndDies()
        {
            var def = ScriptableObject.CreateInstance<CreatureDef>();
            def.maxHealth = 50f;
            var go = new GameObject("Creature");
            var c = go.AddComponent<Creature>();
            c.def = def;
            Assert.IsFalse(c.TakeDamage(20f));
            Assert.AreEqual(30f, c.Health, 1e-4f);
            bool died = false;
            c.Died += _ => died = true;
            Assert.IsTrue(c.TakeDamage(40f));
            Assert.IsTrue(c.Dead && died);
            Assert.IsFalse(c.TakeDamage(10f)); // 死了不会再受伤
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(def);
        }

        [Test]
        public void NoiseReachesListeners()
        {
            GameSignals.ResetAll();
            Vector3 heard = Vector3.zero;
            GameSignals.NoiseMade += (p, r) => heard = p;
            GameSignals.MakeNoise(new Vector3(1, 2, 3), 10f);
            Assert.AreEqual(new Vector3(1, 2, 3), heard);
            GameSignals.ResetAll();
        }
    }
}
