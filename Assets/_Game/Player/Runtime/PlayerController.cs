using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Rishi.Audio;
using Rishi.Core;
using Rishi.World;

namespace Rishi.Player
{
    /// <summary>
    /// 第一人称移动与视角：WASD 移动，Shift 冲刺，空格跳跃，鼠标转视角，Esc 释放鼠标。
    /// 水深没过胸口时自动转为游泳：水面漂浮，沿视线方向游动，Ctrl / C 下潜，空格上浮。
    /// 附带镜头晃动（走路起伏、落地下沉、水面起伏）和后坐力上抬。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("移动")]
        public float walkSpeed = 4.5f;
        public float sprintSpeed = 7.5f;
        public float acceleration = 12f;
        public float airControl = 0.3f;
        public float jumpHeight = 1.1f;
        public float gravity = -22f;

        [Header("视角")]
        public Transform cameraRoot;
        public float mouseSensitivity = 0.08f;
        public float maxPitch = 85f;

        [Header("镜头晃动")]
        public float bobAmplitude = 0.035f;
        public float bobFrequency = 1.9f;
        public float landingDip = 0.12f;

        [Header("后坐力恢复")]
        public float recoilReturnSpeed = 7f;
        public float recoilSnappiness = 18f;

        [Header("游泳")]
        public float swimSpeed = 2.4f;
        public float swimSprintSpeed = 4.0f;
        public float verticalSwimSpeed = 2.0f;
        [Tooltip("水的惯性：越小越「拖」")]
        public float swimAcceleration = 2.2f;
        [Tooltip("脚下水深超过这个值开始游泳（米）")]
        public float swimEnterDepth = 1.3f;
        [Tooltip("脚下水深小于这个值并且踩到底就上岸走路")]
        public float swimExitDepth = 1.05f;
        [Tooltip("在水面漂浮时眼睛高出水面多少")]
        public float floatEyeHeight = 0.16f;
        public float strokeInterval = 0.95f;

        CharacterController controller;
        Vector3 velocity;
        Vector3 planarVelocity;
        Vector3 swimVelocity;
        float pitch;
        Vector3 cameraBasePos;
        float bobTimer;
        float landOffset;
        bool wasGrounded;
        Vector2 recoilTarget;
        Vector2 recoilCurrent;
        float strokeTimer;
        float safeTimer;
        bool headWasUnder;

        public bool IsCursorLocked => Cursor.lockState == CursorLockMode.Locked;
        public bool IsGrounded => controller.isGrounded;
        public bool IsSprinting { get; private set; }
        public bool IsSwimming { get; private set; }
        /// <summary>眼睛在水面以下。</summary>
        public bool IsHeadUnderwater { get; private set; }
        /// <summary>当前水平速度占冲刺速度的比例（0~1），给武器晃动用。</summary>
        public float Speed01 => Mathf.Clamp01((IsSwimming ? swimVelocity.magnitude : planarVelocity.magnitude) / sprintSpeed);
        public Vector2 LookDelta { get; private set; }
        /// <summary>最近一次站在干燥陆地上的位置（溺水昏迷后在这里醒来）。</summary>
        public Vector3 LastSafeLand { get; private set; }
        /// <summary>开始 / 结束游泳。</summary>
        public event Action<bool> SwimStateChanged;

        float FeetY => transform.position.y + controller.center.y - controller.height / 2f;
        float EyeY => cameraRoot.position.y;

        void OnEnable() => GameSignals.RegisterPlayer(transform);
        void OnDisable() => GameSignals.UnregisterPlayer(transform);

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            cameraBasePos = cameraRoot.localPosition;
            LastSafeLand = transform.position;
        }

        void Start()
        {
            LockCursor(true);
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null) return;

            bool blocked = GameSignals.GameplayBlocked; // 阅读笔记 / 查看日志 / 背包 / 过场时
            if (!blocked && keyboard.escapeKey.wasPressedThisFrame) LockCursor(false);
            else if (!blocked && !IsCursorLocked && mouse.leftButton.wasPressedThisFrame) LockCursor(true);

            LookDelta = IsCursorLocked && !blocked ? mouse.delta.ReadValue() * mouseSensitivity : Vector2.zero;
            Look();
            UpdateWaterState();
            if (IsSwimming) Swim(keyboard, blocked);
            else Move(keyboard, blocked);
            UpdateCameraOffset();
            PublishWaterSignals();
        }

        /// <summary>开枪时调用：up 为上抬角度，side 为左右随机偏移角度。</summary>
        public void AddRecoil(float up, float side)
        {
            recoilTarget += new Vector2(up, side);
        }

        /// <summary>瞬移（昏迷醒来、剧情传送），清空速度。</summary>
        public void Teleport(Vector3 position, float yaw)
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            controller.enabled = true;
            velocity = planarVelocity = swimVelocity = Vector3.zero;
            SetSwimming(false);
            UpdateWaterState();
            PublishWaterSignals();
        }

        void Look()
        {
            recoilTarget = Vector2.Lerp(recoilTarget, Vector2.zero, recoilReturnSpeed * Time.deltaTime);
            recoilCurrent = Vector2.Lerp(recoilCurrent, recoilTarget, recoilSnappiness * Time.deltaTime);

            transform.Rotate(Vector3.up, LookDelta.x);
            pitch = Mathf.Clamp(pitch - LookDelta.y, -maxPitch, maxPitch);
            cameraRoot.localRotation = Quaternion.Euler(pitch - recoilCurrent.x, recoilCurrent.y, 0f);
        }

        Vector2 ReadMoveInput(Keyboard keyboard, bool blocked)
        {
            Vector2 input = Vector2.zero;
            if (blocked) return input;
            if (keyboard.wKey.isPressed) input.y += 1;
            if (keyboard.sKey.isPressed) input.y -= 1;
            if (keyboard.dKey.isPressed) input.x += 1;
            if (keyboard.aKey.isPressed) input.x -= 1;
            return Vector2.ClampMagnitude(input, 1f);
        }

        void UpdateWaterState()
        {
            float depth = GameSignals.SeaLevel - FeetY;
            if (!IsSwimming && depth > swimEnterDepth) SetSwimming(true);
            else if (IsSwimming && depth < swimExitDepth && GroundBelow(0.35f)) SetSwimming(false);
        }

        void SetSwimming(bool swimming)
        {
            if (IsSwimming == swimming) return;
            IsSwimming = swimming;
            if (swimming)
            {
                // 带着落水的速度进水，大幅减速
                swimVelocity = (planarVelocity + Vector3.up * velocity.y) * 0.35f;
                AudioManager.Play(velocity.y < -6f ? "splash_big" : "splash_enter", transform.position, 0.9f);
            }
            else
            {
                planarVelocity = Vector3.ProjectOnPlane(swimVelocity, Vector3.up);
                velocity = Vector3.zero;
                AudioManager.Play("water_exit", transform.position, 0.7f);
            }
            SwimStateChanged?.Invoke(swimming);
        }

        bool GroundBelow(float extra) =>
            Physics.SphereCast(transform.position, controller.radius * 0.9f, Vector3.down, out _, controller.height / 2f - controller.radius + extra, ~0, QueryTriggerInteraction.Ignore);

        void Move(Keyboard keyboard, bool blocked)
        {
            Vector2 input = ReadMoveInput(keyboard, blocked);

            IsSprinting = keyboard.leftShiftKey.isPressed && input.y > 0.1f && !blocked;
            float speed = IsSprinting ? sprintSpeed : walkSpeed;
            // 涉水时变慢（水越深越慢）
            float wade = Mathf.InverseLerp(0.2f, swimEnterDepth, GameSignals.SeaLevel - FeetY);
            speed *= Mathf.Lerp(1f, 0.55f, wade);
            Vector3 target = (transform.right * input.x + transform.forward * input.y) * speed;
            float accel = controller.isGrounded ? acceleration : acceleration * airControl;
            planarVelocity = Vector3.MoveTowards(planarVelocity, target, accel * speed * Time.deltaTime);

            if (controller.isGrounded)
            {
                if (!wasGrounded && velocity.y < -6f) landOffset = -landingDip;
                if (velocity.y < 0) velocity.y = -2f;
                if (!blocked && keyboard.spaceKey.wasPressedThisFrame)
                    velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

                safeTimer -= Time.deltaTime;
                if (safeTimer <= 0f && FeetY > GameSignals.SeaLevel + 0.4f)
                {
                    safeTimer = 1f;
                    LastSafeLand = transform.position;
                }
            }
            wasGrounded = controller.isGrounded;
            velocity.y += gravity * Time.deltaTime;

            controller.Move((planarVelocity + Vector3.up * velocity.y) * Time.deltaTime);
        }

        void Swim(Keyboard keyboard, bool blocked)
        {
            float dt = Time.deltaTime;
            Vector2 input = ReadMoveInput(keyboard, blocked);
            IsSprinting = false; // 游泳时不进入冲刺姿态（武器不收起），但 Shift 仍然加速
            bool fast = !blocked && keyboard.leftShiftKey.isPressed && input.y > 0.1f;
            float speed = fast ? swimSprintSpeed : swimSpeed;

            float surfaceEye = GameSignals.SeaLevel + floatEyeHeight;
            bool atSurface = EyeY > surfaceEye - 0.35f;
            Vector3 forward = cameraRoot.forward;
            // 在水面上看着天空往前游时不往上飞
            if (atSurface && forward.y > 0f) forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            Vector3 wish = (cameraRoot.right * input.x + forward * input.y) * speed;

            float vertical = 0f;
            if (!blocked && keyboard.spaceKey.isPressed) vertical += 1f;
            if (!blocked && (keyboard.leftCtrlKey.isPressed || keyboard.cKey.isPressed)) vertical -= 1f;
            wish += Vector3.up * vertical * verticalSwimSpeed;

            swimVelocity = Vector3.MoveTowards(swimVelocity, wish, swimAcceleration * Mathf.Max(speed, 1f) * dt);

            // 浮力：靠近水面时慢慢浮上去漂着；深处接近中性浮力，略微下沉
            float eyeError = surfaceEye - EyeY;
            if (vertical == 0f && input.y <= 0.1f)
            {
                if (eyeError < 1.6f) swimVelocity.y = Mathf.MoveTowards(swimVelocity.y, Mathf.Clamp(eyeError * 1.6f, -0.8f, 0.7f), dt * 2.5f);
                else swimVelocity.y = Mathf.MoveTowards(swimVelocity.y, -0.12f, dt * 0.6f);
            }
            // 不能游出水面
            if (eyeError < 0f && swimVelocity.y > 0f) swimVelocity.y = Mathf.Min(swimVelocity.y, eyeError * 4f);

            controller.Move(swimVelocity * dt);

            // 划水声：水面上哗啦声，水下闷一些
            if (input.sqrMagnitude > 0.01f || vertical != 0f)
            {
                strokeTimer -= dt * (fast ? 1.5f : 1f);
                if (strokeTimer <= 0f)
                {
                    strokeTimer = strokeInterval;
                    AudioManager.Play2D(IsHeadUnderwater ? "swim_under" : "swim_stroke", fast ? 0.9f : 0.65f);
                }
            }
            else strokeTimer = Mathf.Min(strokeTimer, 0.2f);
        }

        void PublishWaterSignals()
        {
            IsHeadUnderwater = EyeY < GameSignals.SeaLevel - 0.03f;
            if (IsHeadUnderwater != headWasUnder)
            {
                AudioManager.Play2D(IsHeadUnderwater ? "dive_under" : "surface_breach", 0.7f);
                headWasUnder = IsHeadUnderwater;
            }
            GameSignals.Swimming = IsSwimming;
            GameSignals.HeadUnderwater = IsHeadUnderwater;
            GameSignals.CameraDepth = Mathf.Max(0f, GameSignals.SeaLevel - EyeY);
        }

        void UpdateCameraOffset()
        {
            float speed = planarVelocity.magnitude;
            Vector3 bob = Vector3.zero;
            if (IsSwimming)
            {
                // 水面上随波起伏，水下缓慢漂动
                float amp = IsHeadUnderwater ? 0.02f : 0.07f;
                bob = new Vector3(Mathf.Sin(Time.time * 0.7f) * amp * 0.4f, Mathf.Sin(Time.time * 1.1f) * amp, 0f);
            }
            else if (controller.isGrounded && speed > 0.5f)
            {
                bobTimer += speed * bobFrequency * Time.deltaTime;
                float amp = bobAmplitude * Mathf.Clamp01(speed / walkSpeed);
                bob = new Vector3(Mathf.Cos(bobTimer) * amp * 0.5f, Mathf.Abs(Mathf.Sin(bobTimer)) * amp, 0f);
            }
            landOffset = Mathf.Lerp(landOffset, 0f, 8f * Time.deltaTime);
            cameraRoot.localPosition = Vector3.Lerp(cameraRoot.localPosition, cameraBasePos + bob + Vector3.up * landOffset, 20f * Time.deltaTime);
        }

        void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
