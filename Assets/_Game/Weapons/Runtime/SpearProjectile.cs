using System;
using UnityEngine;
using Rishi.Audio;
using Rishi.Core;
using Rishi.Inventory;

namespace Rishi.Weapons
{
    /// <summary>
    /// 鱼枪矛：实体弹道（空气里下坠，水里阻力大、下沉慢），逐帧射线检测，命中后插在目标上，变成可以「拔出」回收的物品。
    /// </summary>
    public class SpearProjectile : MonoBehaviour
    {
        public float airGravity = 9.81f;
        public float waterGravity = 1.6f;
        public float waterDrag = 1.1f;
        public float lifetime = 12f;

        public event Action<bool> OnHit;

        Vector3 velocity;
        WeaponDef def;
        LayerMask mask;
        float age;
        float startSpeed;
        bool stuck;
        bool wasUnderwater;

        public static SpearProjectile Launch(GameObject prefab, Vector3 position, Vector3 velocity, WeaponDef def, LayerMask mask)
        {
            GameObject go = prefab != null ? Instantiate(prefab) : CreateFallback();
            go.name = "Spear";
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(velocity));
            var s = go.GetComponent<SpearProjectile>() ?? go.AddComponent<SpearProjectile>();
            s.velocity = velocity;
            s.def = def;
            s.mask = mask;
            s.startSpeed = velocity.magnitude;
            s.wasUnderwater = position.y < GameSignals.SeaLevel;
            foreach (var c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
            var pickup = go.GetComponent<ItemPickup>();
            if (pickup != null) pickup.enabled = false;
            return s;
        }

        static GameObject CreateFallback()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.transform.localScale = new Vector3(0.012f, 0.6f, 0.012f);
            return go;
        }

        void Update()
        {
            if (stuck) return;
            float dt = Time.deltaTime;
            age += dt;
            bool underwater = transform.position.y < GameSignals.SeaLevel;
            if (underwater != wasUnderwater)
            {
                AudioManager.Play(underwater ? "splash_small" : "splash_small", transform.position, 0.5f);
                wasUnderwater = underwater;
            }
            if (underwater)
            {
                velocity *= Mathf.Exp(-waterDrag * dt);
                velocity += Vector3.down * waterGravity * dt;
            }
            else velocity += Vector3.down * airGravity * dt;

            Vector3 step = velocity * dt;
            if (step.sqrMagnitude > 1e-8f && Physics.Raycast(transform.position, step.normalized, out var hit, step.magnitude + 0.3f, mask, QueryTriggerInteraction.Ignore))
            {
                Stick(hit);
                return;
            }
            transform.position += step;
            if (velocity.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(velocity);
            if (age > lifetime) Destroy(gameObject);
        }

        void Stick(RaycastHit hit)
        {
            stuck = true;
            float speedFactor = Mathf.Clamp01(velocity.magnitude / Mathf.Max(startSpeed, 0.01f));
            var target = hit.collider.GetComponentInParent<IDamageable>();
            if (target != null && def != null)
                OnHit?.Invoke(target.TakeDamage(def.damage * Mathf.Lerp(0.3f, 1f, speedFactor)));
            if (hit.rigidbody != null) hit.rigidbody.AddForceAtPosition(velocity.normalized * 4f * speedFactor, hit.point, ForceMode.Impulse);

            string surface = target != null ? "flesh" : SurfaceTag.At(hit);
            AudioManager.Play(surface == "flesh" ? "spear_hit_flesh" : surface == "sand" || surface == "wetsand" ? "spear_hit_sand" : "spear_hit_hard", hit.point, 0.8f);

            // 扎进去一截；软的东西扎得深，石头上只卡住尖
            float depth = surface == "stone" || surface == "metal" ? 0.05f : 0.22f;
            transform.position = hit.point - velocity.normalized * (0.55f - depth);
            transform.SetParent(hit.collider.transform, true);

            foreach (var c in GetComponentsInChildren<Collider>()) { c.enabled = true; c.isTrigger = true; }
            var pickup = GetComponent<ItemPickup>() ?? gameObject.AddComponent<ItemPickup>();
            if (pickup.item == null && def != null) pickup.item = def.ammo;
            pickup.count = 1;
            pickup.verb = "拔出";
            pickup.enabled = true;
            if (GetComponentInChildren<Collider>() == null)
            {
                var box = gameObject.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(0.12f, 0.12f, 1.1f);
            }
        }
    }
}
