using UnityEngine;
using UnityEngine.InputSystem;

namespace Rishi.Core
{
    /// <summary>挂在主摄像机上：射线检测前方 3 米内的 Interactable，按 E 互动。</summary>
    public class PlayerInteractor : MonoBehaviour
    {
        public float range = 3f;
        public LayerMask mask = ~0;

        public Interactable Current { get; private set; }

        void Update()
        {
            Current = null;
            if (GameSignals.GameplayBlocked || GameSignals.InVehicle) return;

            var ray = new Ray(transform.position, transform.forward);
            if (Physics.Raycast(ray, out var hit, range, mask, QueryTriggerInteraction.Collide))
            {
                var it = hit.collider.GetComponentInParent<Interactable>();
                if (it != null && it.enabled && it.Prompt != null) Current = it;
            }

            var kb = Keyboard.current;
            if (Current != null && kb != null && kb.eKey.wasPressedThisFrame)
                Current.Interact();
        }
    }
}
