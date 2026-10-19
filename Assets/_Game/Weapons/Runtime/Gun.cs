using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Rishi.Audio;
using Rishi.Core;
using Rishi.Player;
using Rishi.World;

namespace Rishi.Weapons
{
    /// <summary>
    /// 射线枪（Hitscan）：左键射击，R 换弹。挂在主摄像机上，枪模在 WeaponHolder 下（武器层，单独的叠加摄像机渲染，不会穿墙）。
    /// </summary>
    public class Gun : MonoBehaviour
    {
        [Header("射击")]
        public bool automatic = false;
        public float fireRate = 7f;        // 每秒最多发数
        public float damage = 34f;
        public float range = 150f;
        public float impactForce = 6f;
        public float spread = 0.6f;        // 腰射散布（度）
        public int magazineSize = 12;
        public float reloadTime = 1.4f;

        [Header("后坐")]
        public float recoilUp = 1.6f;
        public float recoilSide = 0.5f;

        [Header("引用")]
        public WeaponFeel weaponFeel;
        public Transform muzzle;
        public Light muzzleLight;
        public Color tracerColor = new Color(1f, 0.8f, 0.5f, 0.6f);
        public LayerMask hitMask = ~0;

        public int Ammo { get; private set; }
        public bool IsReloading { get; private set; }
        public float SpreadNow { get; private set; }
        /// <summary>命中可伤害目标时触发，参数为是否击杀。</summary>
        public event Action<bool> OnHitTarget;

        PlayerController player;
        Camera cam;
        float nextFireTime;
        Material tracerMaterial;

        void Awake()
        {
            cam = GetComponent<Camera>();
            player = GetComponentInParent<PlayerController>();
            Ammo = magazineSize;
            tracerMaterial = new Material(Shader.Find("Sprites/Default"));
            if (muzzleLight != null) muzzleLight.enabled = false;
        }

        void Update()
        {
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            if (mouse == null || keyboard == null) return;

            float moveSpread = player != null ? player.Speed01 * 2.5f + (player.IsGrounded ? 0f : 3f) : 0f;
            SpreadNow = Mathf.Lerp(SpreadNow, spread + moveSpread, 10f * Time.deltaTime);

            if (IsReloading || GameSignals.GameplayBlocked || (player != null && (!player.IsCursorLocked || player.IsSprinting))) return;

            if (keyboard.rKey.wasPressedThisFrame && Ammo < magazineSize)
            {
                StartCoroutine(Reload());
                return;
            }

            bool trigger = automatic ? mouse.leftButton.isPressed : mouse.leftButton.wasPressedThisFrame;
            if (trigger && Time.time >= nextFireTime)
            {
                if (Ammo <= 0)
                {
                    AudioManager.Play2D("gun_empty");
                    StartCoroutine(Reload());
                    return;
                }
                nextFireTime = Time.time + 1f / fireRate;
                Shoot();
            }
        }

        void Shoot()
        {
            Ammo--;
            AudioManager.Play2D("gun_shot");
            weaponFeel?.Kick();
            player?.AddRecoil(recoilUp, UnityEngine.Random.Range(-recoilSide, recoilSide));
            SpreadNow += 1.2f;

            Vector3 dir = Quaternion.Euler(UnityEngine.Random.insideUnitCircle * SpreadNow) * cam.transform.forward;
            Ray ray = new Ray(cam.transform.position, dir);
            Vector3 end = ray.origin + ray.direction * range;

            if (Physics.Raycast(ray, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                var target = hit.collider.GetComponentInParent<IDamageable>();
                if (target != null)
                {
                    bool killed = target.TakeDamage(damage);
                    OnHitTarget?.Invoke(killed);
                }
                if (hit.rigidbody != null)
                    hit.rigidbody.AddForceAtPosition(ray.direction * impactForce, hit.point, ForceMode.Impulse);

                string surface = target != null || hit.collider.name.Contains("Barrel") ? "metal" : SurfaceTag.At(hit);
                AudioManager.Play(SurfaceTag.ImpactSound(surface), hit.point, 0.8f);
                if (FXManager.Instance != null)
                {
                    bool metal = surface == "metal";
                    FXManager.Instance.Impact(hit, metal);
                    if (target == null) FXManager.Instance.BulletHole(hit);
                }
            }

            if (FXManager.Instance != null && muzzle != null) FXManager.Instance.MuzzleFlash(muzzle.position, muzzle.forward);
            // 枪模由小 FOV 的武器摄像机渲染，弹道线从主摄像机视角下枪口大致所在的位置出发
            var t = cam.transform;
            Vector3 start = t.position + t.forward * 0.9f + t.right * 0.12f - t.up * 0.1f;
            StartCoroutine(Tracer(start, end));
            if (muzzleLight != null) StartCoroutine(FlashLight());
        }

        IEnumerator FlashLight()
        {
            muzzleLight.enabled = true;
            yield return new WaitForSeconds(0.04f);
            muzzleLight.enabled = false;
        }

        IEnumerator Tracer(Vector3 start, Vector3 end)
        {
            var go = new GameObject("Tracer");
            var line = go.AddComponent<LineRenderer>();
            line.material = tracerMaterial;
            line.startColor = tracerColor;
            line.endColor = new Color(tracerColor.r, tracerColor.g, tracerColor.b, 0f);
            line.startWidth = 0.012f;
            line.endWidth = 0.004f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.SetPositions(new[] { start, end });
            yield return new WaitForSeconds(0.03f);
            Destroy(go);
        }

        IEnumerator Reload()
        {
            IsReloading = true;
            AudioManager.Play2D("gun_reload");
            if (weaponFeel != null) weaponFeel.Reloading = true;
            yield return new WaitForSeconds(reloadTime);
            Ammo = magazineSize;
            IsReloading = false;
            if (weaponFeel != null) weaponFeel.Reloading = false;
        }
    }
}
