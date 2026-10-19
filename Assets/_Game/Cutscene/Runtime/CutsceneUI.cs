using UnityEngine;
using Rishi.Audio;
using Rishi.Core;
using Rishi.World;

namespace Rishi.Cutscene
{
    /// <summary>过场画面层：电影黑边、字幕、黑场、跳过提示。状态由 Timeline 轨道和 CutscenePlayer 写入。</summary>
    public class CutsceneUI : MonoBehaviour
    {
        public static string Subtitle;
        public static float SubtitleAlpha;
        public static float Fade;
        public static float Letterbox;   // 0~1，黑边展开程度
        public static string SkipHint;

        GUIStyle subStyle, hintStyle;

        void OnGUI()
        {
            GUI.depth = -100; // 画在所有界面之上
            if (subStyle == null)
            {
                subStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.LowerRight };
            }
            float w = Screen.width, h = Screen.height;

            if (Letterbox > 0f)
            {
                float bar = h * 0.11f * Letterbox;
                Rect(0, 0, w, bar, Color.black);
                Rect(0, h - bar, w, bar, Color.black);
            }
            if (Fade > 0f) Rect(0, 0, w, h, new Color(0, 0, 0, Fade));

            if (!string.IsNullOrEmpty(Subtitle) && SubtitleAlpha > 0f)
            {
                var r = new UnityEngine.Rect(w * 0.12f, h * 0.8f, w * 0.76f, h * 0.09f);
                subStyle.normal.textColor = new Color(0, 0, 0, 0.8f * SubtitleAlpha);
                GUI.Label(new UnityEngine.Rect(r.x + 2, r.y + 2, r.width, r.height), Subtitle, subStyle);
                subStyle.normal.textColor = new Color(1f, 0.97f, 0.9f, SubtitleAlpha);
                GUI.Label(r, Subtitle, subStyle);
            }

            if (!string.IsNullOrEmpty(SkipHint) && Letterbox > 0.5f)
            {
                hintStyle.normal.textColor = new Color(1, 1, 1, 0.5f);
                GUI.Label(new UnityEngine.Rect(0, 0, w - 24, h - 14), SkipHint, hintStyle);
            }
        }

        static void Rect(float x, float y, float w, float h, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new UnityEngine.Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
