using UnityEngine;
using Rishi.Audio;
using Rishi.Core;

namespace Rishi.Story
{
    /// <summary>
    /// 林边的人影：穿 18 世纪水手服的彼得（船长日志里走进海里的那个水手）。
    /// 一动不动地站着看玩家；被手电照到、或玩家走近到 vanishDistance 以内，就在一声低语里消失。
    /// </summary>
    public class Peter : MonoBehaviour
    {
        public float vanishDistance = 9f;
        public bool Vanished { get; private set; }

        void Update()
        {
            var p = GameSignals.Player;
            if (Vanished || p == null) return;
            var look = p.position - transform.position; look.y = 0f;
            if (look.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), Time.deltaTime * 2f);

            float dist = Vector3.Distance(p.position, transform.position);
            bool lit = false;
            var beam = GameSignals.Flashlight;
            if (beam != null && beam.enabled && GameSignals.FlashlightOn)
                lit = InBeam(beam.transform.position, beam.transform.forward, transform.position + Vector3.up, beam.range, beam.spotAngle * 0.5f);
            if (ShouldVanish(lit, dist, vanishDistance)) Vanish();
        }

        /// <summary>照到或靠得太近就消失。</summary>
        public static bool ShouldVanish(bool lit, float distance, float vanishDistance) => lit || distance < vanishDistance;

        public static bool InBeam(Vector3 origin, Vector3 forward, Vector3 point, float range, float halfAngle)
        {
            var d = point - origin;
            return d.magnitude <= range && Vector3.Angle(forward, d) <= halfAngle;
        }

        void Vanish()
        {
            Vanished = true;
            AudioManager.Play("peter_vanish", transform.position + Vector3.up * 1.4f, 1f);
            GameSignals.AddFear(0.25f);
            GameSignals.Toast("……刚才那里明明站着一个人。");
            gameObject.SetActive(false);
        }
    }
}
