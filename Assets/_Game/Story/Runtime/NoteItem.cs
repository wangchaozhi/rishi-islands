using UnityEngine;
using Rishi.Audio;
using Rishi.Core;
using Rishi.World;

namespace Rishi.Story
{
    /// <summary>可阅读的笔记 / 日志 / 壁画。读过后可选推进目标。</summary>
    public class NoteItem : Interactable
    {
        public string verb = "阅读";
        public NoteData note = new NoteData();
        [Tooltip("读完后推进到的目标阶段（0 = 不推进）")]
        public int objectiveStage;
        public string objectiveText;
        [Tooltip("互动时播放的音效 id，留空按动作自动选择")]
        public string sound;
        [Tooltip("读完后从场景中拿走（捡起的物品）")]
        public bool hideOnRead;

        public override string Prompt => $"[E] {verb}：{note.title}";

        public override void Interact()
        {
            var gs = GameState.Instance;
            if (gs == null) return;
            AudioManager.Play2D(!string.IsNullOrEmpty(sound) ? sound : verb == "播放" ? "note_recorder" : "note_paper");
            gs.AddNote(note);
            if (hideOnRead) gameObject.SetActive(false);
            if (objectiveStage > 0) gs.SetObjective(objectiveStage, objectiveText);
        }
    }
}
