using System.Collections;
using UnityEngine;
using Rishi.Audio;
using Rishi.Core;
using Rishi.World;

namespace Rishi.Story
{
    /// <summary>
    /// 第一夜：拿到第一块碎片后太阳坠落（TreasureChest 触发），入夜时环境声突然安静；
    /// 过一会儿画面黑下去，梦见陈默在四十米深的沉船舱室里敲舱壁；醒来发现敲击声是真的，从沉船那边传来。
    /// 海滩上多了一串从海里走上来的湿脚印通向沉船，林边站着穿 18 世纪水手服的人影（彼得），一照就消失。
    /// 走到沉船边敲击声停下，目标变成「撑过这一夜」；天亮后回到原来的目标。事件期间不随机刷影嗣。
    /// </summary>
    public class FirstNightEvent : MonoBehaviour
    {
        public DayNightCycle dayNight;
        [Tooltip("沉船：敲击声从这里传来，走近后停止")]
        public Transform wreck;
        [Tooltip("湿脚印（默认隐藏，入夜后出现）")]
        public GameObject footprints;
        [Tooltip("林边的彼得（默认隐藏）")]
        public Peter peter;
        [Tooltip("敲击声的 3D 音源，放在船舱里")]
        public AudioSource knockSource;
        public float reachWreckDistance = 6f;

        public enum Stage { Waiting, Nightfall, Dream, Knocking, Endure, Done }
        public Stage Current { get; private set; } = Stage.Waiting;

        static readonly string[] DreamLines =
        {
            "……四十米。手电的光被淤泥吃掉了一半。",
            "舱壁的另一边，陈默在敲。一下，两下，三下。",
            "气瓶只剩一百巴。我松开了手。",
        };

        string subtitle;
        float subtitleAlpha;
        GUIStyle subStyle;

        void OnEnable() => GameSignals.FragmentsChanged += OnFragments;
        void OnDisable() => GameSignals.FragmentsChanged -= OnFragments;

        void Start()
        {
            if (footprints != null) footprints.SetActive(false);
            if (peter != null) peter.gameObject.SetActive(false);
        }

        void OnFragments(int count)
        {
            if (count >= 1 && Current == Stage.Waiting) StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            Current = Stage.Nightfall;
            GameSignals.ScriptedEventActive = true;
            // 等太阳坠下去
            yield return new WaitForSeconds(1f);
            while (dayNight != null && dayNight.Rushing) yield return null;

            // 入夜：环境声突然安静（虫鸣都停了）
            var amb = AmbientController.Instance;
            for (float t = 0; t < 1f; t += Time.deltaTime / 1.5f) { if (amb != null) amb.silence = t; yield return null; }
            if (amb != null) amb.silence = 1f;
            if (footprints != null) footprints.SetActive(true);
            yield return new WaitForSeconds(7f);

            // 梦
            Current = Stage.Dream;
            var owner = this;
            GameSignals.SetInputBlock(owner, true);
            for (float t = 0; t < 1f; t += Time.deltaTime / 2.5f) { GameSignals.ScreenFade = t; yield return null; }
            GameSignals.ScreenFade = 1f;
            yield return new WaitForSeconds(1f);
            for (int i = 0; i < DreamLines.Length; i++)
            {
                yield return ShowSubtitle(DreamLines[i], 3.8f);
                if (i == 1) AudioManager.Play2D("knock_three", 0.45f); // 梦里的敲击，隔着水和铁，闷闷的
            }
            yield return new WaitForSeconds(1.2f);
            // 醒来：敲击声还在
            if (knockSource != null) knockSource.Play();
            AudioManager.Play2D("gasp", 0.9f);
            for (float t = 1f; t > 0f; t -= Time.deltaTime / 2.5f) { GameSignals.ScreenFade = t; yield return null; }
            GameSignals.ScreenFade = 0f;
            GameSignals.SetInputBlock(owner, false);
            GameSignals.Toast("……敲击声还在。是从沉船那边传来的。");
            GameState.Instance?.SetObjective(6, "沉船里传来敲击声，过去看看");
            if (peter != null) peter.gameObject.SetActive(true);
            if (amb != null) StartCoroutine(Fade(amb, 1f, 0.6f, 6f)); // 稍微回来一点风声和海浪

            // 敲击：每隔十几秒三下，直到走到沉船边
            Current = Stage.Knocking;
            float next = 0f;
            while (wreck != null && GameSignals.Player != null && Vector3.Distance(GameSignals.Player.position, wreck.position) > reachWreckDistance)
            {
                next -= Time.deltaTime;
                if (next <= 0f && knockSource != null && !knockSource.isPlaying) { knockSource.Play(); next = Random.Range(9f, 14f); }
                yield return null;
            }
            if (knockSource != null) knockSource.Stop();
            yield return new WaitForSeconds(0.6f);
            GameSignals.Toast("敲击声停了。船舱里一片漆黑，舱壁上有一排新的抓痕——是从里面抓的。");
            GameSignals.AddFear(0.2f);
            GameState.Instance?.SetObjective(7, "撑过这一夜（点燃的篝火旁是安全的）");

            // 熬到天亮
            Current = Stage.Endure;
            GameSignals.ScriptedEventActive = false;
            if (amb != null) StartCoroutine(Fade(amb, amb.silence, 0f, 10f));
            while (dayNight != null && dayNight.SunElevation < 0.05f) yield return null;
            GameState.Instance?.SetObjective(8, "天亮了。线索：「海水退去之处」。修好沉船旁的小艇（木板 ×3、绳子 ×1），前往东北方海上的环礁");
            if (footprints != null) footprints.SetActive(false);
            if (peter != null) peter.gameObject.SetActive(false);
            Current = Stage.Done;
        }

        IEnumerator ShowSubtitle(string text, float seconds)
        {
            subtitle = text;
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                subtitleAlpha = Mathf.Clamp01(Mathf.Min(t / 0.6f, (seconds - t) / 0.6f));
                yield return null;
            }
            subtitleAlpha = 0f;
        }

        static IEnumerator Fade(AmbientController amb, float from, float to, float seconds)
        {
            for (float t = 0; t < 1f; t += Time.deltaTime / seconds) { amb.silence = Mathf.Lerp(from, to, t); yield return null; }
            amb.silence = to;
        }

        void OnGUI()
        {
            if (subtitleAlpha <= 0.001f || string.IsNullOrEmpty(subtitle)) return;
            GUI.depth = -100; // 画在 HUD 的黑场上面
            subStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            var old = GUI.color;
            GUI.color = new Color(0.85f, 0.88f, 0.92f, subtitleAlpha);
            GUI.Label(new Rect(Screen.width * 0.15f, Screen.height * 0.45f, Screen.width * 0.7f, 80), subtitle, subStyle);
            GUI.color = old;
        }

        /// <summary>离线截图用：直接显示脚印和彼得。</summary>
        public void PreviewShow(bool on)
        {
            if (footprints != null) footprints.SetActive(on);
            if (peter != null) peter.gameObject.SetActive(on);
        }
    }
}
