using UnityEngine;
using Rishi.Core;

namespace Rishi.Inventory
{
    /// <summary>场景里可以捡起的物品：对准按 E 放进背包。</summary>
    public class ItemPickup : Interactable
    {
        public ItemDef item;
        [Min(1)] public int count = 1;
        [Tooltip("捡起时的动作，比如「捡起」「拔出」")]
        public string verb = "拾取";
        [Tooltip("轻微上下浮动和旋转，提示这是可拾取的东西")]
        public bool bob;

        Vector3 basePos;

        public override string Prompt => item == null ? null : count > 1 ? $"[E] {verb}：{item.displayName} ×{count}" : $"[E] {verb}：{item.displayName}";

        void Start() => basePos = transform.position;

        void Update()
        {
            if (!bob) return;
            transform.position = basePos + Vector3.up * (Mathf.Sin(Time.time * 2f) * 0.04f);
            transform.Rotate(Vector3.up, 30f * Time.deltaTime, Space.World);
        }

        public override void Interact()
        {
            var inv = PlayerInventory.Instance;
            if (inv == null || item == null) return;
            int added = inv.Add(item, count);
            count -= added;
            if (count <= 0) Destroy(gameObject);
        }

        /// <summary>在地上生成一个通用的物品包（丢弃物品时用）。</summary>
        public static ItemPickup Spawn(ItemDef item, int count, Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Dropped_" + item.id;
            go.transform.position = position + Vector3.up * 0.1f;
            go.transform.localScale = new Vector3(0.28f, 0.18f, 0.22f);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.SetColor("_BaseColor", Color.Lerp(item.iconColor, new Color(0.45f, 0.36f, 0.24f), 0.5f));
                mat.SetFloat("_Smoothness", 0.25f);
                go.GetComponent<Renderer>().sharedMaterial = mat;
            }
            var p = go.AddComponent<ItemPickup>();
            p.item = item;
            p.count = count;
            return p;
        }
    }
}
