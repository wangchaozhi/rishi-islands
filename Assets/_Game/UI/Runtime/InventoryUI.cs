using UnityEngine;
using UnityEngine.InputSystem;
using Rishi.Audio;
using Rishi.Core;
using Rishi.Inventory;
using Rishi.Weapons;

namespace Rishi.UI
{
    /// <summary>
    /// 背包界面（Tab 开关）：6×4 格子，分类筛选，点击选中查看说明，使用 / 装备 / 丢弃，拖动整理。
    /// 打开时暂停玩家操作并释放鼠标。
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        public PlayerInventory inventory;
        public WeaponController weapons;
        public int columns = 6;

        public bool IsOpen { get; private set; }

        int selected = -1;
        int dragFrom = -1;
        int filter = -1; // -1 = 全部
        GUIStyle titleStyle, nameStyle, bodyStyle, countStyle, glyphStyle, tabStyle, buttonStyle, hintStyle;

        static readonly (int cat, string name)[] Tabs =
        {
            (-1, "全部"), ((int)ItemCategory.Weapon, "武器"), ((int)ItemCategory.Ammo, "弹药"),
            ((int)ItemCategory.Consumable, "消耗品"), ((int)ItemCategory.Treasure, "收藏"), ((int)ItemCategory.Quest, "任务"),
        };

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || inventory == null) return;
            bool otherUi = GameSignals.InputBlocked && !IsOpen;
            if (GameSignals.CutsceneActive || otherUi) { if (IsOpen) SetOpen(false); return; }
            if (kb.tabKey.wasPressedThisFrame || kb.iKey.wasPressedThisFrame) SetOpen(!IsOpen);
            else if (IsOpen && kb.escapeKey.wasPressedThisFrame) SetOpen(false);
        }

        void SetOpen(bool open)
        {
            IsOpen = open;
            GameSignals.SetInputBlock(this, open);
            AudioManager.Play2D(open ? "bag_open" : "bag_close", 0.7f);
            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;
            dragFrom = -1;
        }

        void OnDisable() => GameSignals.SetInputBlock(this, false);

        void OnGUI()
        {
            if (!IsOpen || inventory == null || inventory.Slots == null) return;
            InitStyles();
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(0.02f, 0.03f, 0.04f, 0.72f), 0, 0);

            const float cell = 78, gap = 8, panelW = 340;
            int rows = Mathf.CeilToInt(inventory.Slots.Length / (float)columns);
            float gridW = columns * cell + (columns - 1) * gap, gridH = rows * cell + (rows - 1) * gap;
            float totalW = gridW + 30 + panelW;
            float x0 = (Screen.width - totalW) / 2f, y0 = (Screen.height - gridH) / 2f + 20;

            GUI.Label(new Rect(x0, y0 - 92, 400, 40), "背包", titleStyle);
            // 分类页签
            for (int t = 0; t < Tabs.Length; t++)
            {
                var r = new Rect(x0 + t * 74, y0 - 44, 68, 30);
                bool on = filter == Tabs[t].cat;
                GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, on ? new Color(0.95f, 0.75f, 0.35f, 0.5f) : new Color(1, 1, 1, 0.08f), 0, 6);
                if (GUI.Button(r, Tabs[t].name, tabStyle)) filter = Tabs[t].cat;
            }

            var e = Event.current;
            for (int i = 0; i < inventory.Slots.Length; i++)
            {
                int cx = i % columns, cy = i / columns;
                var r = new Rect(x0 + cx * (cell + gap), y0 + cy * (cell + gap), cell, cell);
                var s = inventory.Slots[i];
                bool visible = s.IsEmpty || filter < 0 || (int)s.item.category == filter;
                bool isSel = i == selected;
                GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, isSel ? new Color(0.95f, 0.75f, 0.35f, 0.45f) : new Color(1, 1, 1, 0.07f), 0, 8);
                if (!s.IsEmpty && visible) DrawIcon(new Rect(r.x + 7, r.y + 7, r.width - 14, r.height - 14), s.item, s.count, i == dragFrom ? 0.35f : 1f);
                else if (!s.IsEmpty)
                    GUI.DrawTexture(new Rect(r.x + 7, r.y + 7, r.width - 14, r.height - 14), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(1, 1, 1, 0.04f), 0, 6);

                if (!r.Contains(e.mousePosition)) continue;
                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    selected = s.IsEmpty ? -1 : i;
                    dragFrom = s.IsEmpty ? -1 : i;
                    if (!s.IsEmpty) AudioManager.Play2D("ui_click", 0.5f);
                    e.Use();
                }
                else if (e.type == EventType.MouseUp && e.button == 0 && dragFrom >= 0)
                {
                    if (dragFrom != i) { inventory.Swap(dragFrom, i); selected = i; }
                    dragFrom = -1;
                    e.Use();
                }
                else if (e.type == EventType.MouseDown && e.button == 1 && !s.IsEmpty)
                {
                    selected = i;
                    PrimaryAction(i);
                    e.Use();
                }
            }
            if (e.type == EventType.MouseUp) dragFrom = -1;
            if (dragFrom >= 0 && !inventory.Slots[dragFrom].IsEmpty)
                DrawIcon(new Rect(e.mousePosition.x - 30, e.mousePosition.y - 30, 60, 60), inventory.Slots[dragFrom].item, inventory.Slots[dragFrom].count, 0.85f);

            DrawDetails(new Rect(x0 + gridW + 30, y0, panelW, gridH));
            GUI.Label(new Rect(x0, y0 + gridH + 14, totalW, 24), "左键选中 / 拖动整理    右键快速使用或装备    Tab / Esc 关闭", hintStyle);
        }

        void DrawDetails(Rect r)
        {
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(1, 1, 1, 0.06f), 0, 10);
            if (selected < 0 || inventory.Slots[selected].IsEmpty)
            {
                GUI.Label(new Rect(r.x + 20, r.y + 20, r.width - 40, 60), "选择一个物品查看说明", bodyStyle);
                return;
            }
            var s = inventory.Slots[selected];
            DrawIcon(new Rect(r.x + 20, r.y + 20, 96, 96), s.item, s.count, 1f);
            GUI.Label(new Rect(r.x + 130, r.y + 26, r.width - 150, 34), s.item.displayName, nameStyle);
            GUI.Label(new Rect(r.x + 130, r.y + 62, r.width - 150, 24), ItemDef.CategoryName(s.item.category) + (s.item.maxStack > 1 ? $"   {s.count} / {s.item.maxStack}" : ""), hintStyle);
            GUI.Label(new Rect(r.x + 20, r.y + 132, r.width - 40, r.height - 210), s.item.description, bodyStyle);

            float by = r.yMax - 56, bx = r.x + 20;
            string primary = PrimaryLabel(s.item);
            if (primary != null)
            {
                if (Button(new Rect(bx, by, 140, 38), primary)) PrimaryAction(selected);
                bx += 152;
            }
            if (s.item.category != ItemCategory.Quest && Button(new Rect(bx, by, 110, 38), "丢弃"))
            {
                inventory.Drop(selected);
                selected = -1;
            }
        }

        string PrimaryLabel(ItemDef item)
        {
            if (item.category == ItemCategory.Weapon) return WeaponIndex(item) >= 0 ? (WeaponIndex(item) == weapons.CurrentIndex ? "已装备" : "装备") : null;
            return item.usable ? item.useVerb : null;
        }

        void PrimaryAction(int slot)
        {
            var s = inventory.Slots[slot];
            if (s.IsEmpty) return;
            if (s.item.category == ItemCategory.Weapon)
            {
                int w = WeaponIndex(s.item);
                if (w >= 0) weapons.Equip(w);
            }
            else if (s.item.usable)
            {
                inventory.Use(slot);
                if (inventory.Slots[slot].IsEmpty) selected = -1;
            }
        }

        int WeaponIndex(ItemDef item)
        {
            if (weapons == null) return -1;
            for (int i = 0; i < weapons.slots.Count; i++)
                if (weapons.slots[i].def != null && weapons.slots[i].def.item == item) return i;
            return -1;
        }

        bool Button(Rect r, string text)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, hover ? new Color(0.95f, 0.75f, 0.35f, 0.7f) : new Color(1, 1, 1, 0.15f), 0, 6);
            bool clicked = GUI.Button(r, text, buttonStyle);
            if (clicked) AudioManager.Play2D("ui_click", 0.6f);
            return clicked;
        }

        void DrawIcon(Rect r, ItemDef item, int count, float alpha)
        {
            if (item.icon != null)
                GUI.DrawTexture(r, item.icon, ScaleMode.ScaleToFit, true, 0, new Color(1, 1, 1, alpha), 0, 0);
            else
            {
                var c = item.iconColor;
                GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(c.r, c.g, c.b, 0.85f * alpha), 0, 8);
                var old = GUI.color;
                GUI.color = new Color(1, 1, 1, alpha);
                glyphStyle.fontSize = Mathf.RoundToInt(r.height * 0.45f);
                GUI.Label(r, item.iconGlyph, glyphStyle);
                GUI.color = old;
            }
            if (count > 1) GUI.Label(new Rect(r.x, r.y, r.width - 4, r.height - 2), count.ToString(), countStyle);
        }

        void InitStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold };
            nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
            bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
            bodyStyle.normal.textColor = new Color(0.9f, 0.9f, 0.88f);
            countStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerRight };
            glyphStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            tabStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
            buttonStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            hintStyle.normal.textColor = new Color(1, 1, 1, 0.6f);
        }
    }
}
