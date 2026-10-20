using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Rishi.Audio;
using Rishi.Core;
using Rishi.World;

namespace Rishi.Cutscene
{
    [Serializable]
    public class SubtitleClip : PlayableAsset, ITimelineClipAsset
    {
        [TextArea(2, 4)] public string text;
        [Tooltip("说话人，留空表示旁白 / 内心独白")]
        public string speaker;

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var p = ScriptPlayable<SubtitleBehaviour>.Create(graph);
            p.GetBehaviour().text = string.IsNullOrEmpty(speaker) ? text : $"{speaker}：{text}";
            return p;
        }
    }
}
