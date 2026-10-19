using UnityEngine;
using Rishi.Core;

namespace Rishi.World
{
    /// <summary>海面网格跟随摄像机水平移动（按网格步长对齐，避免波浪抖动），海平面固定在 y = 0。</summary>
    [ExecuteAlways]
    public class OceanFollow : MonoBehaviour
    {
        public float snap = 4f;

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null && Camera.allCamerasCount > 0) cam = Camera.allCameras[0]; // 过场时主摄像机被关掉，跟随过场摄像机
            if (cam == null) return;
            Vector3 p = cam.transform.position;
            transform.position = new Vector3(Mathf.Round(p.x / snap) * snap, 0f, Mathf.Round(p.z / snap) * snap);
        }
    }
}
