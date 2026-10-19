using UnityEngine;

namespace Rishi.Inventory
{
    public enum ItemCategory { Weapon, Ammo, Consumable, Treasure, Material, Quest }

    /// <summary>物品配置：在 Inspector 里直接调名称、说明、堆叠和图标。资产放在 Assets/_Game/Inventory/Data/Items。</summary>
    [CreateAssetMenu(menuName = "日噬群岛/物品", fileName = "Item")]
    public class ItemDef : ScriptableObject
    {
        [Tooltip("存档和代码里用的唯一 id")]
        public string id;
        public string displayName;
        [TextArea(2, 5)] public string description;
        public ItemCategory category;
        [Min(1)] public int maxStack = 1;

        [Header("图标")]
        public Texture2D icon;
        [Tooltip("没有图标贴图时，用底色 + 一个字代替")]
        public Color iconColor = new Color(0.4f, 0.4f, 0.4f);
        public string iconGlyph = "?";

        [Header("使用")]
        public bool usable;
        public string useVerb = "使用";
        [TextArea(1, 3)] public string useMessage;

        [Header("声音")]
        public string pickupSound = "item_pickup";

        public static string CategoryName(ItemCategory c) => c switch
        {
            ItemCategory.Weapon => "武器",
            ItemCategory.Ammo => "弹药",
            ItemCategory.Consumable => "消耗品",
            ItemCategory.Treasure => "收藏",
            ItemCategory.Material => "材料",
            _ => "任务",
        };
    }
}
