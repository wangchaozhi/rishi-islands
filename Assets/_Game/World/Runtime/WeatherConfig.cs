using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rishi.World
{
    /// <summary>
    /// 动态天气的调度：有哪些天气、各自出现的权重、持续多久、过渡多久。
    /// 夜里浓雾的权重更高，每拿到一块碎片坏天气的权重都会再加一截。资产在 Assets/Config/WeatherConfig.asset。
    /// </summary>
    [CreateAssetMenu(menuName = "Rishi/天气调度", fileName = "WeatherConfig")]
    public class WeatherConfig : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public WeatherProfile profile;
            [Tooltip("基础权重")]
            public float weight = 1f;
            [Tooltip("夜里的权重倍数（浓雾在夜里更常见）")]
            public float nightMultiplier = 1f;
            [Tooltip("每拿到一块碎片权重增加的比例（坏天气 > 0）")]
            public float perFragment;
        }

        public List<Entry> weathers = new List<Entry>();
        [Tooltip("开场的天气（第一章从晴天开始）")]
        public WeatherProfile startWeather;
        [Tooltip("一种天气持续的时间范围（现实秒）")]
        public Vector2 duration = new Vector2(180f, 420f);
        [Tooltip("两种天气之间平滑过渡的时间（秒）")]
        public float transitionSeconds = 45f;
        [Tooltip("地面淋湿 / 晾干需要的时间（秒）")]
        public Vector2 wetDrySeconds = new Vector2(40f, 180f);
        [Tooltip("名字里带这些词的材质下雨时会变湿（岩石、海崖……）")]
        public List<string> wetMaterialKeywords = new List<string> { "rock", "cliff", "boulder", "stone" };

        /// <summary>默认的六种天气：(id, 权重, 夜里倍数, 每块碎片增加的比例)。</summary>
        public static readonly (string id, float weight, float night, float perFragment)[] DefaultEntries =
        {
            ("Clear", 3f, 0.8f, 0f),
            ("Cloudy", 2.5f, 1f, 0f),
            ("Overcast", 1.5f, 1f, 0.15f),
            ("Rain", 1.2f, 1f, 0.3f),
            ("Thunderstorm", 0.5f, 1.2f, 0.45f),
            ("Fog", 0.6f, 2.5f, 0.35f),
        };

        /// <summary>按当前是否夜晚、碎片数，算出每种天气被选中的权重。</summary>
        public float[] Weights(bool night, int fragments)
        {
            var w = new float[weathers.Count];
            for (int i = 0; i < w.Length; i++)
            {
                var e = weathers[i];
                if (e.profile == null) continue;
                w[i] = Mathf.Max(0f, e.weight * (night ? e.nightMultiplier : 1f) * (1f + e.perFragment * fragments));
            }
            return w;
        }

        /// <summary>按权重随机挑下一种天气（尽量不和现在一样）；r 是 0~1 的随机数。</summary>
        public WeatherProfile Pick(bool night, int fragments, WeatherProfile current, float r)
        {
            var w = Weights(night, fragments);
            float total = 0f;
            for (int i = 0; i < w.Length; i++)
                if (weathers[i].profile != current) total += w[i];
            if (total <= 0f) return current;
            float x = r * total;
            for (int i = 0; i < w.Length; i++)
            {
                if (weathers[i].profile == current) continue;
                if (x < w[i]) return weathers[i].profile;
                x -= w[i];
            }
            return current;
        }
    }
}
