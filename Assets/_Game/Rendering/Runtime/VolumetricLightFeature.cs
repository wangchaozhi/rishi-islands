using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Rishi.Rendering
{
    /// <summary>
    /// 体积光（全屏光线步进，着色器 Island/VolumetricLight）：基于 URP 自带的全屏 Pass 特性，
    /// 只对主摄像机生效，跳过武器叠加摄像机和反射探针，避免效果叠加两次。
    /// </summary>
    public class VolumetricLightFeature : FullScreenPassRendererFeature
    {
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var cam = renderingData.cameraData;
            if (cam.renderType == CameraRenderType.Overlay) return;
            if (cam.cameraType == CameraType.Reflection || cam.cameraType == CameraType.Preview) return;
            base.AddRenderPasses(renderer, ref renderingData);
        }
    }
}
