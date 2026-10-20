using System;
using UnityEngine;
using UnityEngine.AI;
using Rishi.Audio;
using Rishi.Core;
using Rishi.World;

namespace Rishi.Creatures
{
    public enum CreatureState { Idle, Wander, Investigate, Chase, Attack, Stunned, Retreat, Dead }

    /// <summary>
    /// 生物框架：生命与受击、感知（视觉锥 + 遮挡、听见噪声、是否被手电照到）、状态机、导航寻路、声音组。
    /// 待机 → 游荡 → 听到动静去调查 → 看见玩家追击 → 进入攻击范围出手；被光照到按 CreatureDef 僵住后退或逃走；
    /// 玩家在篝火安全区里时不靠近；不在作息时间（例如夜行生物到了白天）就退走消失。
    /// 不在导航网格上时退回直线移动。具体的生物（影嗣、野猪……）在此基础上配置参数或派生。
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class Creature : MonoBehaviour, IDamageable
    {
        public CreatureDef def;
        public DayNightCycle dayNight;
        [Tooltip("眼睛位置（视线起点），为空时用身体上方 1.5 米")]
        public Transform eyes;
        [Tooltip("移动速度倍数（影嗣被盯着时几乎不动）")]
        public float speedMultiplier = 1f;

        public CreatureState State { get; private set; } = CreatureState.Idle;
        float health = -1f; // 还没初始化时按满血算（编辑器里的测试不会调用 Awake）
        public float Health { get => health < 0f ? (def != null ? def.maxHealth : 100f) : health; private set => health = value; }
        public bool Dead => State == CreatureState.Dead;
        public bool SeesPlayer { get; private set; }
        public bool Lit { get; private set; }

        /// <summary>状态变化（旧状态、新状态），给动画和调试用。</summary>
        public event Action<CreatureState, CreatureState> StateChanged;
        public event Action<Creature> Died;

        NavMeshAgent agent;
        Vector3 home, target, lastSeen;
        float stateTime, lastSeenTime = -999f, nextAttack, nextIdleSound, senseTimer, attackTimer = -1f;
        bool attackQueued, fresh, provoked;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
        }

        void OnEnable() => GameSignals.NoiseMade += OnNoise;
        void OnDisable() => GameSignals.NoiseMade -= OnNoise;

        void Start()
        {
            home = transform.position;
            nextIdleSound = Time.time + UnityEngine.Random.Range(def.idleInterval.x, def.idleInterval.y);
            SetState(CreatureState.Wander);
        }

        Vector3 EyePos => eyes != null ? eyes.position : transform.position + Vector3.up * 1.5f;
        Transform Player => GameSignals.Player;

        void Update()
        {
            if (Dead || def == null) return;
            float dt = Time.deltaTime;
            stateTime += dt;

            senseTimer -= dt;
            if (senseTimer <= 0f) { senseTimer = 0.15f; Sense(); }

            // 作息：不该出来的时候退走消失
            if (!ActiveNow() && State != CreatureState.Retreat) SetState(CreatureState.Retreat);

            // 光：被照到时按设定僵住 / 逃
            if (Lit && def.lightReaction != LightReaction.Ignore && State != CreatureState.Stunned && State != CreatureState.Retreat)
            {
                if (def.lightReaction == LightReaction.Flinch) SetState(CreatureState.Stunned);
                else SetState(CreatureState.Retreat);
                AudioManager.Play(def.hurtSound, EyePos, 0.8f);
            }

            switch (State)
            {
                case CreatureState.Idle:
                    Stop();
                    if (stateTime > 3f) SetState(CreatureState.Wander);
                    if (SeesPlayer && WantsToAttack()) Alert();
                    break;
                case CreatureState.Wander:
                    if (fresh)
                    {
                        fresh = false;
                        var r = UnityEngine.Random.insideUnitCircle * def.wanderRadius;
                        target = home + new Vector3(r.x, 0, r.y);
                    }
                    else if (stateTime > 0.5f && (Arrived() || stateTime > 25f)) { SetState(CreatureState.Idle); break; }
                    MoveTo(target, def.walkSpeed);
                    if (SeesPlayer && WantsToAttack()) Alert();
                    break;
                case CreatureState.Investigate:
                    MoveTo(target, Mathf.Lerp(def.walkSpeed, def.runSpeed, 0.4f));
                    if (SeesPlayer && WantsToAttack()) Alert();
                    else if (Arrived() || stateTime > 20f) { if (stateTime > 3f) SetState(CreatureState.Wander); }
                    break;
                case CreatureState.Chase:
                    if (SeesPlayer) lastSeen = Player.position;
                    if (GameSignals.InSafeZone) { SetState(CreatureState.Retreat); break; } // 篝火旁不靠近
                    if (Time.time - lastSeenTime > def.memorySeconds) { target = lastSeen; SetState(CreatureState.Investigate); break; }
                    MoveTo(lastSeen, def.runSpeed);
                    if (Player != null && Vector3.Distance(transform.position, Player.position) <= def.attackRange) SetState(CreatureState.Attack);
                    if (SeesPlayer && Player != null)
                    {
                        float near = Mathf.InverseLerp(def.sightRange, 2f, Vector3.Distance(transform.position, Player.position));
                        GameSignals.AddFear(def.fearPerSecond * Mathf.Lerp(0.3f, 1.5f, near) * dt);
                    }
                    break;
                case CreatureState.Attack:
                    Stop();
                    if (Player == null) { SetState(CreatureState.Wander); break; }
                    Face(Player.position, dt);
                    float dist = Vector3.Distance(transform.position, Player.position);
                    if (!attackQueued && Time.time >= nextAttack)
                    {
                        attackQueued = true;
                        attackTimer = def.attackWindup;
                        AudioManager.Play(def.attackSound, EyePos, 1f);
                    }
                    if (attackQueued)
                    {
                        attackTimer -= dt;
                        if (attackTimer <= 0f)
                        {
                            attackQueued = false;
                            nextAttack = Time.time + def.attackCooldown;
                            // 前摇结束时玩家还在范围里才算打中
                            if (dist <= def.attackRange * 1.15f && !GameSignals.InSafeZone)
                                Player.GetComponentInParent<IDamageable>()?.TakeDamage(def.attackDamage);
                        }
                    }
                    else if (dist > def.attackRange * 1.3f) SetState(CreatureState.Chase);
                    break;
                case CreatureState.Stunned:
                    Stop();
                    if (stateTime > def.stunSeconds) SetState(CreatureState.Retreat);
                    break;
                case CreatureState.Retreat:
                    if (Player != null) MoveTo(transform.position + (transform.position - Player.position).normalized * 8f, def.runSpeed);
                    if (!ActiveNow() && stateTime > 6f) Destroy(gameObject);           // 退到看不见的地方消失
                    else if (ActiveNow() && stateTime > def.retreatSeconds && !Lit) SetState(SeesPlayer && !GameSignals.InSafeZone ? CreatureState.Chase : CreatureState.Wander);
                    break;
            }

            if (Time.time >= nextIdleSound)
            {
                nextIdleSound = Time.time + UnityEngine.Random.Range(def.idleInterval.x, def.idleInterval.y);
                if (State == CreatureState.Idle || State == CreatureState.Wander) AudioManager.Play(def.idleSound, EyePos, 0.8f);
            }
        }

        // ---------------- 感知 ----------------

        void Sense()
        {
            var p = Player;
            SeesPlayer = false;
            Lit = false;
            if (p == null) return;
            Vector3 head = p.position + Vector3.up * 0.6f;
            float range = def.sightRange * (GameSignals.FlashlightOn ? def.flashlightSightBonus : 1f);
            if (Perception.InCone(EyePos, transform.forward, head, range, def.sightAngle * 0.5f) && Perception.Clear(EyePos, head, transform, p))
            {
                SeesPlayer = true;
                lastSeen = p.position;
                lastSeenTime = Time.time;
            }
            var beam = GameSignals.Flashlight;
            if (beam != null && beam.enabled && GameSignals.FlashlightOn)
            {
                Vector3 body = transform.position + Vector3.up * 1f;
                Lit = Perception.InCone(beam.transform.position, beam.transform.forward, body, beam.range, beam.spotAngle * 0.5f)
                      && Perception.Clear(beam.transform.position, body, p, transform);
            }
        }

        void OnNoise(Vector3 pos, float radius)
        {
            if (Dead || def == null) return;
            if (State == CreatureState.Chase || State == CreatureState.Attack || State == CreatureState.Stunned || State == CreatureState.Retreat) return;
            if (!Perception.CanHear(transform.position, pos, radius, def.hearing)) return;
            target = pos;
            SetState(CreatureState.Investigate);
        }

        /// <summary>看见玩家后要不要动手：被打过（激怒）或玩家走进了 aggroRange。</summary>
        bool WantsToAttack() => ShouldAggro(provoked, Player != null ? Vector3.Distance(transform.position, Player.position) : 999f, def.aggroRange);

        public static bool ShouldAggro(bool provoked, float distance, float aggroRange) => provoked || distance <= aggroRange;

        /// <summary>被打过，之后看见就追。</summary>
        public bool Provoked => provoked;

        void Alert()
        {
            AudioManager.Play(def.alertSound, EyePos, 1f);
            SetState(CreatureState.Chase);
        }

        bool ActiveNow()
        {
            if (dayNight == null) return true;
            bool day = dayNight.SunElevation > 0.02f;
            return day ? def.activeAtDay : def.activeAtNight;
        }

        // ---------------- 移动 ----------------

        void MoveTo(Vector3 dest, float speed)
        {
            speed *= Mathf.Max(0f, speedMultiplier);
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.speed = speed;
                agent.isStopped = false;
                if ((agent.destination - dest).sqrMagnitude > 0.25f) agent.SetDestination(dest);
                return;
            }
            // 没有导航网格时：直线走过去，贴地
            var d = dest - transform.position; d.y = 0f;
            if (d.sqrMagnitude < 0.04f) return;
            Face(dest, Time.deltaTime);
            var next = transform.position + d.normalized * speed * Time.deltaTime;
            var t = Terrain.activeTerrain;
            if (t != null) next.y = t.SampleHeight(next) + t.transform.position.y;
            transform.position = next;
        }

        void Stop()
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
        }

        bool Arrived()
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
                return !agent.pathPending && agent.remainingDistance <= Mathf.Max(agent.stoppingDistance, 0.6f);
            var d = target - transform.position; d.y = 0f;
            return d.sqrMagnitude < 0.6f * 0.6f;
        }

        void Face(Vector3 point, float dt)
        {
            var d = point - transform.position; d.y = 0f;
            if (d.sqrMagnitude < 1e-4f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), 8f * dt);
        }

        // ---------------- 受伤与死亡 ----------------

        public bool TakeDamage(float amount)
        {
            if (Dead || amount <= 0f) return false;
            provoked = true;
            Health = Mathf.Max(0f, Health - amount * (def != null ? def.damageTaken : 1f));
            if (Health <= 0f) { Die(); return true; }
            if (def != null)
            {
                AudioManager.Play(def.hurtSound, EyePos, 1f);
                // 被打了就知道玩家在哪儿
                if (Player != null) { lastSeen = Player.position; lastSeenTime = Time.time; }
                if (State != CreatureState.Stunned && State != CreatureState.Retreat) SetState(CreatureState.Chase);
            }
            return false;
        }

        void Die()
        {
            SetState(CreatureState.Dead);
            if (def != null) AudioManager.Play(def.deathSound, EyePos, 1f);
            if (agent != null) agent.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            Died?.Invoke(this);
            if (Application.isPlaying) Destroy(gameObject, 8f);
        }

        void SetState(CreatureState s)
        {
            if (s == State) return;
            var old = State;
            State = s;
            stateTime = 0f;
            attackQueued = false;
            fresh = true;
            StateChanged?.Invoke(old, s);
        }
    }
}
