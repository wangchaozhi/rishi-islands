using System.Collections;
using UnityEngine;
using Rishi.Audio;
using Rishi.Core;
using Rishi.Player;
using Rishi.World;

namespace Rishi.Weapons
{
    /// <summary>
    /// 可被射击的靶子：受击闪白，血量归零后隐藏，几秒后复活。
    /// </summary>
    public class Target : MonoBehaviour, IDamageable
    {
        public float maxHealth = 100f;
        public float respawnDelay = 3f;

        public float Health { get; private set; }

        Renderer[] renderers;
        Collider[] colliders;
        MaterialPropertyBlock block;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>();
            colliders = GetComponentsInChildren<Collider>();
            block = new MaterialPropertyBlock();
            Health = maxHealth;
        }

        /// <summary>造成伤害，返回这一击是否击杀。</summary>
        public bool TakeDamage(float amount)
        {
            if (Health <= 0) return false;
            Health -= amount;
            StopAllCoroutines();
            if (Health <= 0)
            {
                StartCoroutine(DieAndRespawn());
                return true;
            }
            StartCoroutine(Flash());
            return false;
        }

        IEnumerator Flash()
        {
            block.SetColor(BaseColorId, Color.white);
            block.SetColor(EmissionId, new Color(1f, 0.6f, 0.4f) * 2f);
            foreach (var r in renderers) r.SetPropertyBlock(block);
            yield return new WaitForSeconds(0.06f);
            ClearTint();
        }

        IEnumerator DieAndRespawn()
        {
            ClearTint();
            SetVisible(false);
            yield return new WaitForSeconds(respawnDelay);
            Health = maxHealth;
            SetVisible(true);
        }

        void SetVisible(bool visible)
        {
            foreach (var r in renderers) r.enabled = visible;
            foreach (var c in colliders) c.enabled = visible;
        }

        void ClearTint()
        {
            foreach (var r in renderers) r.SetPropertyBlock(null);
        }
    }
}
