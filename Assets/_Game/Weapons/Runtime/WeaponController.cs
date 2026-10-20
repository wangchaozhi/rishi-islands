using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Rishi.Audio;
using Rishi.Core;
using Rishi.Inventory;
using Rishi.Player;

namespace Rishi.Weapons
{
    /// <summary>
    /// 武器控制：1/2/3 或滚轮切换，左键攻击，R 换弹。射线枪、近战、投射物三种武器共用一套流程。
    /// 背包里有对应物品才能装备，备用弹药从背包里扣。挂在主摄像机上，枪模在 WeaponHolder 下（武器层）。
    /// </summary>
    public class WeaponController : MonoBehaviour
    {
        [Serializable]
        public class Slot
        {
            public WeaponDef def;
            public GameObject viewModel;
            public Transform muzzle;
            [NonSerialized] public int loaded = -1;
        }

        public List<Slot> slots = new List<Slot>();
        public WeaponFeel weaponFeel;
        public Light muzzleLight;
        public Color tracerColor = new Color(1f, 0.8f, 0.5f, 0.6f);
        public LayerMask hitMask = ~0;
        [Tooltip("投射物（鱼枪矛）的模型")]
        public GameObject projectilePrefab;
        public float switchTime = 0.32f;

        public Slot Current => index >= 0 && index < slots.Count ? slots[index] : null;
        public int CurrentIndex => index;
        public bool IsReloading { get; private set; }
        public bool IsSwitching { get; private set; }
        public float SpreadNow { get; private set; }
        public int Loaded => Current != null ? Current.loaded : 0;
        public int Reserve => Current?.def.ammo != null && PlayerInventory.Instance != null ? PlayerInventory.Instance.Count(Current.def.ammo) : 0;
        /// <summary>当前武器在当前环境（水下 / 陆地）能不能用。</summary>
        public bool CanUseHere => Current != null && (GameSignals.HeadUnderwater ? Current.def.usableUnderwater : Current.def.usableOnLand);

        /// <summary>命中可伤害目标时触发，参数为是否击杀。</summary>
        public event Action<bool> OnHitTarget;

        PlayerController player;
        Camera cam;
        int index = -1;
        float nextFireTime;
        float lastWarnTime = -10f;
        Material tracerMaterial;

        void Awake()
        {
            cam = GetComponent<Camera>();
            player = GetComponentInParent<PlayerController>();
            tracerMaterial = new Material(Shader.Find("Sprites/Default"));
            if (muzzleLight != null) muzzleLight.enabled = false;
            foreach (var s in slots)
            {
                if (s.viewModel != null) s.viewModel.SetActive(false);
                s.loaded = s.def != null ? s.def.magazineSize : 0;
            }
        }

        void Start()
        {
            if (PlayerInventory.Instance != null) PlayerInventory.Instance.Changed += OnInventoryChanged;
            OnInventoryChanged();
        }

        void OnDestroy()
        {
            if (PlayerInventory.Instance != null) PlayerInventory.Instance.Changed -= OnInventoryChanged;
        }

        public bool Owns(int i) => i >= 0 && i < slots.Count && slots[i].def != null &&
                                   (slots[i].def.item == null || (PlayerInventory.Instance != null && PlayerInventory.Instance.Has(slots[i].def.item)));

        /// <summary>背包变化：当前武器被丢掉就收起，空手时捡到武器自动拿出来。</summary>
        void OnInventoryChanged()
        {
            if (index >= 0 && !Owns(index)) Equip(-1);
            if (index < 0)
                for (int i = 0; i < slots.Count; i++)
                    if (Owns(i)) { Equip(i); break; }
        }

        /// <summary>切换到第 i 把武器（-1 = 空手）。</summary>
        public void Equip(int i)
        {
            if (i == index || (i >= 0 && !Owns(i))) return;
            StopAllCoroutines();
            IsReloading = false;
            if (weaponFeel != null) weaponFeel.Reloading = false;
            StartCoroutine(SwitchTo(i));
        }

        IEnumerator SwitchTo(int i)
        {
            IsSwitching = true;
            if (Current?.viewModel != null && weaponFeel != null)
            {
                weaponFeel.Lowered = true;
                yield return new WaitForSeconds(switchTime * 0.5f);
            }
            if (Current?.viewModel != null) Current.viewModel.SetActive(false);
            index = i;
            if (Current != null)
            {
                if (Current.viewModel != null) Current.viewModel.SetActive(true);
                AudioManager.Play2D(Current.def.equipSound);
                if (weaponFeel != null) weaponFeel.Lowered = false;
                yield return new WaitForSeconds(switchTime * 0.5f);
            }
            IsSwitching = false;
        }

        void Update()
        {
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            if (mouse == null || keyboard == null) return;

            float moveSpread = player != null ? player.Speed01 * 2.5f + (player.IsGrounded || GameSignals.Swimming ? 0f : 3f) : 0f;
            float baseSpread = Current != null ? Current.def.spread : 0f;
            SpreadNow = Mathf.Lerp(SpreadNow, baseSpread + moveSpread, 10f * Time.deltaTime);

            if (weaponFeel != null) weaponFeel.Swimming = GameSignals.Swimming;
            if (GameSignals.GameplayBlocked || (player != null && !player.IsCursorLocked)) return;
            HandleSwitchInput(keyboard, mouse);
            if (Current == null || IsReloading || IsSwitching || (player != null && player.IsSprinting)) return;

            if (keyboard.rKey.wasPressedThisFrame && CanReload())
            {
                StartCoroutine(Reload());
                return;
            }

            var def = Current.def;
            bool trigger = def.automatic ? mouse.leftButton.isPressed : mouse.leftButton.wasPressedThisFrame;
            if (!trigger || Time.time < nextFireTime) return;

            if (!CanUseHere)
            {
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    AudioManager.Play2D(def.emptySound);
                    if (Time.time - lastWarnTime > 4f)
                    {
                        lastWarnTime = Time.time;
                        GameSignals.Toast(GameSignals.HeadUnderwater ? $"{def.displayName}在水下用不了" : $"{def.displayName}只能在水下用");
                    }
                }
                return;
            }

            if (def.kind != WeaponKind.Melee && Current.loaded <= 0)
            {
                if (mouse.leftButton.wasPressedThisFrame) AudioManager.Play2D(def.emptySound);
                if (CanReload()) StartCoroutine(Reload());
                nextFireTime = Time.time + 0.25f;
                return;
            }

            nextFireTime = Time.time + 1f / def.fireRate;
            switch (def.kind)
            {
                case WeaponKind.Hitscan: FireHitscan(def); break;
                case WeaponKind.Melee: StartCoroutine(Swing(def)); break;
                case WeaponKind.Projectile: FireProjectile(def); break;
            }
        }

        void HandleSwitchInput(Keyboard keyboard, Mouse mouse)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) Equip(0);
            else if (keyboard.digit2Key.wasPressedThisFrame) Equip(1);
            else if (keyboard.digit3Key.wasPressedThisFrame) Equip(2);
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.1f && slots.Count > 0)
            {
                int dir = scroll > 0 ? -1 : 1;
                for (int k = 1; k <= slots.Count; k++)
                {
                    int i = ((index < 0 ? 0 : index) + dir * k + slots.Count * 4) % slots.Count;
                    if (Owns(i)) { Equip(i); break; }
                }
            }
        }

        bool CanReload() => Current != null && Current.def.kind != WeaponKind.Melee && Current.loaded < Current.def.magazineSize && Reserve > 0;

        Ray AimRay(float spreadDeg)
        {
            Vector3 dir = Quaternion.Euler(UnityEngine.Random.insideUnitCircle * spreadDeg) * cam.transform.forward;
            return new Ray(cam.transform.position, dir);
        }

        /// <summary>武器由小 FOV 的武器摄像机渲染，弹道 / 投射物从主摄像机视角下枪口大致所在的位置出发。</summary>
        Vector3 ApproxMuzzle(float forward = 0.9f)
        {
            var t = cam.transform;
            return t.position + t.forward * forward + t.right * 0.12f - t.up * 0.1f;
        }

        void Kick(WeaponDef def)
        {
            weaponFeel?.Kick();
            player?.AddRecoil(def.recoilUp, UnityEngine.Random.Range(-def.recoilSide, def.recoilSide));
        }

        void FireHitscan(WeaponDef def)
        {
            Current.loaded--;
            AudioManager.Play2D(def.fireSound);
            Kick(def);
            SpreadNow += 1.2f;

            var ray = AimRay(SpreadNow);
            Vector3 end = ray.origin + ray.direction * def.range;
            if (Physics.Raycast(ray, out RaycastHit hit, def.range, hitMask, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                var target = ApplyHit(hit, def.damage, ray.direction * def.impactForce);
                string surface = target != null || hit.collider.name.Contains("Barrel") ? "metal" : SurfaceTag.At(hit);
                AudioManager.Play(SurfaceTag.ImpactSound(surface), hit.point, 0.8f);
                if (FXManager.Instance != null)
                {
                    FXManager.Instance.Impact(hit, surface == "metal");
                    if (target == null) FXManager.Instance.BulletHole(hit);
                }
            }

            if (FXManager.Instance != null && Current.muzzle != null) FXManager.Instance.MuzzleFlash(Current.muzzle.position, Current.muzzle.forward);
            StartCoroutine(Tracer(ApproxMuzzle(), end));
            if (muzzleLight != null) StartCoroutine(FlashLight());
        }

        IDamageable ApplyHit(RaycastHit hit, float damage, Vector3 force)
        {
            var target = GameSignals.IsPlayer(hit.collider) ? null : hit.collider.GetComponentInParent<IDamageable>(); // 玩家自己也是 IDamageable，打不到自己
            if (target != null)
            {
                bool killed = target.TakeDamage(damage);
                OnHitTarget?.Invoke(killed);
            }
            if (hit.rigidbody != null) hit.rigidbody.AddForceAtPosition(force, hit.point, ForceMode.Impulse);
            return target;
        }

        IEnumerator Swing(WeaponDef def)
        {
            weaponFeel?.Swing();
            AudioManager.Play2D(def.fireSound);
            yield return new WaitForSeconds(def.meleeHitDelay);
            var ray = new Ray(cam.transform.position, cam.transform.forward);
            if (Physics.SphereCast(ray, def.meleeRadius, out var hit, def.range, hitMask, QueryTriggerInteraction.Ignore))
            {
                var target = ApplyHit(hit, def.damage, ray.direction * def.impactForce);
                string surface = target != null ? "flesh" : SurfaceTag.At(hit);
                AudioManager.Play(surface == "flesh" ? "machete_hit_flesh" : surface == "wood" || surface == "leaves" || surface == "grass" ? "machete_hit_wood" : "machete_hit_stone", hit.point, 0.9f);
                player?.AddRecoil(1.2f, UnityEngine.Random.Range(-0.8f, 0.8f));
                if (FXManager.Instance != null && target == null) FXManager.Instance.Impact(hit, surface == "stone");
            }
        }

        void FireProjectile(WeaponDef def)
        {
            Current.loaded--;
            AudioManager.Play2D(def.fireSound);
            Kick(def);
            if (Current.viewModel != null)
            {
                // 枪模上的矛射出去了，换弹后再显示
                var rod = Current.viewModel.transform.Find("LoadedSpear");
                if (rod != null) rod.gameObject.SetActive(false);
            }
            var ray = AimRay(def.spread);
            // 起点往后收一点，免得贴着墙射的时候直接穿过去
            Vector3 start = ApproxMuzzle(0.5f);
            Vector3 aimPoint = Physics.Raycast(ray, out var aimHit, 200f, hitMask, QueryTriggerInteraction.Ignore) ? aimHit.point : ray.GetPoint(200f);
            Vector3 dir = (aimPoint - start).normalized;
            float speed = GameSignals.HeadUnderwater ? def.projectileSpeedUnderwater : def.projectileSpeed;
            var spear = SpearProjectile.Launch(projectilePrefab, start, dir * speed, def, hitMask);
            spear.OnHit += killed => OnHitTarget?.Invoke(killed);
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
            var slot = Current;
            IsReloading = true;
            AudioManager.Play2D(slot.def.reloadSound);
            if (weaponFeel != null) weaponFeel.Reloading = true;
            yield return new WaitForSeconds(slot.def.reloadTime);
            int need = slot.def.magazineSize - slot.loaded;
            int got = PlayerInventory.Instance != null ? PlayerInventory.Instance.Remove(slot.def.ammo, need) : 0;
            slot.loaded += got;
            if (slot.viewModel != null && slot.loaded > 0)
            {
                var rod = slot.viewModel.transform.Find("LoadedSpear");
                if (rod != null) rod.gameObject.SetActive(true);
            }
            IsReloading = false;
            if (weaponFeel != null) weaponFeel.Reloading = false;
        }
    }
}
