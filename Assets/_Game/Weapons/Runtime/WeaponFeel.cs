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

        Vector3 restPos;
        Quaternion restRot;
        Vector2 sway;
        float bobTimer;
        float poseSprint, poseReload;
        // 后坐弹簧：x = 后移量, y = 上抬角度
        Vector2 recoil, recoilVel;

        public bool Reloading { get; set; }

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
            float s = Mathf.SmoothStep(0, 1, poseSprint);
            float r = Mathf.SmoothStep(0, 1, poseReload);

            transform.localPosition = restPos + bob
                + Vector3.back * recoil.x
                + sprintOffset * s + reloadOffset * r;
            transform.localRotation = restRot
                * Quaternion.Euler(sway.y - recoil.y + sprintEuler.x * s + reloadEuler.x * r,
                                   sway.x + sprintEuler.y * s + reloadEuler.y * r,
                                   sway.x * 0.5f + sprintEuler.z * s + reloadEuler.z * r);
        }
    }
}
