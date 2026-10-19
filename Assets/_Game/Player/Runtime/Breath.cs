using System.Collections;
using UnityEngine;
using Rishi.Audio;
using Rishi.Core;

namespace Rishi.Player
{
    /// <summary>
    /// 氧气：头在水下时消耗，冲刺游得快消耗更多，浮出水面迅速恢复。
    /// 氧气低时心跳加重；某次下潜里氧气见底前，会隔着水听见三下敲击声（十年前陈默敲舱壁的声音）。
    /// 氧气耗尽昏迷，在最近的岸边醒来。挂在玩家身上。
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class Breath : MonoBehaviour
    {
        [Tooltip("能憋气多少秒")]
        public float capacity = 45f;
        public float fastSwimDrain = 1.6f;
        [Tooltip("水面上每秒恢复多少秒的氧气")]
        public float refillRate = 9f;
        [Range(0, 1)] public float lowThreshold = 0.3f;
        [Tooltip("氧气低到这个比例时可能听见敲击声")]
        [Range(0, 1)] public float knockThreshold = 0.22f;
        [Tooltip("呼气冒泡的粒子（可选）")]
        public ParticleSystem bubbles;

        public float Oxygen { get; private set; }
        public float Oxygen01 => Oxygen / capacity;
        public bool Drowning { get; private set; }

        PlayerController player;
        AudioSource heartbeat;
        float exhaleTimer = 2f;
        bool knockedThisDive;
        float diveTime;
        int dives;

        void Awake()
        {
            player = GetComponent<PlayerController>();
            Oxygen = capacity;
        }

        void Start()
        {
            var clip = AudioManager.Instance != null ? AudioManager.Instance.RandomClip("heartbeat") : null;
            if (clip == null) return;
            heartbeat = gameObject.AddComponent<AudioSource>();
            heartbeat.clip = clip;
            heartbeat.loop = true;
            heartbeat.spatialBlend = 0f;
            heartbeat.volume = 0f;
            heartbeat.playOnAwake = false;
            heartbeat.Play();
        }

        void Update()
        {
            if (Drowning) return;
            float dt = Time.deltaTime;
            if (player.IsHeadUnderwater)
            {
                if (diveTime == 0f) { dives++; knockedThisDive = false; }
                diveTime += dt;
                float drain = player.IsSwimming && player.Speed01 > 0.45f ? fastSwimDrain : 1f;
                Oxygen = Mathf.Max(0f, Oxygen - dt * drain);

                exhaleTimer -= dt;
                if (exhaleTimer <= 0f)
                {
                    exhaleTimer = Random.Range(2.5f, 4.5f) * Mathf.Lerp(0.5f, 1f, Oxygen01);
                    AudioManager.Play2D("bubbles", 0.5f);
                    if (bubbles != null) bubbles.Emit(Random.Range(6, 12));
                }

                // 第二次及以后的下潜里，憋气快到极限时听见三下敲击
                if (!knockedThisDive && dives >= 2 && Oxygen01 < knockThreshold)
                {
                    knockedThisDive = true;
                    StartCoroutine(Knock());
                }
                if (Oxygen <= 0f) StartCoroutine(BlackOut());
            }
            else
            {
                if (diveTime > capacity * 0.5f || Oxygen01 < 0.4f) AudioManager.Play2D("gasp", Mathf.Lerp(0.5f, 1f, 1f - Oxygen01));
                diveTime = 0f;
                Oxygen = Mathf.Min(capacity, Oxygen + refillRate * dt);
            }
            GameSignals.Oxygen = Oxygen01;

            if (heartbeat != null)
            {
                float target = Oxygen01 < lowThreshold ? Mathf.Lerp(0.9f, 0.25f, Oxygen01 / lowThreshold) : 0f;
                heartbeat.volume = Mathf.MoveTowards(heartbeat.volume, target, dt * 0.8f);
                heartbeat.pitch = Mathf.Lerp(1.35f, 1f, Oxygen01 / Mathf.Max(lowThreshold, 0.01f));
            }
        }

        IEnumerator Knock()
        {
            yield return new WaitForSeconds(Random.Range(0.3f, 1.2f));
            // 声音来自脚下更深的地方
            AudioManager.Play("knock_three", transform.position + Vector3.down * 12f + Random.insideUnitSphere * 4f, 1f);
        }

        IEnumerator BlackOut()
        {
            Drowning = true;
            var owner = this;
            GameSignals.SetInputBlock(owner, true);
            AudioManager.Play2D("drown", 1f);
            for (float t = 0; t < 1f; t += Time.deltaTime / 1.8f)
            {
                GameSignals.ScreenFade = t;
                yield return null;
            }
            GameSignals.ScreenFade = 1f;
            yield return new WaitForSeconds(1.2f);

            var cam = Camera.main;
            float yaw = cam != null ? cam.transform.eulerAngles.y : transform.eulerAngles.y;
            player.Teleport(player.LastSafeLand, yaw);
            Oxygen = capacity;
            GameSignals.Oxygen = 1f;
            diveTime = 0f;
            AudioManager.Play2D("gasp", 1f);
            GameSignals.Toast("……你在沙滩上醒来，咳出一大口海水。");

            for (float t = 1f; t > 0f; t -= Time.deltaTime / 2.5f)
            {
                GameSignals.ScreenFade = t;
                yield return null;
            }
            GameSignals.ScreenFade = 0f;
            GameSignals.SetInputBlock(owner, false);
            Drowning = false;
        }
    }
}
