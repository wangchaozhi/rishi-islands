using UnityEngine;
using UnityEngine.InputSystem;
using Rishi.Audio;
using Rishi.Core;
using Rishi.World;

namespace Rishi.Player
{
    /// <summary>
    /// 第一人称移动与视角：WASD 移动，Shift 冲刺，空格跳跃，鼠标转视角，Esc 释放鼠标。
    /// 附带镜头晃动（走路起伏、落地下沉）和后坐力上抬。
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

        CharacterController controller;
        Vector3 velocity;
        Vector3 planarVelocity;
        float pitch;
        Vector3 cameraBasePos;
        float bobTimer;
        float landOffset;
        bool wasGrounded;
        Vector2 recoilTarget;
        Vector2 recoilCurrent;

        public bool IsCursorLocked => Cursor.lockState == CursorLockMode.Locked;
        public bool IsGrounded => controller.isGrounded;
        public bool IsSprinting { get; private set; }
        /// <summary>当前水平速度占冲刺速度的比例（0~1），给武器晃动用。</summary>
        public float Speed01 => Mathf.Clamp01(planarVelocity.magnitude / sprintSpeed);
        public Vector2 LookDelta { get; private set; }

        void OnEnable() => GameSignals.RegisterPlayer(transform);
        void OnDisable() => GameSignals.UnregisterPlayer(transform);

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            cameraBasePos = cameraRoot.localPosition;
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

            bool blocked = GameSignals.GameplayBlocked; // 阅读笔记 / 查看日志 / 过场时
            if (!blocked && keyboard.escapeKey.wasPressedThisFrame) LockCursor(false);
            else if (!IsCursorLocked && mouse.leftButton.wasPressedThisFrame) LockCursor(true);

            LookDelta = IsCursorLocked && !blocked ? mouse.delta.ReadValue() * mouseSensitivity : Vector2.zero;
            Look();
            Move(keyboard, blocked);
            UpdateCameraOffset();
        }

        /// <summary>开枪时调用：up 为上抬角度，side 为左右随机偏移角度。</summary>
        public void AddRecoil(float up, float side)
        {
            recoilTarget += new Vector2(up, side);
        }

        void Look()
        {
            recoilTarget = Vector2.Lerp(recoilTarget, Vector2.zero, recoilReturnSpeed * Time.deltaTime);
            recoilCurrent = Vector2.Lerp(recoilCurrent, recoilTarget, recoilSnappiness * Time.deltaTime);

            transform.Rotate(Vector3.up, LookDelta.x);
            pitch = Mathf.Clamp(pitch - LookDelta.y, -maxPitch, maxPitch);
            cameraRoot.localRotation = Quaternion.Euler(pitch - recoilCurrent.x, recoilCurrent.y, 0f);
        }

        void Move(Keyboard keyboard, bool blocked)
        {
            Vector2 input = Vector2.zero;
            if (!blocked)
            {
                if (keyboard.wKey.isPressed) input.y += 1;
                if (keyboard.sKey.isPressed) input.y -= 1;
                if (keyboard.dKey.isPressed) input.x += 1;
                if (keyboard.aKey.isPressed) input.x -= 1;
            }
            input = Vector2.ClampMagnitude(input, 1f);

            IsSprinting = keyboard.leftShiftKey.isPressed && input.y > 0.1f;
            float speed = IsSprinting ? sprintSpeed : walkSpeed;
            Vector3 target = (transform.right * input.x + transform.forward * input.y) * speed;
            float accel = controller.isGrounded ? acceleration : acceleration * airControl;
            planarVelocity = Vector3.MoveTowards(planarVelocity, target, accel * speed * Time.deltaTime);

            if (controller.isGrounded)
            {
                if (!wasGrounded && velocity.y < -6f) landOffset = -landingDip;
                if (velocity.y < 0) velocity.y = -2f;
                if (!blocked && keyboard.spaceKey.wasPressedThisFrame)
                    velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
            wasGrounded = controller.isGrounded;
            velocity.y += gravity * Time.deltaTime;

            controller.Move((planarVelocity + Vector3.up * velocity.y) * Time.deltaTime);
        }

        void UpdateCameraOffset()
        {
            float speed = planarVelocity.magnitude;
            Vector3 bob = Vector3.zero;
            if (controller.isGrounded && speed > 0.5f)
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
