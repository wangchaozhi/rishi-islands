using UnityEngine;
using Rishi.Inventory;

namespace Rishi.Weapons
{
    public enum WeaponKind { Hitscan, Melee, Projectile }

    /// <summary>武器配置：伤害、射速、弹匣、所需弹药、能否水下使用。资产放在 Assets/_Game/Weapons/Data。</summary>
    [CreateAssetMenu(menuName = "日噬群岛/武器", fileName = "Weapon")]
    public class WeaponDef : ScriptableObject
    {
        public string id;
        public string displayName;
        public WeaponKind kind;
        [Tooltip("背包里有这个物品才能装备")]
        public ItemDef item;
        [Tooltip("备用弹药（背包里的物品）；近战武器留空")]
        public ItemDef ammo;

        [Header("伤害")]
        public float damage = 34f;
        [Tooltip("每秒最多攻击次数")]
        public float fireRate = 6f;
        public bool automatic;
        public float range = 150f;
        public float impactForce = 6f;

        [Header("弹匣")]
        [Min(0)] public int magazineSize = 12;
        public float reloadTime = 1.4f;

        [Header("精度与后坐")]
        public float spread = 0.6f;
        public float recoilUp = 1.6f;
        public float recoilSide = 0.5f;

        [Header("环境")]
        public bool usableUnderwater;
        public bool usableOnLand = true;

        [Header("投射物")]
        public float projectileSpeed = 38f;
        public float projectileSpeedUnderwater = 22f;

        [Header("近战")]
        public float meleeRadius = 0.35f;
        [Tooltip("挥砍开始到判定命中的时间")]
        public float meleeHitDelay = 0.12f;

        [Header("声音 id")]
        public string fireSound = "gun_shot";
        public string reloadSound = "gun_reload";
        public string emptySound = "gun_empty";
        public string equipSound = "weapon_equip";
    }
}
