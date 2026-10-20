using UnityEngine;
using Rishi.Audio;
using Rishi.Core;
using Rishi.World;

namespace Rishi.Player
{
    /// <summary>按走过的距离播放脚步声，地表类型自动判断（地形图层 / SurfaceTag / 浅水）；落地时播放一次重脚步。</summary>
    [RequireComponent(typeof(CharacterController))]
    public class Footsteps : MonoBehaviour
    {
        public float walkStride = 1.7f;
        public float sprintStride = 2.4f;
        public float seaLevel = 0f;
        [Tooltip("脚踝没入海面多深算涉水")]
        public float wadeDepth = 0.15f;

        CharacterController cc;
        PlayerController player;
        float distance;
        bool wasGrounded = true;
        float airTime;

        public string CurrentSurface { get; private set; } = "sand";

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            player = GetComponent<PlayerController>();
        }

        void Update()
        {
            Vector3 v = cc.velocity;
            v.y = 0;
            bool grounded = cc.isGrounded;
            if (player != null && player.IsSwimming) { distance = 0; wasGrounded = true; airTime = 0; return; } // 游泳时没有脚步声

            if (grounded)
            {
                distance += v.magnitude * Time.deltaTime;
                float stride = player != null && player.IsSprinting ? sprintStride : walkStride;
                if (distance >= stride && v.magnitude > 0.5f)
                {
                    distance = 0;
                    Step(0.55f + Mathf.Clamp01(v.magnitude / 8f) * 0.45f);
                }
                if (!wasGrounded && airTime > 0.35f) Step(1.2f);
                airTime = 0;
            }
            else airTime += Time.deltaTime;
            wasGrounded = grounded;
        }

        void Step(float volume)
        {
            CurrentSurface = DetectSurface();
            Vector3 feet = transform.position + Vector3.down * (cc.height / 2f);
            AudioManager.Play("step_" + CurrentSurface, feet, volume);
            GameSignals.MakeNoise(feet, player != null && player.IsSprinting ? 12f : 4f); // 冲刺的脚步声更容易被听见
        }

        string DetectSurface()
        {
            Vector3 feet = transform.position + Vector3.down * (cc.height / 2f - cc.radius);
            if (feet.y - cc.radius < seaLevel - wadeDepth) return "water";
            if (Physics.Raycast(feet + Vector3.up * 0.1f, Vector3.down, out var hit, cc.radius + 0.8f, ~0, QueryTriggerInteraction.Ignore))
                return SurfaceTag.At(hit);
            return CurrentSurface;
        }
    }
}
