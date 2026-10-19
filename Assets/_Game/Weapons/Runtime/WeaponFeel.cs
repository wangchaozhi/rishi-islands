using UnityEngine;
using Rishi.Audio;
using Rishi.Core;
using Rishi.Player;
using Rishi.World;

namespace Rishi.Weapons
{
    /// <summary>
    /// 武器手感：鼠标拖拽惯性、走路摆动、冲刺姿态、开枪后坐（弹簧回位）、换弹动作。挂在 WeaponHolder 上。
    /// </summary>
    public class WeaponFeel : MonoBehaviour
    {
        public PlayerController player;

        [Header("惯性摆动")]
        public float swayAmount = 1.6f;
        public float swayMax = 4f;
        public float swaySmooth = 10f;

        [Header("走路摆动")]
        public float bobAmount = 0.012f;
        public float bobSpeed = 9f;

        [Header("后坐")]
        public float kickBack = 0.06f;
        public float kickUp = 7f;
        public float springStiffness = 260f;
        public float springDamping = 22f;

        [Header("姿态")]
        public Vector3 sprintOffset = new Vector3(-0.03f, -0.04f, -0.02f);
        public Vector3 sprintEuler = new Vector3(12f, -28f, 8f);
        public Vector3 reloadOffset = new Vector3(0f, -0.08f, -0.03f);
        public Vector3 reloadEuler = new Vector3(-25f, 10f, 35f);
        [Tooltip("切换武器时收到画面下方")]
        public Vector3 loweredOffset = new Vector3(0.02f, -0.32f, -0.08f);
        public Vector3 loweredEuler = new Vector3(40f, 0f, 0f);
        [Tooltip("游泳时武器往下收一点，跟着划水摆动")]
        public Vector3 swimOffset = new Vector3(0.02f, -0.06f, -0.03f);

        [Header("挥砍（近战）")]
        public float swingTime = 0.36f;
        public Vector3 swingStartEuler = new Vector3(-35f, 35f, 60f);
        public Vector3 swingEndEuler = new Vector3(45f, -50f, -20f);
        public Vector3 swingOffset = new Vector3(-0.08f, 0.02f, 0.08f);

        Vector3 restPos;
        Quaternion restRot;
        Vector2 sway;
        float bobTimer;
        float poseSprint, poseReload, poseLowered, poseSwim;
        float swingT = 1f;
        // 后坐弹簧：x = 后移量, y = 上抬角度
        Vector2 recoil, recoilVel;

        public bool Reloading { get; set; }
        public bool Lowered { get; set; }
        public bool Swimming { get; set; }

        /// <summary>开始一次挥砍动画。</summary>
        public void Swing() => swingT = 0f;

        void Awake()
        {
            restPos = transform.localPosition;
            restRot = transform.localRotation;
        }

        public void Kick()
        {
            recoilVel += new Vector2(kickBack * 30f, kickUp * 30f);
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            Vector2 look = player != null ? player.LookDelta : Vector2.zero;
            Vector2 targetSway = Vector2.ClampMagnitude(-look * swayAmount, swayMax);
            sway = Vector2.Lerp(sway, targetSway, swaySmooth * dt);

            float speed01 = player != null && player.IsGrounded ? player.Speed01 : 0f;
            bobTimer += dt * bobSpeed * (0.4f + speed01);
            Vector3 bob = new Vector3(Mathf.Sin(bobTimer * 0.5f) * bobAmount, Mathf.Sin(bobTimer) * bobAmount * 0.6f, 0f) * (speed01 * 1.6f + 0.08f);

            Vector2 accel = -springStiffness * recoil - springDamping * recoilVel;
            recoilVel += accel * dt;
            recoil += recoilVel * dt;

            poseSprint = Mathf.MoveTowards(poseSprint, player != null && player.IsSprinting && !Reloading ? 1f : 0f, dt * 5f);
            poseReload = Mathf.MoveTowards(poseReload, Reloading ? 1f : 0f, dt * 5f);
            poseLowered = Mathf.MoveTowards(poseLowered, Lowered ? 1f : 0f, dt * 6f);
            poseSwim = Mathf.MoveTowards(poseSwim, Swimming ? 1f : 0f, dt * 3f);
            float s = Mathf.SmoothStep(0, 1, poseSprint);
            float r = Mathf.SmoothStep(0, 1, poseReload);
            float lo = Mathf.SmoothStep(0, 1, poseLowered);
            float sw = Mathf.SmoothStep(0, 1, poseSwim);
            // 挥砍：先蓄力抬到右上，再快速劈向左下，最后回位
            swingT = Mathf.Min(1f, swingT + dt / swingTime);
            float wind = swingT < 0.25f ? Mathf.SmoothStep(0, 1, swingT / 0.25f) : 1f;
            float strike = swingT < 0.25f ? 0f : Mathf.SmoothStep(0, 1, Mathf.Min(1f, (swingT - 0.25f) / 0.25f));
            float back = swingT < 0.6f ? 0f : Mathf.SmoothStep(0, 1, (swingT - 0.6f) / 0.4f);
            float swingW = (1f - back) * (swingT < 1f ? 1f : 0f);
            Vector3 swingEuler = Vector3.Lerp(Vector3.Lerp(Vector3.zero, swingStartEuler, wind), swingEndEuler, strike) * swingW;
            Vector3 swingPos = swingOffset * Mathf.Sin(strike * Mathf.PI) * swingW;
            Vector3 swimBob = new Vector3(Mathf.Sin(Time.time * 1.6f) * 0.01f, Mathf.Sin(Time.time * 3.2f) * 0.008f, 0f) * sw;

            transform.localPosition = restPos + bob
                + Vector3.back * recoil.x
                + sprintOffset * s + reloadOffset * r
                + loweredOffset * lo + swimOffset * sw + swimBob + swingPos;
            transform.localRotation = restRot
                * Quaternion.Euler(sway.y - recoil.y + sprintEuler.x * s + reloadEuler.x * r,
                                   sway.x + sprintEuler.y * s + reloadEuler.y * r,
                                   sway.x * 0.5f + sprintEuler.z * s + reloadEuler.z * r)
                * Quaternion.Euler(loweredEuler * lo + swingEuler);
        }
    }
}
