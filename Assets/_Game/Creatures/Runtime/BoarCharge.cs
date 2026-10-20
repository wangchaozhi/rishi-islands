using UnityEngine;
using UnityEngine.AI;
using Rishi.Audio;
using Rishi.Core;

namespace Rishi.Creatures
{
    /// <summary>
    /// 野猪冲撞：追击中、看得见玩家、距离 5~14 米时，低头沿直线猛冲约 1.3 秒。
    /// 冲的方向一开始就定了——侧身闪开它就会冲过头，得掉头再来。撞上扣血、恐惧上涨。蚀化时伤害更高。
    /// </summary>
    [RequireComponent(typeof(Creature))]
    public class BoarCharge : MonoBehaviour
    {
        public Creature creature;
        public Beast beast;
        public float minDistance = 5f, maxDistance = 14f;
        public float speed = 11f;
        public float duration = 1.3f;
        public float cooldown = 4f;
        public float damage = 25f;
        public float nightDamageMultiplier = 1.5f;
        public float hitDistance = 1.5f;

        public bool Charging { get; private set; }

        NavMeshAgent agent;
        Vector3 dir;
        float t, cd = 2f;
        bool hit;

        void Awake()
        {
            if (creature == null) creature = GetComponent<Creature>();
            if (beast == null) beast = GetComponent<Beast>();
            agent = GetComponent<NavMeshAgent>();
        }

        /// <summary>这个距离能不能起冲。</summary>
        public static bool InChargeRange(float distance, float min, float max) => distance >= min && distance <= max;

        void Update()
        {
            if (creature.Dead) { if (Charging) Stop(); return; }
            float dt = Time.deltaTime;
            cd -= dt;
            var p = GameSignals.Player;
            if (p == null) return;
            var to = Vector3.ProjectOnPlane(p.position - transform.position, Vector3.up);
            float dist = to.magnitude;

            if (!Charging)
            {
                if (creature.State == CreatureState.Chase && creature.SeesPlayer && cd <= 0f && InChargeRange(dist, minDistance, maxDistance) && !GameSignals.InSafeZone)
                {
                    Charging = true;
                    hit = false;
                    t = 0f;
                    dir = to.normalized;
                    creature.speedMultiplier = 0f; // 冲撞期间由这里接管移动
                    AudioManager.Play("boar_charge", transform.position + Vector3.up * 0.6f, 1f);
                }
                return;
            }

            t += dt;
            var step = dir * speed * dt;
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.Move(step);
            else transform.position += step;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 12f * dt);
            if (!hit && dist < hitDistance)
            {
                hit = true;
                float dmg = damage * (beast != null && beast.Eclipsed ? nightDamageMultiplier : 1f);
                p.GetComponentInParent<IDamageable>()?.TakeDamage(dmg);
                GameSignals.AddFear(0.15f);
                AudioManager.Play("boar_hit", transform.position + Vector3.up * 0.6f, 1f);
            }
            if (t >= duration) Stop();
        }

        void Stop()
        {
            Charging = false;
            cd = cooldown;
            creature.speedMultiplier = 1f;
        }
    }
}
