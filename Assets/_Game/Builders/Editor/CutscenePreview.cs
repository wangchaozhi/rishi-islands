using System.IO;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using Rishi.Core;
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
    /// <summary>离线渲染过场的若干帧（黑场按透明度压暗、写入字幕日志），输出到 Screenshots/Cutscenes/。</summary>
    public static class CutscenePreview
    {
        public static void Capture(string scenePath, string cutsceneName, float[] times)
        {
            EditorSceneManager.OpenScene(scenePath);
            var go = GameObject.Find(cutsceneName);
            var director = go.GetComponent<PlayableDirector>();
            var player = go.GetComponent<CutscenePlayer>();
            player.cutsceneCamera.gameObject.SetActive(true);
            player.rig.gameObject.SetActive(true);
            var brain = player.cutsceneCamera.GetComponent<CinemachineBrain>();
            brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
            var cycle = Object.FindFirstObjectByType<DayNightCycle>();
            if (cycle != null) { cycle.Apply(false); DynamicGI.UpdateEnvironment(); }

            Directory.CreateDirectory("Screenshots/Cutscenes");
            director.RebuildGraph();
            foreach (var t in times)
            {
                director.time = t;
                director.Evaluate();
                player.rig.Lens.FieldOfView = CameraPathMixer.CurrentFov;
                brain.ManualUpdate();
                var ocean = Object.FindFirstObjectByType<OceanFollow>();
                if (ocean != null)
                {
                    var p = player.cutsceneCamera.transform.position;
                    ocean.transform.position = new Vector3(Mathf.Round(p.x / 4) * 4, 0, Mathf.Round(p.z / 4) * 4);
                }

                const int w = 1280, h = 720;
                var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                var cam = player.cutsceneCamera;
                cam.targetTexture = rt;
                for (int i = 0; i < 2; i++) cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                // 模拟黑场和电影黑边（IMGUI 不会出现在离线渲染里）
                var px = tex.GetPixels();
                int bar = (int)(h * 0.11f);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int i = y * w + x;
                        px[i] = (y < bar || y >= h - bar) ? Color.black : px[i] * (1f - CutsceneUI.Fade);
                    }
                tex.SetPixels(px);
                tex.Apply();
                File.WriteAllBytes($"Screenshots/Cutscenes/{cutsceneName}_{t:00.0}s.png", tex.EncodeToPNG());
                Debug.Log($"[Cutscene] {cutsceneName} t={t:0.0}s fade={CutsceneUI.Fade:0.00} subtitle=「{CutsceneUI.Subtitle}」 cam={cam.transform.position}");
                cam.targetTexture = null;
                RenderTexture.active = null;
                Object.DestroyImmediate(tex);
                rt.Release();
            }
        }
    }
}
