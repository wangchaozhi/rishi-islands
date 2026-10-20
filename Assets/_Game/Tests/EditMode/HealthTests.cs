using NUnit.Framework;
using Rishi.Core;
using Rishi.Player;
using UnityEngine;

namespace Rishi.Tests
{
    public class HealthTests
    {
        GameObject go;
        PlayerHealth health;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            go = new GameObject("Player");
            go.AddComponent<CharacterController>();
            go.AddComponent<PlayerController>();
            health = go.AddComponent<PlayerHealth>();
        }

        [TearDown]
        public void TearDown() { Object.DestroyImmediate(go); GameSignals.ResetAll(); }

        [Test]
        public void DamageLowersHealthAndFlashes()
        {
            health.TakeDamage(30f);
            Assert.AreEqual(0.7f, GameSignals.Health, 1e-4f);
            Assert.Greater(GameSignals.DamageFlash, 0f);
        }

        [Test]
        public void LethalDamageKills()
        {
            Assert.IsFalse(health.TakeDamage(60f));
            Assert.IsTrue(health.TakeDamage(60f));
            Assert.IsTrue(health.Dead);
        }

        [Test]
        public void HealIsClamped()
        {
            health.TakeDamage(10f);
            health.Heal(50f);
            Assert.AreEqual(1f, GameSignals.Health, 1e-4f);
        }

        [Test]
        public void RespawnsAtNearestLitCampfire()
        {
            var a = new GameObject("A").AddComponent<Campfire>();
            var b = new GameObject("B").AddComponent<Campfire>();
            var c = new GameObject("C").AddComponent<Campfire>();
            a.transform.position = new Vector3(5, 0, 0);
            b.transform.position = new Vector3(20, 0, 0);
            c.transform.position = new Vector3(1, 0, 0);
            a.lit = b.lit = true; // c 没点
            var fires = new[] { a, b, c };
            Assert.AreSame(a, Campfire.NearestLit(Vector3.zero, fires));
            Assert.AreSame(b, Campfire.NearestLit(new Vector3(30, 0, 0), fires));
            a.lit = b.lit = false;
            Assert.IsNull(Campfire.NearestLit(Vector3.zero, fires));
            Object.DestroyImmediate(a.gameObject); Object.DestroyImmediate(b.gameObject); Object.DestroyImmediate(c.gameObject);
        }
    }
}
