using UnityEngine;
using Rishi.Core;

namespace Rishi.World
{
    /// <summary>
    /// 全局风：写入着色器全局变量 _IslandWind（xy 风向，z 强度，w 阵风），植物着色器 Island/Foliage 读取它摆动。
    /// 参数在 WindConfig（Assets/Config/Wind.asset）；风暴等事件可以通过 boost 临时加大。
    /// </summary>
    [ExecuteAlways]
    public class WindController : MonoBehaviour
    {
        public static WindController Instance { get; private set; }

        [Tooltip("风的参数（Assets/Config/Wind.asset）；为空时用默认值")]
        public WindConfig config;
        [Tooltip("额外加成（风暴时由 StormFX 设置）")]
        public float boost;

        static readonly int WindId = Shader.PropertyToID("_IslandWind");
        static WindConfig defaults;

        public WindConfig Config
        {
            get
            {
                if (config != null) return config;
                if (defaults == null)
                {
                    defaults = ScriptableObject.CreateInstance<WindConfig>();
                    defaults.hideFlags = HideFlags.HideAndDontSave;
                }
                return defaults;
            }
        }

        void OnEnable() => Instance = this;

        void Update()
        {
            var c = Config;
            float t = Time.time;
    #if UNITY_EDITOR
            if (!Application.isPlaying) t = (float)UnityEditor.EditorApplication.timeSinceStartup; // 编辑器里不运行也能看到摆动
    #endif
            float gust = (Mathf.PerlinNoise(t * c.gustFrequency, 0.37f) - 0.35f) * c.gustStrength;
            Vector2 dir = c.direction.normalized;
            // 风向缓慢摆动
            float wobble = (Mathf.PerlinNoise(t * 0.02f, 5.1f) - 0.5f) * c.directionWobble;
            dir = new Vector2(dir.x * Mathf.Cos(wobble) - dir.y * Mathf.Sin(wobble), dir.x * Mathf.Sin(wobble) + dir.y * Mathf.Cos(wobble));
            Shader.SetGlobalVector(WindId, new Vector4(dir.x, dir.y, c.strength + boost, Mathf.Max(0, gust) * (1 + boost)));
        }

        void OnDisable() => Shader.SetGlobalVector(WindId, Vector4.zero);
    }
}
