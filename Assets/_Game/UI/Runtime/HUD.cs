using UnityEngine;
using Rishi.Audio;
using Rishi.Core;
using Rishi.Player;
using Rishi.Weapons;
using Rishi.World;

namespace Rishi.UI
{
    /// <summary>
    /// 简易 HUD：动态准星（随散布张开）、命中/击杀提示、弹药、操作提示。
    /// IMGUI 实现，原型阶段够用，正式版换 UI Toolkit/uGUI。
    /// </summary>
    public class HUD : MonoBehaviour
    {
        public Gun gun;
        public PlayerController player;
        public DayNightCycle dayNight;

        GUIStyle ammoStyle, ammoShadow, hintStyle;
        float hitTime = -1f;
        bool lastHitKill;

        void OnEnable()
        {
            if (gun != null) gun.OnHitTarget += OnHit;
        }

        void OnDisable()
        {
            if (gun != null) gun.OnHitTarget -= OnHit;
        }

        void OnHit(bool killed)
        {
            hitTime = Time.time;
            lastHitKill = killed;
        }

        void OnGUI()
        {
            if (GameSignals.CutsceneActive) return;
            if (ammoStyle == null)
            {
                ammoStyle = new GUIStyle(GUI.skin.label) { fontSize = 34, alignment = TextAnchor.LowerRight, fontStyle = FontStyle.Bold };
                ammoShadow = new GUIStyle(ammoStyle);
                ammoShadow.normal.textColor = new Color(0, 0, 0, 0.6f);
                hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 15 };
                hintStyle.normal.textColor = new Color(1, 1, 1, 0.75f);
            }

            float cx = Screen.width / 2f, cy = Screen.height / 2f;
            bool hideCrosshair = player != null && player.IsSprinting;
            if (!hideCrosshair)
            {
                float gap = 4f + (gun != null ? gun.SpreadNow * 6f : 0f);
                var old = GUI.color;
                GUI.color = new Color(0, 0, 0, 0.5f);
                DrawCross(cx, cy, gap, 1f);
                GUI.color = new Color(1, 1, 1, 0.9f);
                DrawCross(cx, cy, gap, 0f);
                GUI.color = old;
            }

            float since = Time.time - hitTime;
            if (since < 0.18f)
            {
                var old = GUI.color;
                GUI.color = lastHitKill ? new Color(1f, 0.25f, 0.2f, 1f - since / 0.18f) : new Color(1, 1, 1, 1f - since / 0.18f);
                DrawHitMarker(cx, cy);
                GUI.color = old;
            }

            if (gun != null)
            {
                string ammo = gun.IsReloading ? "换弹中..." : $"{gun.Ammo} <size=20>/ {gun.magazineSize}</size>";
                ammoStyle.richText = ammoShadow.richText = true;
                var rect = new Rect(Screen.width - 330, Screen.height - 80, 300, 50);
                GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), ammo, ammoShadow);
                GUI.Label(rect, ammo, ammoStyle);
            }

            string hint = player != null && !player.IsCursorLocked
                ? "点击画面继续"
                : "WASD 移动  Shift 冲刺  空格 跳  左键 射击  R 换弹  E 互动  J 日志  Esc 释放鼠标" + (dayNight != null ? "  按住 T 快进时间" : "");
            GUI.Label(new Rect(16, 12, 900, 30), hint, hintStyle);

            if (dayNight != null)
            {
                var clockStyle = new GUIStyle(hintStyle) { fontSize = 22, alignment = TextAnchor.UpperRight, fontStyle = FontStyle.Bold };
                GUI.Label(new Rect(Screen.width - 216, 10, 200, 34), dayNight.Clock, clockStyle);
            }
        }

        static void DrawCross(float cx, float cy, float gap, float pad)
        {
            float len = 8f, w = 2f;
            Rect(cx - gap - len - pad, cy - w / 2 - pad, len + pad * 2, w + pad * 2);
            Rect(cx + gap - pad, cy - w / 2 - pad, len + pad * 2, w + pad * 2);
            Rect(cx - w / 2 - pad, cy - gap - len - pad, w + pad * 2, len + pad * 2);
            Rect(cx - w / 2 - pad, cy + gap - pad, w + pad * 2, len + pad * 2);
            Rect(cx - 1 - pad, cy - 1 - pad, 2 + pad * 2, 2 + pad * 2);
        }

        static void DrawHitMarker(float cx, float cy)
        {
            var m = GUI.matrix;
            for (int i = 0; i < 4; i++)
            {
                GUIUtility.RotateAroundPivot(45f + i * 90f, new Vector2(cx, cy));
                Rect(cx + 7f, cy - 1f, 9f, 2f);
                GUI.matrix = m;
            }
        }

        static void Rect(float x, float y, float w, float h)
        {
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
        }
    }
}
