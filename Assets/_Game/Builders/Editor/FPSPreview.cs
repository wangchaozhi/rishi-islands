using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
    /// 从玩家视角离线渲染几张截图到项目根目录 Screenshots/，用于检查画面（命令行 -executeMethod FPSPreview.Capture）。
    /// </summary>
    public static class FPSPreview
    {
        public static void BuildAndCapture()
        {
            FPSSceneBuilder.Build();
            Capture();
        }

        [MenuItem("Tools/FPS/渲染预览截图")]
        public static void Capture()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/FPSTest.unity");
            var player = GameObject.Find("Player").transform;
            var root = player.Find("CameraRoot");
            var cam = root.GetComponentInChildren<Camera>();

            Directory.CreateDirectory("Screenshots");
            Shot(cam, player, root, new Vector3(0, 1, -18), 0f, 3f, "01_spawn");
            Shot(cam, player, root, new Vector3(-3, 1, -14), 25f, 12f, "02_props");
            Shot(cam, player, root, new Vector3(8, 1, -3), -40f, 5f, "03_cover");
            Shot(cam, player, root, new Vector3(0, 1, 10), 0f, 0f, "04_targets");

            EditorSceneManager.OpenScene(scene.path); // 丢弃预览时对相机的改动
            Debug.Log("[FPS] 预览截图已写入 Screenshots/");
        }

        static void Shot(Camera cam, Transform player, Transform root, Vector3 pos, float yaw, float pitch, string name)
        {
            player.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            root.localRotation = Quaternion.Euler(pitch, 0, 0);

            const int w = 1600, h = 900;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
            cam.targetTexture = rt;
            cam.Render();
            cam.Render(); // 第二帧让 TAA/SMAA、自动曝光等稳定
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes($"Screenshots/{name}.png", tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(tex);
            rt.Release();
        }
    }
}
