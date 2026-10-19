using UnityEngine;

namespace Rishi.Wildlife
{
    /// <summary>小动物生态参数：数量、生成范围、受惊距离与速度。WildlifeDirector 读取它。资产在 Assets/Config/Wildlife.asset。</summary>
    [CreateAssetMenu(menuName = "Rishi/小动物配置", fileName = "Wildlife")]
    public class WildlifeConfig : ScriptableObject
    {
        [Header("数量")]
        public int maxCrabs = 14;
        public int maxButterflies = 12;
        public int maxFishSchools = 4;
        public int maxBats = 7;

        [Header("范围")]
        [Tooltip("在玩家周围多远内生成")]
        public float spawnRadius = 28f;
        [Tooltip("离玩家多远就回收")]
        public float despawnRadius = 45f;

        [Header("昼夜")]
        [Tooltip("太阳高度（-1~1）高于它算白天：蝴蝶出来")]
        public float dayElevation = 0.05f;
        [Tooltip("太阳高度低于它算黄昏和夜晚：蝙蝠出来")]
        public float duskElevation = 0.12f;
        [Tooltip("太阳高度低于它、且在植被里时出现萤火虫")]
        public float fireflyElevation = -0.03f;
        public float fireflyRate = 14f;

        [Header("螃蟹")]
        public float crabFleeDistance = 4.5f;
        public float crabWalkSpeed = 0.9f;
        public float crabFleeSpeed = 3.4f;

        [Header("蝴蝶")]
        public float butterflyFleeDistance = 2.2f;

        [Header("鱼群")]
        public float fishScareDistance = 4.5f;
        public float fishSwimSpeed = 1.1f;
        public float fishFleeSpeed = 3.5f;
    }
}
