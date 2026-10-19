using UnityEngine;
using Rishi.Core;

namespace Rishi.World
{
    /// <summary>漂在海面上的东西（浮标、漂流物）：随浪上下起伏、轻轻摇晃。</summary>
    public class FloatingBob : MonoBehaviour
    {
        [Tooltip("物体原点相对海平面的高度")]
        public float waterline = -0.35f;
        public float heave = 0.18f;
        public float heaveSpeed = 0.9f;
        public float tilt = 7f;

        Quaternion baseRot;
        float phase;

        void Start()
        {
            baseRot = transform.rotation;
            phase = Random.value * 10f;
        }

        void Update()
        {
            float t = Time.time * heaveSpeed + phase;
            var p = transform.position;
            p.y = GameSignals.SeaLevel + waterline + Mathf.Sin(t) * heave + Mathf.Sin(t * 2.3f) * heave * 0.3f;
            transform.position = p;
            transform.rotation = Quaternion.Euler(Mathf.Sin(t * 0.8f) * tilt, 0, Mathf.Cos(t * 0.65f) * tilt) * baseRot;
        }
    }
}
