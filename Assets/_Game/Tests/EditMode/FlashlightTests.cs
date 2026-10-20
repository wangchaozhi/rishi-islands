using NUnit.Framework;
using Rishi.Inventory;
using UnityEngine;

namespace Rishi.Tests
{
    public class FlashlightTests
    {
        GameObject go;
        PlayerInventory inventory;
        Flashlight light;
        ItemDef torch, battery;

        [SetUp]
        public void SetUp()
        {
            torch = ScriptableObject.CreateInstance<ItemDef>();
            torch.id = "flashlight";
            battery = ScriptableObject.CreateInstance<ItemDef>();
            battery.id = "battery";
            battery.maxStack = 8;
            battery.usable = true;
            go = new GameObject("Player");
            inventory = go.AddComponent<PlayerInventory>();
            inventory.Init();
            light = go.AddComponent<Flashlight>();
            light.inventory = inventory;
            light.flashlightItem = torch;
            light.batteryItem = battery;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(torch);
            Object.DestroyImmediate(battery);
        }

        [Test]
        public void FullChargeLastsConfiguredMinutes()
        {
            float c = 1f;
            for (int i = 0; i < 8 * 60; i++) c = Flashlight.Drain(c, 1f, 8f);
            Assert.AreEqual(0f, c, 1e-4f);
            Assert.Greater(Flashlight.Drain(1f, 60f, 8f), 0.8f);
        }

        [Test]
        public void DimsOnlyWhenLow()
        {
            Assert.AreEqual(1f, Flashlight.Brightness(0.5f, 0.15f));
            Assert.Less(Flashlight.Brightness(0.05f, 0.15f), 1f);
            Assert.GreaterOrEqual(Flashlight.Brightness(0f, 0.15f), 0.25f);
        }

        [Test]
        public void OwnedOnlyAfterPickingUpTheFlashlight()
        {
            Assert.IsFalse(light.Owned);
            inventory.Add(torch, 1, silent: true);
            Assert.IsTrue(light.Owned);
        }

        [Test]
        public void InsertingBatteryUsesOneAndRecharges()
        {
            light.charge = 0f;
            Assert.IsFalse(light.TryInsertBattery());
            inventory.Add(battery, 2, silent: true);
            Assert.IsTrue(light.TryInsertBattery());
            Assert.AreEqual(1f, light.charge);
            Assert.AreEqual(1, inventory.Count(battery));
        }
    }
}
