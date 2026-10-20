using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Rishi.Audio;
using Rishi.Core;

namespace Rishi.Player
{
    /// <summary>
    /// 玩家生命：受伤红屏与痛哼、生命低时沉重的喘息（红色暗角由 HUD 画）、摔落伤害；
    /// 在点燃的篝火旁慢慢回血。倒下后黑屏，在最近一堆点燃的篝火旁醒来，一堆都没点时回到醒来的沙滩。
    /// 怪物通过 IDamageable 造成伤害。按 F9 受伤（调试用）。挂在玩家身上。
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        public float maxHealth = 100f;
        [Tooltip("复活时的生命比例")]
        [Range(0.1f, 1f)] public float respawnHealth = 0.7f;
        [Tooltip("篝火旁每秒回多少血")]
        public float campfireRegen = 8f;
        [Tooltip("生命低于这个比例时喘息、暗角")]
        [Range(0, 1)] public float lowThreshold = 0.35f;

        [Header("摔落")]
        [Tooltip("落地速度超过多少米/秒开始受伤")]
        public float safeFallSpeed = 11f;
        [Tooltip("超过部分每 1 米/秒扣多少血")]
        public float fallDamagePerSpeed = 9f;

        float health = -1f; // 还没初始化时按满血算（编辑器里的测试不会调用 Awake）
        public float Health { get => health < 0f ? maxHealth : health; private set => health = value; }
        public float Health01 => Health / maxHealth;
        public bool Dead { get; private set; }

        PlayerController player;
        CharacterController controller;
        Vector3 spawnPoint;
        float spawnYaw;
        float lastHurtSound = -10f;
        float airSpeed;
        AudioSource breathing;

        void Awake()
        {
            player = GetComponent<PlayerController>();
            controller = GetComponent<CharacterController>();
            Health = maxHealth;
        }

        void Start()
        {
            spawnPoint = transform.position;
            spawnYaw = transform.eulerAngles.y;
            var clip = AudioManager.Instance != null ? AudioManager.Instance.RandomClip("breath_hurt") : null;
            if (clip == null) return;
            breathing = gameObject.AddComponent<AudioSource>();
            breathing.clip = clip;
            breathing.loop = true;
            breathing.spatialBlend = 0f;
            breathing.volume = 0f;
            breathing.playOnAwake = false;
            breathing.Play();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            GameSignals.DamageFlash = Mathf.MoveTowards(GameSignals.DamageFlash, 0f, dt * 1.6f);
            if (Dead) return;

            var kb = Keyboard.current;
            if (kb != null && kb.f9Key.wasPressedThisFrame) TakeDamage(25f);

            // 摔落：记下空中的下落速度，落地时按超出的部分扣血（水里不算）
            if (controller != null)
            {
                if (!controller.isGrounded) airSpeed = Mathf.Max(airSpeed, -controller.velocity.y);
                else
                {
                    if (airSpeed > safeFallSpeed && !player.IsSwimming) TakeDamage((airSpeed - safeFallSpeed) * fallDamagePerSpeed);
                    airSpeed = 0f;
                }
            }

            // 篝火旁：安全区，慢慢回血
            var fire = Campfire.NearestLit(transform.position);
            bool safe = fire != null && (fire.transform.position - transform.position).sqrMagnitude < fire.safeRadius * fire.safeRadius;
            GameSignals.InSafeZone = safe;
            if (safe && Health < maxHealth) Health = Mathf.Min(maxHealth, Health + campfireRegen * dt);
            GameSignals.Health = Health01;

            if (breathing != null)
            {
                float target = Health01 < lowThreshold ? Mathf.Lerp(0.8f, 0.2f, Health01 / lowThreshold) : 0f;
                breathing.volume = Mathf.MoveTowards(breathing.volume, target, dt * 0.6f);
            }
        }

        public bool TakeDamage(float amount)
        {
            if (Dead || amount <= 0f) return false;
            Health = Mathf.Max(0f, Health - amount);
            GameSignals.Health = Health01;
            GameSignals.DamageFlash = Mathf.Clamp01(GameSignals.DamageFlash + 0.35f + amount / maxHealth);
            if (Time.time - lastHurtSound > 0.4f)
            {
                lastHurtSound = Time.time;
                AudioManager.Play2D("player_hurt", Mathf.Lerp(0.6f, 1f, amount / 40f));
            }
            if (Health > 0f) return false;
            if (Application.isPlaying) StartCoroutine(Die());
            else Dead = true;
            return true;
        }

        public void Heal(float amount)
        {
            if (Dead) return;
            Health = Mathf.Min(maxHealth, Health + amount);
            GameSignals.Health = Health01;
        }

        IEnumerator Die()
        {
            Dead = true;
            var owner = this;
            GameSignals.SetInputBlock(owner, true);
            AudioManager.Play2D("player_death", 1f);
            for (float t = 0; t < 1f; t += Time.deltaTime / 2f)
            {
                GameSignals.ScreenFade = t;
                yield return null;
            }
            GameSignals.ScreenFade = 1f;
            yield return new WaitForSeconds(1.5f);

            var fire = Campfire.NearestLit(transform.position);
            if (fire != null)
            {
                var p = fire.RespawnPoint;
                var to = fire.transform.position - p;
                player.Teleport(p, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);
            }
            else player.Teleport(spawnPoint, spawnYaw);
            Health = maxHealth * respawnHealth;
            GameSignals.Health = Health01;
            GameSignals.DamageFlash = 0f;
            airSpeed = 0f;
            AudioManager.Play2D("gasp", 0.8f);
            GameSignals.Toast(fire != null ? "……你在篝火旁醒来，浑身酸痛。" : "……你又一次在沙滩上醒来。");

            for (float t = 1f; t > 0f; t -= Time.deltaTime / 2.5f)
            {
                GameSignals.ScreenFade = t;
                yield return null;
            }
            GameSignals.ScreenFade = 0f;
            GameSignals.SetInputBlock(owner, false);
            Dead = false;
        }
    }
}
