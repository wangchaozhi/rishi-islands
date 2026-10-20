using UnityEngine;

namespace Rishi.Creatures
{
    public enum LightReaction
    {
        [Tooltip("不怕光")] Ignore,
        [Tooltip("被照到时僵住，随后后退（影嗣）")] Flinch,
        [Tooltip("被照到就逃")] Flee,
    }

    /// <summary>
    /// 一种生物的参数：生命、移动、感知、对光的反应、攻击、作息、声音组。Creature 读取它。
    /// 资产在 Assets/Config/Creatures/，在 Inspector 里直接调。
    /// </summary>
    [CreateAssetMenu(menuName = "Rishi/生物", fileName = "Creature")]
    public class CreatureDef : ScriptableObject
    {
        public string displayName = "生物";

        [Header("生命")]
        public float maxHealth = 100f;

        [Header("移动")]
        public float walkSpeed = 1.4f;
        public float runSpeed = 4.5f;
        [Tooltip("游荡时离出生点最远多少米")]
        public float wanderRadius = 18f;

        [Header("视觉")]
        public float sightRange = 25f;
        [Range(10, 360)] public float sightAngle = 120f;
        [Tooltip("玩家开着手电时视距的倍数（光会暴露位置）")]
        public float flashlightSightBonus = 1.6f;
        [Tooltip("看不见玩家后还记得多少秒")]
        public float memorySeconds = 6f;

        [Header("听觉")]
        [Tooltip("噪声半径的倍数：1 = 正常听力")]
        public float hearing = 1f;

        [Header("光")]
        public LightReaction lightReaction = LightReaction.Ignore;
        [Tooltip("被照到后僵住的秒数")]
        public float stunSeconds = 1.5f;
        [Tooltip("僵住之后后退的秒数")]
        public float retreatSeconds = 3f;

        [Header("攻击")]
        public float attackDamage = 15f;
        public float attackRange = 1.8f;
        public float attackCooldown = 1.6f;
        [Tooltip("出手前的前摇（秒）：玩家有机会躲开")]
        public float attackWindup = 0.35f;

        [Header("作息")]
        public bool activeAtDay = true;
        public bool activeAtNight = true;

        [Header("恐惧")]
        [Tooltip("追击且看得见玩家时，玩家每秒增加的恐惧（离得越近越多）")]
        public float fearPerSecond = 0.04f;

        [Header("声音组（AudioManager 的 id）")]
        public string idleSound = "creature_idle";
        public Vector2 idleInterval = new Vector2(6f, 14f);
        public string alertSound = "creature_alert";
        public string attackSound = "creature_attack";
        public string hurtSound = "creature_hurt";
        public string deathSound = "creature_death";
    }
}
