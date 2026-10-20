using UnityEngine;
using Rishi.Core;
using Rishi.Inventory;
using Rishi.Player;
using Rishi.Weapons;
using Rishi.World;

namespace Rishi.UI
{
    /// <summary>
    /// 简易 HUD：动态准星（随散布张开）、命中/击杀提示、武器栏与弹药、氧气条、缺氧暗角、黑场、操作提示。
    /// IMGUI 实现，原型阶段够用，正式版换 UI Toolkit/uGUI。
    /// </summary>
    public class HUD : MonoBehaviour
    {
        public WeaponController weapons;
        public PlayerController player;
        public DayNightCycle dayNight;

        GUIStyle ammoStyle, ammoShadow, hintStyle, slotStyle, labelStyle, flashStyle;
        Texture2D vignette;
        float hitTime = -1f;
        bool lastHitKill;
        float oxygenAlpha, healthAlpha;

        void OnEnable()
        {
            if (weapons != null) weapons.OnHitTarget += OnHit;
        }

        void OnDisable()
        {
            if (weapons != null) weapons.OnHitTarget -= OnHit;
        }

        void OnHit(bool killed)
        {
            hitTime = Time.time;
            lastHitKill = killed;
        }

        void OnGUI()
        {
            if (GameSignals.CutsceneActive) return;
            InitStyles();
            float cx = Screen.width / 2f, cy = Screen.height / 2f;

            DrawUnderwaterOverlay();
            DrawFearOverlay();
            DrawDamageOverlay();

            bool hideCrosshair = (player != null && player.IsSprinting) || GameSignals.InputBlocked;
            if (!hideCrosshair)
            {
                float gap = 4f + (weapons != null ? weapons.SpreadNow * 6f : 0f);
                bool unusable = weapons != null && weapons.Current != null && !weapons.CanUseHere;
                var old = GUI.color;
                GUI.color = new Color(0, 0, 0, 0.5f);
                DrawCross(cx, cy, gap, 1f);
                GUI.color = unusable ? new Color(1f, 0.4f, 0.35f, 0.7f) : new Color(1, 1, 1, 0.9f);
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

            DrawWeaponBar();
            DrawOxygen();
            DrawFlashlight();
            DrawHealth();

            string hint = player != null && !player.IsCursorLocked && !GameSignals.InputBlocked
                ? "点击画面继续"
                : player != null && player.IsSwimming
                    ? "WASD 游泳（朝视线方向）  Shift 快游  空格 上浮  Ctrl 下潜  1/2/3 切换武器  E 互动  Tab 背包"
                    : "WASD 移动  Shift 冲刺  空格 跳  左键 攻击  R 换弹  1/2/3 切换武器  E 互动  Tab 背包  J 日志  Esc 释放鼠标" + (dayNight != null ? "  按住 T 快进时间" : "");
            GUI.Label(new Rect(16, 12, 1200, 30), hint, hintStyle);

            if (dayNight != null)
            {
                var clockStyle = new GUIStyle(hintStyle) { fontSize = 22, alignment = TextAnchor.UpperRight, fontStyle = FontStyle.Bold };
                GUI.Label(new Rect(Screen.width - 216, 10, 200, 34), dayNight.Clock, clockStyle);
            }

            // 黑场画在最上面
            if (GameSignals.ScreenFade > 0.001f)
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(0, 0, 0, GameSignals.ScreenFade), 0, 0);
        }

        void DrawWeaponBar()
        {
            if (weapons == null) return;
            // 右下角：弹药
            var cur = weapons.Current;
            if (cur != null)
            {
                string text = cur.def.kind == WeaponKind.Melee ? cur.def.displayName
                    : weapons.IsReloading ? "换弹中..."
                    : $"{weapons.Loaded} <size=20>/ {weapons.Reserve}</size>";
                var rect = new Rect(Screen.width - 330, Screen.height - 80, 300, 50);
                GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), text, ammoShadow);
                GUI.Label(rect, text, ammoStyle);
                if (cur.def.kind != WeaponKind.Melee)
                    GUI.Label(new Rect(Screen.width - 330, Screen.height - 108, 300, 24), cur.def.displayName, labelStyle);
            }

            // 右下角上方：武器栏 1/2/3
            float w = 92, h = 30, x0 = Screen.width - 30 - weapons.slots.Count * (w + 6);
            for (int i = 0; i < weapons.slots.Count; i++)
            {
                var s = weapons.slots[i];
                bool owned = weapons.Owns(i);
                bool active = i == weapons.CurrentIndex;
                var r = new Rect(x0 + i * (w + 6), Screen.height - 150, w, h);
                Color bg = active ? new Color(0.95f, 0.75f, 0.35f, 0.55f) : new Color(0, 0, 0, owned ? 0.35f : 0.15f);
                GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, bg, 0, 4);
                var old = GUI.color;
                GUI.color = owned ? Color.white : new Color(1, 1, 1, 0.3f);
                GUI.Label(r, $"{i + 1}  {(owned ? s.def.displayName : "——")}", slotStyle);
                GUI.color = old;
            }
        }

        void DrawOxygen()
        {
            float o = GameSignals.Oxygen;
            bool show = GameSignals.HeadUnderwater || o < 0.999f;
            oxygenAlpha = Mathf.MoveTowards(oxygenAlpha, show ? 1f : 0f, Time.unscaledDeltaTime * 2f);
            if (oxygenAlpha <= 0.001f) return;
            float w = 260, h = 10, x = Screen.width / 2f - w / 2, y = Screen.height - 64;
            Color fill = o < 0.3f ? Color.Lerp(new Color(1f, 0.25f, 0.2f), new Color(0.55f, 0.85f, 1f), o / 0.3f) : new Color(0.55f, 0.85f, 1f);
            if (o < 0.3f) fill.a = 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * 6f));
            fill.a *= oxygenAlpha;
            GUI.DrawTexture(new Rect(x - 2, y - 2, w + 4, h + 4), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(0, 0, 0, 0.45f * oxygenAlpha), 0, 6);
            GUI.DrawTexture(new Rect(x, y, w * o, h), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, fill, 0, 5);
            var old = GUI.color;
            GUI.color = new Color(1, 1, 1, 0.8f * oxygenAlpha);
            string depth = GameSignals.HeadUnderwater ? $"   深度 {GameSignals.CameraDepth:0.0} 米" : "";
            GUI.Label(new Rect(x, y - 24, w + 200, 22), "氧气" + depth, labelStyle);
            GUI.color = old;
        }

        /// <summary>恐惧：视野四周的黑色暗角随恐惧加深，高恐惧时随心跳轻轻收缩。</summary>
        void DrawFearOverlay()
        {
            float f = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 1f, GameSignals.Fear));
            if (f <= 0.001f) return;
            float pulse = 1f + 0.08f * f * Mathf.Sin(Time.time * Mathf.Lerp(5f, 8.5f, f));
            float a = Mathf.Clamp01(f * 0.9f * pulse);
            float grow = Mathf.Lerp(0f, 0.18f, f); // 暗角往里收
            float gw = Screen.width * grow, gh = Screen.height * grow;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), vignette, ScaleMode.StretchToFill, true, 0, new Color(0, 0, 0, a), 0, 0);
            if (grow > 0.01f)
                GUI.DrawTexture(new Rect(gw * 0.5f, gh * 0.5f, Screen.width - gw, Screen.height - gh), vignette, ScaleMode.StretchToFill, true, 0, new Color(0, 0, 0, a * 0.6f), 0, 0);
        }

        /// <summary>受伤：整屏闪红；生命低时四周泛起随心跳脉动的红色暗角。</summary>
        void DrawDamageOverlay()
        {
            float flash = GameSignals.DamageFlash;
            if (flash > 0.001f)
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(0.6f, 0f, 0f, flash * 0.35f), 0, 0);
            float h = GameSignals.Health;
            float low = Mathf.Clamp01(1f - h / 0.35f);
            if (low > 0.001f || flash > 0.001f)
            {
                float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * Mathf.Lerp(5f, 9f, low));
                float a = Mathf.Max(low * pulse, flash * 0.8f);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), vignette, ScaleMode.StretchToFill, true, 0, new Color(0.55f, 0f, 0f, a), 0, 0);
            }
        }

        /// <summary>左下角的生命条：受伤后或不满血时显示。</summary>
        void DrawHealth()
        {
            float h = GameSignals.Health;
            bool show = h < 0.999f || GameSignals.DamageFlash > 0.01f;
            healthAlpha = Mathf.MoveTowards(healthAlpha, show ? 1f : 0f, Time.unscaledDeltaTime * 1.5f);
            if (healthAlpha <= 0.001f) return;
            float w = 200, bh = 8, x = 30, y = Screen.height - 104;
            Color fill = Color.Lerp(new Color(0.85f, 0.15f, 0.12f), new Color(0.9f, 0.85f, 0.8f), Mathf.Clamp01((h - 0.2f) / 0.6f));
            fill.a = 0.9f * healthAlpha;
            GUI.DrawTexture(new Rect(x - 2, y - 2, w + 4, bh + 4), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(0, 0, 0, 0.45f * healthAlpha), 0, 5);
            GUI.DrawTexture(new Rect(x, y, w * h, bh), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, fill, 0, 4);
            var old = GUI.color;
            GUI.color = new Color(1, 1, 1, 0.85f * healthAlpha);
            flashStyle ??= new GUIStyle(labelStyle) { alignment = TextAnchor.LowerLeft };
            GUI.Label(new Rect(x, y - 24, 260, 22), GameSignals.InSafeZone ? "生命   篝火旁 · 安全" : "生命", flashStyle);
            GUI.color = old;
        }

        /// <summary>左下角的手电筒电量：拿到手电筒后显示，亮着时更醒目，电量低时变红闪烁。</summary>
        void DrawFlashlight()
        {
            float c = GameSignals.FlashlightCharge;
            if (c < 0f) return;
            bool on = GameSignals.FlashlightOn;
            float a = on ? 0.9f : 0.45f;
            float w = 120, h = 8, x = 30, y = Screen.height - 46;
            Color fill = c < 0.15f ? new Color(1f, 0.35f, 0.25f) : new Color(1f, 0.9f, 0.6f);
            if (c < 0.15f && on) fill.a = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 5f));
            fill.a *= a;
            GUI.DrawTexture(new Rect(x - 2, y - 2, w + 4, h + 4), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(0, 0, 0, 0.45f * a), 0, 5);
            GUI.DrawTexture(new Rect(x, y, w * c, h), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, fill, 0, 4);
            var old = GUI.color;
            GUI.color = new Color(1, 1, 1, a);
            flashStyle ??= new GUIStyle(labelStyle) { alignment = TextAnchor.LowerLeft };
            GUI.Label(new Rect(x, y - 24, 260, 22), on ? "手电筒  [F]" : "手电筒 关  [F]", flashStyle);
            GUI.color = old;
        }

        void DrawUnderwaterOverlay()
        {
            float o = GameSignals.Oxygen;
            // 缺氧：视野四周变暗收窄
            float low = GameSignals.HeadUnderwater ? Mathf.Clamp01(1f - o / 0.3f) : 0f;
            if (low > 0.001f)
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), vignette, ScaleMode.StretchToFill, true, 0, new Color(0, 0, 0, low * (0.75f + 0.25f * Mathf.Sin(Time.time * 7f))), 0, 0);
        }

        void InitStyles()
        {
            if (ammoStyle != null) return;
            ammoStyle = new GUIStyle(GUI.skin.label) { fontSize = 34, alignment = TextAnchor.LowerRight, fontStyle = FontStyle.Bold, richText = true };
            ammoShadow = new GUIStyle(ammoStyle);
            ammoShadow.normal.textColor = new Color(0, 0, 0, 0.6f);
            hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            hintStyle.normal.textColor = new Color(1, 1, 1, 0.75f);
            slotStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.LowerRight };
            labelStyle.normal.textColor = new Color(1, 1, 1, 0.85f);

            // 径向暗角贴图
            const int n = 128;
            vignette = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    vignette.SetPixel(x, y, new Color(1, 1, 1, Mathf.SmoothStep(0.35f, 1.15f, d)));
                }
            vignette.Apply();
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
