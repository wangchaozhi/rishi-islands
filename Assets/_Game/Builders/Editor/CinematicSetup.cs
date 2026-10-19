using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Rishi.Core;
using Rishi.Inventory;
using Rishi.World;
using Rishi.Audio;
using Rishi.Player;
using Rishi.Weapons;
using Rishi.UI;
using Rishi.Story;
using Rishi.Cutscene;
using Rishi.Wildlife;
using Rishi.Rendering;

namespace Rishi.Builders
{
    /// <summary>
    /// 电影画质：体积光渲染特性、植物风动画材质、过场专用的电影后处理（景深、运动模糊、胶片颗粒、镜头光晕、电影调色）。
    /// </summary>
    public static class CinematicSetup
    {
        const string RendererPath = "Assets/Settings/PC_Renderer.asset";
        const string Gen = "Assets/Art/Generated";

        // ================= 体积光 =================

        public static void EnsureVolumetricLight()
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (data == null) { Debug.LogWarning("[Cinematic] 找不到 " + RendererPath); return; }

            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{Gen}/Materials/VolumetricLight.mat");
            if (mat == null)
            {
                mat = new Material(Shader.Find("Island/VolumetricLight"));
                AssetDatabase.CreateAsset(mat, $"{Gen}/Materials/VolumetricLight.mat");
            }

            // 体积光参数：只想要树冠缝隙里的光柱和远处一层薄雾，不要整屏发白
            mat.SetFloat("_Density", 0.0022f);
            mat.SetFloat("_Intensity", 0.7f);
            mat.SetFloat("_Anisotropy", 0.78f);
            mat.SetFloat("_MaxDistance", 70f);
            mat.SetFloat("_HeightFalloff", 0.09f);
            mat.SetFloat("_NightScale", 1.5f);
            EditorUtility.SetDirty(mat);

            var feature = data.rendererFeatures.OfType<VolumetricLightFeature>().FirstOrDefault();
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<VolumetricLightFeature>();
                feature.name = "VolumetricLight";
                AssetDatabase.AddObjectToAsset(feature, data);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
                var so = new SerializedObject(data);
                var list = so.FindProperty("m_RendererFeatures");
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feature;
                var map = so.FindProperty("m_RendererFeatureMap");
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                so.ApplyModifiedProperties();
            }
            feature.passMaterial = mat;
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
            feature.fetchColorBuffer = true;
            feature.requirements = ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color;
            feature.SetActive(true);
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
        }

        // ================= 植物风动画 =================

        /// <summary>把植物材质换成 Island/Foliage，并按植物类型设置摆动幅度（存放在 _Parallax）。</summary>
        public static void ApplyFoliageWind()
        {
            var shader = Shader.Find("Island/Foliage");
            if (shader == null) { Debug.LogWarning("[Cinematic] 找不到 Island/Foliage 着色器"); return; }
            int n = 0;
            void W(string path, float amount)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) return;
                m.shader = shader;
                m.SetFloat("_Parallax", amount);
                EditorUtility.SetDirty(m);
                n++;
            }
            foreach (var id in new[] { "island_tree_01", "island_tree_02" })
            {
                W($"{Gen}/Materials/{id}_trunk.mat", 0.22f);
                W($"{Gen}/Island/Materials/{id}_branches.mat", 0.22f);
                W($"{Gen}/Island/Materials/{id}_leaves.mat", 0.22f);
            }
            W($"{Gen}/Jungle/PalmBark.mat", 0.3f);
            W($"{Gen}/Jungle/CanopyBark.mat", 0.16f);
            W($"{Gen}/Jungle/CanopyLeaves.mat", 0.16f);
            W($"{Gen}/Jungle/ShrubLeaves.mat", 0.5f);
            W($"{Gen}/Jungle/PalmFrond.mat", 0.3f);
            W($"{Gen}/Jungle/BananaLeaf.mat", 0.6f);
            W($"{Gen}/Jungle/TaroLeaf.mat", 0.5f);
            W($"{Gen}/Island/Materials/grass_medium_01.mat", 1.0f);
            W($"{Gen}/Island/Materials/fern_02.mat", 0.7f);
            W($"{Gen}/Island/Materials/shrub_03.mat", 0.6f);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Cinematic] {n} 个植物材质启用了风动画");
        }

        // ================= 过场电影后处理 =================

        public static Volume CreateCinematicVolume()
        {
            string path = $"{Gen}/CinematicPostProcess.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            T Add<T>() where T : VolumeComponent
            {
                var c = profile.Add<T>(true);
                c.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(c, profile);
                return c;
            }

            var dof = Add<DepthOfField>();
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(35f);
            dof.gaussianEnd.Override(160f);
            dof.gaussianMaxRadius.Override(1.2f);
            dof.highQualitySampling.Override(true);
            var mb = Add<MotionBlur>();
            mb.intensity.Override(0.35f);
            mb.quality.Override(MotionBlurQuality.High);
            var grain = Add<FilmGrain>();
            grain.type.Override(FilmGrainLookup.Medium3);
            grain.intensity.Override(0.28f);
            grain.response.Override(0.75f);
            var flare = Add<ScreenSpaceLensFlare>();
            flare.intensity.Override(0.6f);
            var color = Add<ColorAdjustments>();
            color.contrast.Override(12f);
            color.saturation.Override(4f);
            var vig = Add<Vignette>();
            vig.intensity.Override(0.32f);
            vig.smoothness.Override(0.5f);
            var smh = Add<ShadowsMidtonesHighlights>();
            smh.shadows.Override(new Vector4(0.92f, 1f, 1.1f, 0f));   // 暗部偏青
            smh.highlights.Override(new Vector4(1.08f, 1f, 0.9f, 0f)); // 高光偏暖
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var vol = new GameObject("CinematicPostProcess").AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 10;
            vol.weight = 0f; // 过场时由 CutscenePlayer 打开
            vol.sharedProfile = profile;
            return vol;
        }

        /// <summary>给游戏画面加一点镜头光晕和极轻的胶片颗粒。</summary>
        public static void TuneGameplayProfile(string profilePath)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null) return;
            if (!profile.Has<ScreenSpaceLensFlare>())
            {
                var f = profile.Add<ScreenSpaceLensFlare>(true);
                f.name = nameof(ScreenSpaceLensFlare);
                AssetDatabase.AddObjectToAsset(f, profile);
                f.intensity.Override(0.3f);
            }
            if (!profile.Has<FilmGrain>())
            {
                var g = profile.Add<FilmGrain>(true);
                g.name = nameof(FilmGrain);
                AssetDatabase.AddObjectToAsset(g, profile);
                g.type.Override(FilmGrainLookup.Thin1);
                g.intensity.Override(0.08f);
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

        /// <summary>TAA 时间抗锯齿：远处植被和细节不再闪烁。</summary>
        public static void UseTAA(Camera cam)
        {
            var data = cam.GetUniversalAdditionalCameraData();
            data.antialiasing = AntialiasingMode.TemporalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
        }
    }
}
