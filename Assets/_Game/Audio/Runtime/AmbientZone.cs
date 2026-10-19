using UnityEngine;
using Rishi.Core;
using Rishi.World;

namespace Rishi.Audio
{
    /// <summary>环境声区域（例如神殿内室）：玩家进入 / 离开时通知 AmbientController。需要 isTrigger 碰撞体。</summary>
    [RequireComponent(typeof(Collider))]
    public class AmbientZone : MonoBehaviour
    {
        public string zone = "temple";

        void OnTriggerEnter(Collider other)
        {
            if (GameSignals.IsPlayer(other)) AmbientController.Instance?.EnterZone(zone);
        }

        void OnTriggerExit(Collider other)
        {
            if (GameSignals.IsPlayer(other)) AmbientController.Instance?.ExitZone(zone);
        }
    }
}
