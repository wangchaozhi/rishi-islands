using NUnit.Framework;
using Rishi.Core;
using UnityEngine;

namespace Rishi.Tests
{
    public class GameSignalsTests
    {
        [SetUp] public void SetUp() => GameSignals.ResetAll();
        [TearDown] public void TearDown() => GameSignals.ResetAll();

        [Test]
        public void GameplayBlocked_WhenReadingOrCutscene()
        {
            Assert.IsFalse(GameSignals.GameplayBlocked);
            GameSignals.InputBlocked = true;
            Assert.IsTrue(GameSignals.GameplayBlocked);
            GameSignals.InputBlocked = false;
            GameSignals.CutsceneActive = true;
            Assert.IsTrue(GameSignals.GameplayBlocked);
        }

        [Test]
        public void SetFragments_RaisesEventOnlyOnChange()
        {
            int calls = 0, last = -1;
            GameSignals.FragmentsChanged += n => { calls++; last = n; };
            GameSignals.SetFragments(1);
            GameSignals.SetFragments(1);
            Assert.AreEqual(1, calls);
            Assert.AreEqual(1, last);
            Assert.AreEqual(1, GameSignals.Fragments);
        }

        [Test]
        public void IsPlayer_MatchesPlayerChildrenOnly()
        {
            var player = new GameObject("Player");
            var child = new GameObject("Hitbox");
            child.transform.SetParent(player.transform);
            var other = new GameObject("Crab");
            try
            {
                Assert.IsFalse(GameSignals.IsPlayer(child.transform));
                GameSignals.RegisterPlayer(player.transform);
                Assert.IsTrue(GameSignals.IsPlayer(child.transform));
                Assert.IsFalse(GameSignals.IsPlayer(other.transform));
                GameSignals.UnregisterPlayer(player.transform);
                Assert.IsNull(GameSignals.Player);
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(other);
            }
        }
    }
}
