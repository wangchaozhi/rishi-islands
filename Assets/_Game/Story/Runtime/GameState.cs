using System;
using System.Collections.Generic;
using UnityEngine;
using Rishi.Audio;
using Rishi.Core;
using Rishi.World;

namespace Rishi.Story
{
    /// <summary>
    /// 剧情状态：当前目标、已收集的笔记、太阳之心碎片数。场景里放一个即可（单例）。
    /// </summary>
    public class GameState : MonoBehaviour
    {
        public static GameState Instance { get; private set; }
        /// <summary>阅读笔记、查看日志时为 true，玩家移动和射击会暂停。</summary>
        public static bool InputBlocked { get => GameSignals.InputBlocked; set => GameSignals.InputBlocked = value; }

        [TextArea] public string startObjective = "沙滩那头有一艘古老的沉船残骸，过去看看";
        public int totalFragments = 7;

        public string Objective { get; private set; }
        public int ObjectiveStage { get; private set; }
        public int Fragments { get; private set; }
        public readonly List<NoteData> Notes = new List<NoteData>();

        public event Action<string> OnToast;
        public event Action<NoteData> OnNoteOpened;

        void Awake()
        {
            Instance = this;
            InputBlocked = false;
            GameSignals.SetFragments(0);
            Objective = startObjective;
        }

        /// <summary>推进目标；stage 小于等于当前阶段时忽略，避免剧情倒退。</summary>
        public void SetObjective(int stage, string text)
        {
            if (stage <= ObjectiveStage) return;
            ObjectiveStage = stage;
            Objective = text;
            Toast("新目标：" + text);
        }

        public void AddNote(NoteData note)
        {
            if (!Notes.Exists(n => n.title == note.title))
            {
                Notes.Add(note);
                Toast("已记入日志：" + note.title + "（按 J 查看）");
            }
            OnNoteOpened?.Invoke(note);
        }

        public void AddFragment()
        {
            Fragments++;
            GameSignals.SetFragments(Fragments);
            Toast($"获得 太阳之心碎片（{Fragments}/{totalFragments}）");
        }

        public void Toast(string text) => OnToast?.Invoke(text);
    }

    [Serializable]
    public class NoteData
    {
        public string title;
        [TextArea(4, 12)] public string body;
    }
}
