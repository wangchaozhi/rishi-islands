using UnityEngine;
using UnityEngine.InputSystem;
using Rishi.Audio;
using Rishi.Core;

namespace Rishi.Inventory
{
    /// <summary>
    /// 手电筒：背包里有手电筒时按 F 开关（咔嗒声）。亮着会耗电，电量低时光变暗、闪烁；
    /// 没电时自动换上背包里的电池，也可以在背包里「装进手电筒」。挂在玩家身上，光源挂在摄像机下。
    /// </summary>
    public class Flashlight : MonoBehaviour
    {
        public PlayerInventory inventory;
        public ItemDef flashlightItem;
        public ItemDef batteryItem;
        [Tooltip("摄像机下的聚光灯")]
        public Light beam;
        [Tooltip("左下角拿在手里的模型（武器层），亮着时显示")]
        public GameObject viewModel;

        [Header("电量")]
        [Tooltip("满电能亮多少分钟（现实时间）")]
        public float minutesPerBattery = 8f;
        [Range(0, 1)] public float charge = 1f;
        [Tooltip("电量低于这个比例时开始变暗、闪烁")]
        public float lowCharge = 0.15f;

        [Header("光")]
        public float intensity = 45f;
        public float range = 32f;

        public bool On { get; private set; }
        public bool Owned => inventory != null && flashlightItem != null && inventory.Has(flashlightItem);

        float flicker = 1f, flickerTimer;

        void OnEnable()
        {
            if (inventory != null) inventory.ItemUsed += OnItemUsed;
            SetOn(false, silent: true);
        }

        void OnDisable()
        {
            if (inventory != null) inventory.ItemUsed -= OnItemUsed;
            GameSignals.FlashlightOn = false;
            GameSignals.FlashlightCharge = -1f;
        }

        void Update()
        {
            bool owned = Owned;
            GameSignals.FlashlightCharge = owned ? charge : -1f;
            if (!owned) { if (On) SetOn(false, silent: true); return; }

            var kb = Keyboard.current;
            if (kb != null && kb.fKey.wasPressedThisFrame && !GameSignals.GameplayBlocked) Toggle();

            if (On)
            {
                charge = Drain(charge, Time.deltaTime, minutesPerBattery);
                if (charge <= 0f && !TryInsertBattery()) { SetOn(false, silent: true); GameSignals.Toast("手电筒没电了"); }
            }
            UpdateBeam();
        }

        /// <summary>按 F：没电时先试着换电池。</summary>
        public void Toggle()
        {
            if (!On && charge <= 0f && !TryInsertBattery())
            {
                AudioManager.Play2D("flashlight_click", 0.6f);
                GameSignals.Toast("手电筒没电了，找找电池");
                return;
            }
            SetOn(!On, silent: false);
        }

        public void SetOn(bool on, bool silent)
        {
            On = on;
            if (beam != null) beam.enabled = on;
            if (viewModel != null) viewModel.SetActive(on);
            GameSignals.FlashlightOn = on;
            GameSignals.Flashlight = on ? beam : null;
            if (!silent) AudioManager.Play2D("flashlight_click");
        }

        /// <summary>从背包里拿一节电池装上，成功时电量回满。</summary>
        public bool TryInsertBattery()
        {
            if (inventory == null || batteryItem == null || inventory.Remove(batteryItem, 1) == 0) return false;
            charge = 1f;
            AudioManager.Play2D("battery_insert");
            GameSignals.Toast("换上了一节新电池");
            return true;
        }

        void OnItemUsed(ItemDef item)
        {
            // 在背包里「装进手电筒」：电池已经被背包扣掉了，这里只把电量充满
            if (item != batteryItem) return;
            charge = 1f;
            AudioManager.Play2D("battery_insert");
        }

        /// <summary>亮着时每帧耗电；minutes 是满电能亮的分钟数。</summary>
        public static float Drain(float charge, float dt, float minutes) => Mathf.Max(0f, charge - dt / Mathf.Max(minutes * 60f, 1f));

        /// <summary>电量对亮度的影响：电量高时满亮，低于 low 后快速变暗（最暗保留 25%）。</summary>
        public static float Brightness(float charge, float low) => charge >= low ? 1f : Mathf.Lerp(0.25f, 1f, charge / Mathf.Max(low, 1e-3f));

        void UpdateBeam()
        {
            if (beam == null || !On) return;
            // 电量低时随机闪烁
            flickerTimer -= Time.deltaTime;
            if (charge < lowCharge && flickerTimer <= 0f)
            {
                flickerTimer = Random.Range(0.04f, 0.25f) + charge / lowCharge * Random.Range(0.3f, 1.5f);
                flicker = Random.value < 0.35f ? Random.Range(0.05f, 0.5f) : 1f;
            }
            else if (charge >= lowCharge) flicker = 1f;
            beam.intensity = intensity * Brightness(charge, lowCharge) * flicker;
            beam.range = range;
        }

        /// <summary>离线截图用：直接打开 / 关闭光（不检查背包、不出声）。</summary>
        public void PreviewOn(bool on)
        {
            if (beam != null) { beam.enabled = on; beam.intensity = intensity; beam.range = range; }
            if (viewModel != null) viewModel.SetActive(on);
        }
    }
}
