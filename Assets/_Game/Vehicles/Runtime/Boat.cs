using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using Rishi.Audio;
using Rishi.Core;
using Rishi.Inventory;
using Rishi.Player;

namespace Rishi.Vehicles
{
    /// <summary>
    /// 沉船旁的小艇。船底破了，要用木板和绳子修好（按 E）；修好后按 E 上船：
    /// W / S 划桨前进后退，A / D 转向，按住 Shift + W 开船尾的小马达（快，但很吵，会把东西引过来）；E 下船（附近有岸就跳到岸上，没有就下水）。
    /// 船浮在海面上随浪起伏，撞到浅滩搁浅停下，撞到礁石、树之类的停下。
    /// 第一次上船触发 onFirstBoard（CG06 启航），第一次在环礁上下船触发 onReachAtoll。
    /// </summary>
    public class Boat : Interactable
    {
        [Header("修理")]
        public ItemDef plankItem;
        public int planksNeeded = 3;
        public ItemDef ropeItem;
        public int ropesNeeded = 1;
        [Tooltip("修好之前显示（船底的破洞、散落的木板）")]
        public GameObject brokenParts;
        [Tooltip("修好之后显示（钉上去的补丁、绑好的绳子）")]
        public GameObject repairedParts;
        public UnityEvent onRepaired = new UnityEvent();

        [Header("驾驶")]
        public Transform seat;
        public Transform[] oars;
        public Transform propeller;
        public float rowSpeed = 2.6f;
        public float motorSpeed = 8.5f;
        public float reverseSpeed = 1.4f;
        [Tooltip("转向角速度（度/秒，船速 2 米/秒以上时）")]
        public float turnRate = 38f;
        [Tooltip("吃水深度：船底在水面下多少米")]
        public float draft = 0.22f;
        [Tooltip("船头 / 船尾离中心的距离（判断搁浅）")]
        public float halfLength = 2f;
        public AudioSource motorSource;
        public AudioSource waterSource;

        [Header("剧情")]
        public UnityEvent onFirstBoard = new UnityEvent();
        [Tooltip("在这块地形上下船算到达（环礁）")]
        public string reachTerrain = "Atoll";
        public UnityEvent onReachAtoll = new UnityEvent();

        public bool Repaired { get; private set; }
        public bool Occupied => rider != null;
        public float Speed => speed;
        public bool MotorOn { get; private set; }

        PlayerController rider;
        bool boarded, reached, repairing, motorStarted;
        int boardFrame = -1;
        float speed, yaw, rowPhase, slapTimer = 2f, noiseTimer;
        Quaternion[] oarRest;

        // ---------------- 纯计算（测试用） ----------------

        /// <summary>船底高度：浮在水面（吃水 draft），水太浅就坐在海底 / 沙滩上。</summary>
        public static float HullHeight(float seaSurface, float ground, float draft) => Mathf.Max(seaSurface - draft, ground);

        /// <summary>
        /// 往前走会不会搁浅：前方的地面露出水面（高于吃水线）而且比脚下更高，就是冲上了岸。
        /// 往更低的地方走总是可以的（把搁在沙滩上的船推下水）。
        /// </summary>
        public static bool Blocked(float groundAhead, float groundHere, float seaSurface, float draft)
            => groundAhead > seaSurface - draft * 0.4f && groundAhead > groundHere + 0.03f;

        /// <summary>修船还缺什么（空字符串表示齐了）。</summary>
        public static string Missing(int planks, int planksNeeded, int ropes, int ropesNeeded)
        {
            string s = "";
            if (planks < planksNeeded) s += $"木板 ×{planksNeeded - planks}";
            if (ropes < ropesNeeded) s += (s.Length > 0 ? "、" : "") + $"绳子 ×{ropesNeeded - ropes}";
            return s;
        }

        // ---------------- 互动 ----------------

        public override string Prompt
        {
            get
            {
                if (repairing || Occupied) return null;
                if (Repaired) return "上船";
                var inv = PlayerInventory.Instance;
                int planks = inv != null ? inv.Count(plankItem) : 0, ropes = inv != null ? inv.Count(ropeItem) : 0;
                return $"修补小艇（木板 {Mathf.Min(planks, planksNeeded)}/{planksNeeded}，绳子 {Mathf.Min(ropes, ropesNeeded)}/{ropesNeeded}）";
            }
        }

        public override void Interact()
        {
            if (Repaired) { Board(); return; }
            var inv = PlayerInventory.Instance;
            string missing = Missing(inv != null ? inv.Count(plankItem) : 0, planksNeeded, inv != null ? inv.Count(ropeItem) : 0, ropesNeeded);
            if (missing.Length > 0)
            {
                GameSignals.Toast($"船底破了个洞，还缺：{missing}");
                return;
            }
            StartCoroutine(Repair(inv));
        }

        IEnumerator Repair(PlayerInventory inv)
        {
            repairing = true;
            GameSignals.SetInputBlock(this, true);
            GameSignals.Toast("把木板钉在破洞上，再用绳子勒紧……");
            for (int i = 0; i < 6; i++)
            {
                AudioManager.Play("impact_wood", transform.position, 0.9f);
                yield return new WaitForSeconds(i == 2 ? 0.9f : 0.42f);
            }
            inv.Remove(plankItem, planksNeeded);
            inv.Remove(ropeItem, ropesNeeded);
            SetRepaired(true);
            GameSignals.SetInputBlock(this, false);
            repairing = false;
            GameSignals.Toast("小艇修好了。还能浮起来。");
            onRepaired?.Invoke();
        }

        /// <summary>直接设置修好与否（预览、存档用）。</summary>
        public void SetRepaired(bool repaired)
        {
            Repaired = repaired;
            if (brokenParts != null) brokenParts.SetActive(!repaired);
            if (repairedParts != null) repairedParts.SetActive(repaired);
        }

        void Board()
        {
            var p = GameSignals.Player != null ? GameSignals.Player.GetComponent<PlayerController>() : null;
            if (p == null || seat == null) return;
            rider = p;
            rider.SitOn(seat);
            boardFrame = Time.frameCount; // 同一帧的 E 是上船，不要马上又下船
            rider.LeftSeat += OnRiderLeft;
            AudioManager.Play("impact_wood", transform.position, 0.5f);
            if (!boarded)
            {
                boarded = true;
                GameSignals.Toast("W / S 划桨  A / D 转向  按住 Shift 开马达  E 下船");
                onFirstBoard?.Invoke();
            }
        }

        void OnRiderLeft()
        {
            if (rider != null) rider.LeftSeat -= OnRiderLeft;
            rider = null;
            SetMotor(false);
        }

        /// <summary>下船：附近有岸就跳到岸上（离船最近的干地），没有就从船舷下水。</summary>
        void Disembark()
        {
            var r = rider;
            if (r == null) return;
            Vector3 best = Vector3.zero;
            float bestD = float.MaxValue;
            for (float dist = 1.6f; dist <= 6f; dist += 0.5f)
                for (int a = 0; a < 16; a++)
                {
                    var p = transform.position + Quaternion.Euler(0, a * 22.5f, 0) * Vector3.forward * dist;
                    float g = Islands.GroundY(p);
                    if (g < 0.2f || dist >= bestD) continue;
                    bestD = dist;
                    best = new Vector3(p.x, g + 1.05f, p.z);
                }
            float faceYaw = transform.eulerAngles.y;
            if (bestD == float.MaxValue) best = transform.position + transform.right * 1.8f + Vector3.up * 0.1f; // 深水：下水游泳
            else faceYaw = Mathf.Atan2(best.x - transform.position.x, best.z - transform.position.z) * Mathf.Rad2Deg;
            r.Teleport(best, faceYaw); // 会触发 LeftSeat
            AudioManager.Play(bestD == float.MaxValue ? "splash_enter" : "impact_wood", best, 0.6f);

            var t = Islands.TerrainAt(best);
            if (!reached && bestD != float.MaxValue && t != null && t.name == reachTerrain)
            {
                reached = true;
                onReachAtoll?.Invoke();
            }
        }

        // ---------------- 运动 ----------------

        void Awake()
        {
            yaw = transform.eulerAngles.y;
            if (oars != null)
            {
                oarRest = new Quaternion[oars.Length];
                for (int i = 0; i < oars.Length; i++) oarRest[i] = oars[i] != null ? oars[i].localRotation : Quaternion.identity;
            }
            SetRepaired(Repaired);
        }

        void OnDisable() => OnRiderLeft();

        void Update()
        {
            float dt = Time.deltaTime;
            float throttle = 0f, steer = 0f;
            bool wantMotor = false;
            var kb = Keyboard.current;
            if (Occupied && kb != null && !GameSignals.GameplayBlocked)
            {
                if (kb.eKey.wasPressedThisFrame && Time.frameCount != boardFrame) { Disembark(); return; }
                if (kb.wKey.isPressed) throttle += 1f;
                if (kb.sKey.isPressed) throttle -= 1f;
                if (kb.dKey.isPressed) steer += 1f;
                if (kb.aKey.isPressed) steer -= 1f;
                wantMotor = throttle > 0f && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
            }
            SetMotor(wantMotor);

            // 速度：划桨慢慢加速，马达更快；松手后被水的阻力拖慢
            float target = throttle > 0f ? (MotorOn ? motorSpeed : rowSpeed) : throttle < 0f ? -reverseSpeed : 0f;
            float accel = MotorOn ? 2.2f : throttle != 0f ? 1.1f : 0.7f;
            speed = Mathf.MoveTowards(speed, target, accel * dt);

            // 转向：船要有速度才转得动；划桨时原地也能慢慢掉头
            float steerScale = Mathf.Clamp01(Mathf.Abs(speed) / 2f) + (Occupied && !MotorOn ? 0.35f : 0f);
            yaw += steer * turnRate * Mathf.Min(steerScale, 1f) * (speed < -0.05f ? -1f : 1f) * dt;

            var pos = transform.position;
            var fwd = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            float sea = SeaSurface(pos);

            // 搁浅 / 撞上障碍：船头（倒车时船尾）前方的地面太高，或者有碰撞体挡着
            if (Mathf.Abs(speed) > 0.01f)
            {
                var dir = fwd * Mathf.Sign(speed);
                var probe = pos + dir * (halfLength + Mathf.Abs(speed) * dt + 0.2f);
                bool hit = Blocked(Islands.GroundY(probe), Islands.GroundY(pos), SeaSurface(probe), draft) || Obstacle(pos, dir, halfLength + Mathf.Abs(speed) * dt);
                if (hit)
                {
                    if (Mathf.Abs(speed) > 1.2f) AudioManager.Play("boat_scrape", pos + dir * halfLength, Mathf.Clamp01(Mathf.Abs(speed) / motorSpeed + 0.4f));
                    speed = 0f;
                }
                else pos += fwd * speed * dt;
            }

            float ground = Islands.GroundY(pos);
            bool floating = ground < sea - draft;
            pos.y = HullHeight(sea, ground, draft);

            // 船身姿态：浮着时随浪摇晃，开马达时船头抬起；搁浅时稍微歪着
            float t = Time.time;
            float wave = WaveScale();
            float pitchDeg = floating ? (Mathf.Sin(t * 1.3f + pos.x * 0.2f) * 2.2f * wave - Mathf.Clamp01(speed / motorSpeed) * 5f) : -2f;
            float rollDeg = floating ? Mathf.Sin(t * 0.9f + pos.z * 0.17f) * 3f * wave - steer * Mathf.Clamp01(Mathf.Abs(speed) / 3f) * 4f : 4f;
            transform.SetPositionAndRotation(pos, Quaternion.Euler(pitchDeg, yaw, rollDeg));

            AnimateOars(throttle, dt);
            UpdateSounds(floating, dt);
        }

        /// <summary>海面高度：平均海平面加上几道慢浪（天气起风时浪更大）。</summary>
        float SeaSurface(Vector3 p)
        {
            float t = Time.time, w = WaveScale();
            return GameSignals.SeaLevel + (Mathf.Sin(t * 1.1f + p.x * 0.15f) * 0.08f + Mathf.Sin(t * 1.7f + p.z * 0.21f) * 0.05f) * w;
        }

        static float WaveScale() => 1f + Shader.GetGlobalFloat("_IslandWaveBoost") * 1.5f;

        bool Obstacle(Vector3 pos, Vector3 dir, float dist)
        {
            var origin = pos + Vector3.up * 0.45f;
            foreach (var h in Physics.SphereCastAll(origin, 0.55f, dir, dist, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.collider is TerrainCollider) continue;          // 地形按水深判断
                if (h.transform.IsChildOf(transform)) continue;
                if (GameSignals.Player != null && h.transform.IsChildOf(GameSignals.Player)) continue;
                if (h.distance <= 0f) continue;                       // 起点就重叠（例如靠岸时碰着岸边的东西）不算
                return true;
            }
            return false;
        }

        void AnimateOars(float throttle, float dt)
        {
            if (oars == null || oarRest == null) return;
            bool rowing = Occupied && throttle != 0f && !MotorOn;
            if (rowing)
            {
                float before = rowPhase;
                rowPhase += dt / 1.4f * Mathf.Sign(throttle);
                if (Mathf.FloorToInt(before) != Mathf.FloorToInt(rowPhase)) AudioManager.Play("boat_oar", transform.position, 0.8f);
            }
            float a = rowing ? rowPhase * Mathf.PI * 2f : 0f;
            for (int i = 0; i < oars.Length; i++)
            {
                if (oars[i] == null) continue;
                // 桨在桨架上前后摆（绕竖轴），划水时桨叶下沉（绕纵轴）
                float swing = rowing ? Mathf.Sin(a) * 32f : 0f;
                float dip = rowing ? (Mathf.Cos(a) > 0f ? 10f : -6f) : 0f;
                float side = i % 2 == 0 ? 1f : -1f;
                oars[i].localRotation = oarRest[i] * Quaternion.Euler(0, swing * side, dip * side);
            }
        }

        void SetMotor(bool on)
        {
            if (on == MotorOn) return;
            MotorOn = on;
            if (on && !motorStarted) { motorStarted = true; AudioManager.Play("boat_motor_start", transform.position, 0.9f); }
            if (motorSource == null) return;
            if (on && !motorSource.isPlaying) motorSource.Play();
        }

        void UpdateSounds(bool floating, float dt)
        {
            float s01 = Mathf.Clamp01(Mathf.Abs(speed) / motorSpeed);
            if (motorSource != null)
            {
                // 松开油门后马达声慢慢收掉，而不是一下就断
                float targetVol = MotorOn ? 0.75f : 0f;
                motorSource.volume = Mathf.MoveTowards(motorSource.volume, targetVol, dt * (MotorOn ? 3f : 1.2f));
                motorSource.pitch = 0.75f + s01 * 0.55f;
                if (!MotorOn && motorSource.volume <= 0.001f && motorSource.isPlaying) motorSource.Stop();
            }
            if (propeller != null && MotorOn) propeller.Rotate(Vector3.forward, 1800f * dt, Space.Self);
            if (waterSource != null) waterSource.volume = floating ? 0.12f + s01 * 0.6f : 0f;

            if (floating)
            {
                slapTimer -= dt * (1f + s01 * 3f);
                if (slapTimer <= 0f)
                {
                    slapTimer = Random.Range(1.5f, 4f);
                    AudioManager.Play("boat_slap", transform.position + transform.forward * halfLength * 0.6f, 0.5f + s01 * 0.5f);
                }
            }

            if (MotorOn)
            {
                noiseTimer -= dt;
                if (noiseTimer <= 0f) { noiseTimer = 1f; GameSignals.MakeNoise(transform.position, 45f); }
            }
        }
    }
}
