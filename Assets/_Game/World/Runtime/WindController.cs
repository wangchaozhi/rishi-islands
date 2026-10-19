using UnityEngine;
using Rishi.Core;

namespace Rishi.World
{
    /// <summary>
    /// 全局风：写入着色器全局变量 _IslandWind（xy 风向，z 强度，w 阵风），植物着色器 Island/Foliage 读取它摆动。
    /// 风暴等事件可以通过 Boost 临时加大。
    /// </summary>
    [ExecuteAlways]
    public class WindController : MonoBehaviour
    {
        public static WindController Instance { get; private set; }

        public Vector2 direction = new Vector2(0.8f, 0.6f);
        [Range(0, 3)] public float strength = 1f;
        public float gustStrength = 0.6f;
        public float gustFrequency = 0.12f;
        [Tooltip("额外加成（风暴时由 StormFX 设置）")]
        public float boost;

        static readonly int WindId = Shader.PropertyToID("_IslandWind");

        void OnEnable() => Instance = this;

        void Update()
        {
            float t = Time.time;
    #if UNITY_EDITOR
            if (!Application.isPlaying) t = (float)UnityEditor.EditorApplication.timeSinceStartup; // 编辑器里不运行也能看到摆动
    #endif
            float gust = (Mathf.PerlinNoise(t * gustFrequency, 0.37f) - 0.35f) * gustStrength;
            Vector2 dir = direction.normalized;
            // 风向缓慢摆动
            float wobble = (Mathf.PerlinNoise(t * 0.02f, 5.1f) - 0.5f) * 0.6f;
            dir = new Vector2(dir.x * Mathf.Cos(wobble) - dir.y * Mathf.Sin(wobble), dir.x * Mathf.Sin(wobble) + dir.y * Mathf.Cos(wobble));
            Shader.SetGlobalVector(WindId, new Vector4(dir.x, dir.y, strength + boost, Mathf.Max(0, gust) * (1 + boost)));
        }

        void OnDisable() => Shader.SetGlobalVector(WindId, Vector4.zero);
    }
}
