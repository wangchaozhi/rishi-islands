using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Rishi.Audio;
using Rishi.Core;
using Rishi.World;

namespace Rishi.Story
{
    /// <summary>
    /// 剧情界面（IMGUI 原型）：当前目标、互动提示、笔记阅读、日志（J）、获得提示、碎片计数。
    /// </summary>
    public class StoryUI : MonoBehaviour
    {
        public PlayerInteractor interactor;

        NoteData reading;
        bool journalOpen;
        int journalIndex;
        readonly Queue<(string text, float time)> toasts = new Queue<(string, float)>();
        GUIStyle objStyle, promptStyle, toastStyle, titleStyle, bodyStyle, listStyle, smallStyle;
        Texture2D paper;

        void Start()
        {
            var gs = GameState.Instance;
            if (gs == null) return;
            gs.OnToast += t => toasts.Enqueue((t, Time.unscaledTime));
            // 记下打开的帧：同一次按 E 既打开又关闭
            gs.OnNoteOpened += n => { reading = n; journalOpen = false; lastOpenFrame = Time.frameCount; };
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (reading != null && (kb.eKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame) && Time.frameCount > lastOpenFrame)
                reading = null;
            else if (kb.jKey.wasPressedThisFrame && reading == null)
                journalOpen = !journalOpen;
            else if (journalOpen && kb.escapeKey.wasPressedThisFrame)
                journalOpen = false;

            if (journalOpen && GameState.Instance != null && GameState.Instance.Notes.Count > 0)
            {
                int n = GameState.Instance.Notes.Count;
                if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) journalIndex = (journalIndex + n - 1) % n;
                if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) journalIndex = (journalIndex + 1) % n;
            }

            GameState.InputBlocked = reading != null || journalOpen;
            while (toasts.Count > 0 && Time.unscaledTime - toasts.Peek().time > 4f) toasts.Dequeue();
        }

        int lastOpenFrame;

        void OnGUI()
        {
            var gs = GameState.Instance;
            if (gs == null || GameSignals.CutsceneActive) return;
            InitStyles();

            // 目标与碎片
            GUI.Label(new Rect(18, 44, 900, 30), "◆ " + gs.Objective, objStyle);
            if (gs.Fragments > 0)
                GUI.Label(new Rect(18, 72, 600, 24), $"太阳之心碎片 {gs.Fragments}/{gs.totalFragments}", smallStyle);

            // 互动提示
            if (reading == null && !journalOpen && interactor != null && interactor.Current != null)
                Shadowed(new Rect(Screen.width / 2f - 300, Screen.height / 2f + 40, 600, 30), interactor.Current.Prompt, promptStyle);

            // 获得提示
            int i = 0;
            foreach (var (text, time) in toasts)
            {
                float a = Mathf.Clamp01(4f - (Time.unscaledTime - time));
                var c = GUI.color;
                GUI.color = new Color(1, 1, 1, a);
                Shadowed(new Rect(Screen.width / 2f - 400, Screen.height * 0.22f + i * 34, 800, 30), text, toastStyle);
                GUI.color = c;
                i++;
            }

            if (reading != null) DrawPaper(reading.title, reading.body, "按 E 或 Esc 关闭");
            else if (journalOpen) DrawJournal(gs);
        }

        void DrawPaper(string title, string body, string footer)
        {
            float w = Mathf.Min(760, Screen.width - 80), h = Mathf.Min(560, Screen.height - 80);
            var r = new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(0, 0, 0, 0.55f), 0, 0);
            GUI.DrawTexture(r, paper);
            GUI.Label(new Rect(r.x + 40, r.y + 30, w - 80, 40), title, titleStyle);
            GUI.Label(new Rect(r.x + 40, r.y + 84, w - 80, h - 140), body, bodyStyle);
            GUI.Label(new Rect(r.x + 40, r.yMax - 44, w - 80, 24), footer, listStyle);
        }

        void DrawJournal(GameState gs)
        {
            if (gs.Notes.Count == 0)
            {
                DrawPaper("日志", "还没有记录任何东西。\n\n去海滩上的沉船残骸看看吧。", "按 J 关闭");
                return;
            }
            journalIndex = Mathf.Clamp(journalIndex, 0, gs.Notes.Count - 1);
            var note = gs.Notes[journalIndex];
            string list = "";
            for (int k = 0; k < gs.Notes.Count; k++) list += (k == journalIndex ? "▶ " : "   ") + gs.Notes[k].title + "\n";
            DrawPaper($"日志（{journalIndex + 1}/{gs.Notes.Count}）· {note.title}", note.body + "\n\n————————\n" + list, "↑↓ 切换  J 关闭");
        }

        void Shadowed(Rect r, string text, GUIStyle style)
        {
            var c = style.normal.textColor;
            style.normal.textColor = new Color(0, 0, 0, 0.7f * GUI.color.a);
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, style);
            style.normal.textColor = c;
            GUI.Label(r, text, style);
        }

        void InitStyles()
        {
            if (objStyle != null) return;
            objStyle = new GUIStyle(GUI.skin.label) { fontSize = 19, fontStyle = FontStyle.Bold };
            objStyle.normal.textColor = new Color(1f, 0.88f, 0.55f);
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            smallStyle.normal.textColor = new Color(1f, 0.8f, 0.4f);
            promptStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            promptStyle.normal.textColor = Color.white;
            toastStyle = new GUIStyle(promptStyle) { fontSize = 22, fontStyle = FontStyle.Bold };
            toastStyle.normal.textColor = new Color(1f, 0.92f, 0.7f);
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, wordWrap = true };
            titleStyle.normal.textColor = new Color(0.25f, 0.16f, 0.08f);
            bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 19, wordWrap = true };
            bodyStyle.normal.textColor = new Color(0.2f, 0.14f, 0.08f);
            listStyle = new GUIStyle(bodyStyle) { fontSize = 15, alignment = TextAnchor.MiddleRight };
            listStyle.normal.textColor = new Color(0.4f, 0.3f, 0.2f);

            paper = new Texture2D(64, 64);
            var px = new Color[64 * 64];
            var rnd = new System.Random(3);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float edge = Mathf.Min(Mathf.Min(x, 63 - x), Mathf.Min(y, 63 - y)) / 6f;
                    float n = (float)rnd.NextDouble() * 0.04f;
                    float k = Mathf.Lerp(0.78f, 1f, Mathf.Clamp01(edge)) - n;
                    px[y * 64 + x] = new Color(0.93f * k, 0.86f * k, 0.7f * k, 0.97f);
                }
            paper.SetPixels(px);
            paper.Apply();
        }
    }
}
