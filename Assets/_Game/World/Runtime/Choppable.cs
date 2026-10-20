using System.Collections;
using UnityEngine;
using Rishi.Core;

namespace Rishi.World
{
    /// <summary>
    /// 可以用砍刀砍开的灌木丛：挡路（有碰撞体），砍 hits 刀才倒。每一刀都会晃一下、溅出碎叶；
    /// 砍倒时朝挥刀方向倒下、缩下去，碰撞体随即关掉，人可以过去。
    /// </summary>
    public class Choppable : MonoBehaviour, IChoppable
    {
        [Tooltip("砍几刀才倒")]
        public int hits = 3;
        [Tooltip("碎叶粒子（可选）")]
        public ParticleSystem leaves;

        int taken;
        Quaternion rest;
        bool restSet;

        void Awake() { rest = transform.rotation; restSet = true; }
        public bool Cut { get; private set; }
        public int Remaining => Mathf.Max(0, hits - taken);

        public bool Chop(Vector3 point, Vector3 direction)
        {
            if (Cut) return false;
            taken++;
            if (leaves != null)
            {
                leaves.transform.position = point;
                leaves.Emit(taken >= hits ? 40 : 14);
            }
            if (taken >= hits)
            {
                Cut = true;
                foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
                if (Application.isPlaying) { StopAllCoroutines(); StartCoroutine(Fall(direction)); } // 停掉还没晃完的那一下
                return true;
            }
            if (Application.isPlaying) StartCoroutine(Shake(direction));
            return false;
        }

        IEnumerator Shake(Vector3 dir)
        {
            var axis = Vector3.Cross(Vector3.up, Vector3.ProjectOnPlane(dir, Vector3.up).normalized);
            var start = restSet ? rest : transform.rotation; // 连砍几刀时都从原来的姿态晃，不会越晃越歪
            for (float t = 0; t < 0.35f; t += Time.deltaTime)
            {
                float a = Mathf.Sin(t * 45f) * 9f * (1f - t / 0.35f);
                transform.rotation = Quaternion.AngleAxis(a, axis) * start;
                yield return null;
            }
            transform.rotation = start;
        }

        IEnumerator Fall(Vector3 dir)
        {
            var axis = Vector3.Cross(Vector3.up, Vector3.ProjectOnPlane(dir, Vector3.up).normalized);
            var start = restSet ? rest : transform.rotation;
            var scale = transform.localScale;
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.6f)
            {
                float e = t * t;
                transform.rotation = Quaternion.AngleAxis(e * 75f, axis) * start;
                transform.localScale = new Vector3(scale.x, scale.y * Mathf.Lerp(1f, 0.35f, e), scale.z);
                yield return null;
            }
            // 倒下的枝叶留一会儿再消失，地上只剩一点残桩
            yield return new WaitForSeconds(4f);
            for (float t = 0; t < 1f; t += Time.deltaTime / 1.5f)
            {
                transform.localScale = Vector3.Lerp(new Vector3(scale.x, scale.y * 0.35f, scale.z), new Vector3(scale.x * 0.6f, 0.05f, scale.z * 0.6f), t);
                yield return null;
            }
        }
    }
}
