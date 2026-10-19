using System;
using System.Collections.Generic;
using UnityEngine;
using Rishi.Audio;
using Rishi.Core;

namespace Rishi.Inventory
{
    /// <summary>玩家背包：固定格数，同类物品自动堆叠。挂在玩家身上。</summary>
    public class PlayerInventory : MonoBehaviour
    {
        [Serializable]
        public struct Stack
        {
            public ItemDef item;
            public int count;
            public bool IsEmpty => item == null || count <= 0;
        }

        public static PlayerInventory Instance { get; private set; }

        [Min(1)] public int capacity = 24;
        [Tooltip("开局自带的物品")]
        public List<Stack> startItems = new List<Stack>();

        public Stack[] Slots { get; private set; }

        /// <summary>背包内容变化（加、减、移动）。</summary>
        public event Action Changed;
        /// <summary>使用了消耗品（以后由生命 / 饥饿系统接收）。</summary>
        public event Action<ItemDef> ItemUsed;

        void Awake()
        {
            Instance = this;
            Init();
        }

        /// <summary>按容量清空并放入开局物品（Awake 自动调用，测试里也可以手动调）。</summary>
        public void Init()
        {
            Slots = new Stack[capacity];
            foreach (var s in startItems)
                if (!s.IsEmpty) Add(s.item, s.count, silent: true);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>放进背包，返回实际放进去的数量（满了会少于 count）。</summary>
        public int Add(ItemDef item, int count, bool silent = false)
        {
            if (item == null || count <= 0) return 0;
            if (Slots == null) Init();
            int left = count;
            for (int i = 0; i < Slots.Length && left > 0; i++)
            {
                if (Slots[i].item != item || Slots[i].count >= item.maxStack) continue;
                int n = Mathf.Min(left, item.maxStack - Slots[i].count);
                Slots[i].count += n;
                left -= n;
            }
            for (int i = 0; i < Slots.Length && left > 0; i++)
            {
                if (!Slots[i].IsEmpty) continue;
                int n = Mathf.Min(left, item.maxStack);
                Slots[i] = new Stack { item = item, count = n };
                left -= n;
            }
            int added = count - left;
            if (added > 0)
            {
                Changed?.Invoke();
                if (!silent)
                {
                    AudioManager.Play2D(string.IsNullOrEmpty(item.pickupSound) ? "item_pickup" : item.pickupSound);
                    GameSignals.Toast(added > 1 ? $"获得 {item.displayName} ×{added}" : $"获得 {item.displayName}");
                }
            }
            if (left > 0 && !silent) GameSignals.Toast("背包满了");
            return added;
        }

        public int Count(ItemDef item)
        {
            if (item == null || Slots == null) return 0;
            int n = 0;
            foreach (var s in Slots) if (s.item == item) n += s.count;
            return n;
        }

        public bool Has(ItemDef item) => Count(item) > 0;

        /// <summary>取走最多 count 个，返回实际取走的数量。优先从数量少的格子取。</summary>
        public int Remove(ItemDef item, int count)
        {
            if (item == null || count <= 0 || Slots == null) return 0;
            int left = count;
            while (left > 0)
            {
                int best = -1;
                for (int i = 0; i < Slots.Length; i++)
                    if (Slots[i].item == item && (best < 0 || Slots[i].count < Slots[best].count)) best = i;
                if (best < 0) break;
                int n = Mathf.Min(left, Slots[best].count);
                Slots[best].count -= n;
                if (Slots[best].count <= 0) Slots[best] = default;
                left -= n;
            }
            if (left != count) Changed?.Invoke();
            return count - left;
        }

        /// <summary>使用一个消耗品。</summary>
        public bool Use(int slot)
        {
            var s = Slots[slot];
            if (s.IsEmpty || !s.item.usable) return false;
            Slots[slot].count--;
            if (Slots[slot].count <= 0) Slots[slot] = default;
            AudioManager.Play2D("item_use");
            if (!string.IsNullOrEmpty(s.item.useMessage)) GameSignals.Toast(s.item.useMessage);
            Changed?.Invoke();
            ItemUsed?.Invoke(s.item);
            return true;
        }

        /// <summary>把一格扔在玩家面前。任务物品不能丢。</summary>
        public void Drop(int slot)
        {
            var s = Slots[slot];
            if (s.IsEmpty || s.item.category == ItemCategory.Quest) return;
            Slots[slot] = default;
            var cam = Camera.main != null ? Camera.main.transform : transform;
            Vector3 pos = cam.position + Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized * 1.2f;
            if (Physics.Raycast(pos + Vector3.up, Vector3.down, out var hit, 6f, ~0, QueryTriggerInteraction.Ignore)) pos = hit.point + Vector3.up * 0.08f;
            ItemPickup.Spawn(s.item, s.count, pos);
            AudioManager.Play2D("item_drop");
            Changed?.Invoke();
        }

        /// <summary>交换两格（拖拽整理）。</summary>
        public void Swap(int a, int b)
        {
            if (a == b) return;
            (Slots[a], Slots[b]) = (Slots[b], Slots[a]);
            Changed?.Invoke();
        }
    }
}
