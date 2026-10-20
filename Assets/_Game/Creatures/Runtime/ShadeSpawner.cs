using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using Rishi.Audio;
using Rishi.Core;
using Rishi.World;

namespace Rishi.Creatures
{
    /// <summary>
    /// 影嗣只在夜里出现：玩家不在篝火旁时，隔一段时间在玩家背后 / 侧面 35~50 米的黑暗里生成一只。
    /// 同时最多 1 只，每拿到一块碎片多 1 只（最多 4 只）。有影嗣在逼近时，环境声慢慢安静下来。
    /// 白天影嗣自己退走消散。按 F11 在面前生成一只（调试用）。
    /// </summary>
    public class ShadeSpawner : MonoBehaviour
    {
        public CreatureDef shadeDef;
        public DayNightCycle dayNight;
        public Material bodyMaterial, smokeMaterial, eyeMaterial;
        [Tooltip("两次生成之间的秒数范围")]
        public Vector2 spawnInterval = new Vector2(35f, 80f);
        public Vector2 spawnDistance = new Vector2(35f, 50f);

        readonly List<Shade> shades = new List<Shade>();
        float timer = 20f;

        /// <summary>同时存在的影嗣上限：1 只起，每块碎片 +1，最多 4 只。</summary>
        public static int MaxShades(int fragments) => Mathf.Clamp(1 + fragments, 1, 4);

        bool Night => dayNight != null && dayNight.SunElevation < -0.05f;

        void Update()
        {
            shades.RemoveAll(s => s == null);
            var kb = Keyboard.current;
            if (kb != null && kb.f11Key.wasPressedThisFrame && GameSignals.Player != null) SpawnInFront();

            UpdateThreat();

            if (!Night || GameSignals.CutsceneActive || GameSignals.Player == null) return;
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = Random.Range(spawnInterval.x, spawnInterval.y);
            if (GameSignals.InSafeZone || shades.Count >= MaxShades(GameSignals.Fragments)) return;
            if (TryFindSpawn(out var pos)) Add(pos);
        }

        /// <summary>有影嗣在追的时候，越近环境声越安静（危险来临前，鸟叫虫鸣突然停了）。</summary>
        void UpdateThreat()
        {
            var amb = AmbientController.Instance;
            if (amb == null) return;
            float threat = 0f;
            var p = GameSignals.Player;
            if (p != null)
                foreach (var s in shades)
                {
                    var st = s.creature.State;
                    if (st != CreatureState.Chase && st != CreatureState.Attack && st != CreatureState.Investigate) continue;
                    threat = Mathf.Max(threat, Mathf.InverseLerp(35f, 8f, Vector3.Distance(s.transform.position, p.position)));
                }
            amb.threatSilence = Mathf.MoveTowards(amb.threatSilence, threat * 0.9f, Time.deltaTime * 0.6f);
        }

        /// <summary>在玩家背后或侧面、导航网格上、不在篝火光里的地方找个生成点。</summary>
        bool TryFindSpawn(out Vector3 pos)
        {
            var p = GameSignals.Player.position;
            var cam = Camera.main;
            var fwd = cam != null ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized : Vector3.forward;
            for (int i = 0; i < 12; i++)
            {
                float yaw = Random.Range(100f, 260f); // 背后和两侧
                var dir = Quaternion.Euler(0, yaw, 0) * fwd;
                var candidate = p + dir * Random.Range(spawnDistance.x, spawnDistance.y);
                if (!NavMesh.SamplePosition(candidate, out var hit, 8f, NavMesh.AllAreas)) continue;
                if (hit.position.y < 0.3f) continue; // 不在水里
                bool nearFire = false;
                foreach (var f in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (f.enabled && f.type == LightType.Point && f.range > 5f && (f.transform.position - hit.position).sqrMagnitude < f.range * f.range) { nearFire = true; break; }
                if (nearFire) continue;
                pos = hit.position;
                return true;
            }
            pos = default;
            return false;
        }

        void SpawnInFront()
        {
            var p = GameSignals.Player;
            var cam = Camera.main;
            var fwd = Vector3.ProjectOnPlane(cam != null ? cam.transform.forward : p.forward, Vector3.up).normalized;
            var pos = p.position + fwd * 14f;
            if (NavMesh.SamplePosition(pos, out var hit, 6f, NavMesh.AllAreas)) pos = hit.position;
            Add(pos);
            GameSignals.Toast("生成了一只影嗣（调试）");
        }

        public Shade Add(Vector3 pos)
        {
            var p = GameSignals.Player;
            var look = p != null ? Vector3.ProjectOnPlane(p.position - pos, Vector3.up) : Vector3.forward;
            var s = Shade.Spawn(shadeDef, pos, Quaternion.LookRotation(look.sqrMagnitude > 1e-4f ? look : Vector3.forward), dayNight, bodyMaterial, smokeMaterial, eyeMaterial);
            shades.Add(s);
            return s;
        }
    }
}
