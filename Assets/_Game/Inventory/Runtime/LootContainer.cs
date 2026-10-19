using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rishi.Audio;
using Rishi.Core;

namespace Rishi.Inventory
{
    /// <summary>能打开的容器（木箱、巨型砗磲……）：第一次打开时把里面的东西放进背包，可选掀开盖子的动画。</summary>
    public class LootContainer : Interactable
    {
        public string displayName = "箱子";
        public string verb = "打开";
        public List<PlayerInventory.Stack> contents = new List<PlayerInventory.Stack>();
        public string openSound = "chest_open";
        [Header("盖子动画（可选）")]
        public Transform lid;
        public Vector3 lidOpenEuler = new Vector3(-75f, 0, 0);
        public float lidTime = 0.8f;
        [TextArea(1, 3)] public string openMessage;

        public bool Opened { get; private set; }

        public override string Prompt => Opened ? null : $"[E] {verb}：{displayName}";

        public override void Interact()
        {
            if (Opened || PlayerInventory.Instance == null) return;
            Opened = true;
            AudioManager.Play(openSound, transform.position);
            if (!string.IsNullOrEmpty(openMessage)) GameSignals.Toast(openMessage);
            if (lid != null) StartCoroutine(OpenLid());
            foreach (var s in contents)
                if (!s.IsEmpty) PlayerInventory.Instance.Add(s.item, s.count);
            if (contents.Count == 0) GameSignals.Toast("里面是空的");
        }

        IEnumerator OpenLid()
        {
            var from = lid.localRotation;
            var to = from * Quaternion.Euler(lidOpenEuler);
            for (float t = 0; t < 1f; t += Time.deltaTime / lidTime)
            {
                lid.localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0, 1, t));
                yield return null;
            }
            lid.localRotation = to;
        }
    }
}
