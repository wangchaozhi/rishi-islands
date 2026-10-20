using UnityEngine;

namespace Rishi.Creatures
{
    /// <summary>感知用的几何判断（纯函数，方便测试）。</summary>
    public static class Perception
    {
        /// <summary>target 是否在以 origin 为顶点、朝 forward、半角 halfAngle 度、长 range 的锥里。</summary>
        public static bool InCone(Vector3 origin, Vector3 forward, Vector3 target, float range, float halfAngle)
        {
            var d = target - origin;
            float dist = d.magnitude;
            if (dist > range) return false;
            if (dist < 1e-4f) return true;
            return Vector3.Angle(forward, d) <= halfAngle;
        }

        /// <summary>噪声能不能被听见：距离在半径 × 听力之内。</summary>
        public static bool CanHear(Vector3 listener, Vector3 noise, float radius, float hearing) =>
            (noise - listener).sqrMagnitude <= radius * hearing * radius * hearing;

        /// <summary>中间有没有东西挡着（忽略触发器）；ignore 是自己身上的碰撞体所在的根物体。</summary>
        public static bool Clear(Vector3 from, Vector3 to, Transform ignore, Transform target)
        {
            var d = to - from;
            var hits = Physics.RaycastAll(from, d.normalized, d.magnitude, ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (ignore != null && h.transform.IsChildOf(ignore)) continue;
                if (target != null && h.transform.IsChildOf(target)) continue;
                return false;
            }
            return true;
        }
    }
}
