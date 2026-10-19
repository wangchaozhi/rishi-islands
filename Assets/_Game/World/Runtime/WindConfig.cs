using UnityEngine;

namespace Rishi.World
{
    /// <summary>风的配置：方向、强度、阵风。WindController 读取它。资产在 Assets/Config/Wind.asset。</summary>
    [CreateAssetMenu(menuName = "Rishi/风配置", fileName = "Wind")]
    public class WindConfig : ScriptableObject
    {
        public Vector2 direction = new Vector2(0.8f, 0.6f);
        [Range(0, 3)] public float strength = 1f;
        public float gustStrength = 0.6f;
        public float gustFrequency = 0.12f;
        [Tooltip("风向缓慢摆动的幅度（弧度）")]
        public float directionWobble = 0.6f;
    }
}
