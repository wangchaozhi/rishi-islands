using System.Collections.Generic;
using UnityEngine;
using Rishi.Audio;
using Rishi.Core;

namespace Rishi.Player
{
    /// <summary>
    /// 篝火：按 E 点燃（火焰、火星、烟、摇曳的火光、噼啪声）。点燃后是安全区（附近慢慢回血、怪物不靠近），
    /// 也是复活点：玩家倒下后在最近一堆点燃的篝火旁醒来。
    /// </summary>
    public class Campfire : Interactable
    {
        static readonly List<Campfire> all = new List<Campfire>();
        /// <summary>场景里所有篝火。</summary>
        public static IReadOnlyList<Campfire> All => all;

        public bool lit;
        [Tooltip("安全区半径（米）")]
        public float safeRadius = 7f;
        public Light fireLight;
        public ParticleSystem[] effects;
        [Tooltip("噼啪声（3D 循环）")]
        public AudioSource crackle;

        float baseIntensity = -1f, flickerSeed;

        public override string Prompt => lit ? null : "点燃篝火";

        void OnEnable() { all.Add(this); flickerSeed = Random.value * 100f; Apply(); }
        void OnDisable() => all.Remove(this);

        public override void Interact()
        {
            if (lit) return;
            lit = true;
            Apply();
            AudioManager.Play("fire_ignite", transform.position, 1f);
            GameSignals.Toast("篝火燃起来了。在这里是安全的，倒下后会在这里醒来。");
        }

        void Apply()
        {
            if (fireLight != null)
            {
                if (baseIntensity < 0f) baseIntensity = fireLight.intensity;
                fireLight.enabled = lit;
            }
            if (effects != null)
                foreach (var e in effects)
                {
                    if (e == null) continue;
                    if (lit) e.Play(); else e.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            if (crackle != null && Application.isPlaying)
            {
                if (crackle.clip == null && AudioManager.Instance != null) crackle.clip = AudioManager.Instance.RandomClip("fire_crackle");
                if (lit && crackle.clip != null) crackle.Play(); else crackle.Stop();
            }
        }

        void Update()
        {
            if (!lit || fireLight == null) return;
            // 火光摇曳：两层噪声叠加
            float t = Time.time * 7f + flickerSeed;
            float f = 0.75f + 0.18f * Mathf.PerlinNoise(t, 0.3f) + 0.12f * Mathf.PerlinNoise(t * 2.7f, 5.1f);
            fireLight.intensity = baseIntensity * f;
        }

        /// <summary>离 position 最近的一堆点燃的篝火，没有时返回 null。</summary>
        public static Campfire NearestLit(Vector3 position, IReadOnlyList<Campfire> fires = null)
        {
            Campfire best = null;
            float bestD = float.MaxValue;
            foreach (var c in fires ?? all)
            {
                if (c == null || !c.lit) continue;
                float d = (c.transform.position - position).sqrMagnitude;
                if (d < bestD) { bestD = d; best = c; }
            }
            return best;
        }

        /// <summary>复活位置：篝火旁边一点、朝向篝火。</summary>
        public Vector3 RespawnPoint => transform.position + transform.right * 1.8f + Vector3.up * 1.05f;

        /// <summary>离线截图用：直接点亮 / 熄灭（不出声、粒子先模拟几秒）。</summary>
        public void PreviewLit(bool on)
        {
            lit = on;
            Apply();
            if (on && effects != null) foreach (var e in effects) if (e != null) e.Simulate(3f, true, true);
            if (fireLight != null) fireLight.intensity = baseIntensity;
        }
    }
}
