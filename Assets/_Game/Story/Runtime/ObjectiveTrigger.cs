using UnityEngine;
using Rishi.Audio;
using Rishi.Core;
using Rishi.World;

namespace Rishi.Story
{
    /// <summary>玩家走进触发区时推进目标（例如发现神殿）。需要一个 isTrigger 的碰撞体。</summary>
    [RequireComponent(typeof(Collider))]
    public class ObjectiveTrigger : MonoBehaviour
    {
        public int objectiveStage;
        public string objectiveText;
        public string toast;

        void OnTriggerEnter(Collider other)
        {
            if (!GameSignals.IsPlayer(other) || GameState.Instance == null) return;
            if (!string.IsNullOrEmpty(toast) && GameState.Instance.ObjectiveStage < objectiveStage) GameState.Instance.Toast(toast);
            GameState.Instance.SetObjective(objectiveStage, objectiveText);
        }
    }
}
