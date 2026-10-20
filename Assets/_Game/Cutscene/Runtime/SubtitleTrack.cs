using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Rishi.Audio;
using Rishi.Core;
using Rishi.World;

namespace Rishi.Cutscene
{
    /// <summary>Timeline 字幕轨道：每个片段一句字幕，在 Timeline 窗口里直接拖动时间、改文字。</summary>
    [TrackColor(0.95f, 0.85f, 0.45f)]
    [TrackClipType(typeof(SubtitleClip))]
    public class SubtitleTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount) =>
            ScriptPlayable<SubtitleMixer>.Create(graph, inputCount);
    }

    public class SubtitleBehaviour : PlayableBehaviour
    {
        public string text;
    }

    public class SubtitleMixer : PlayableBehaviour
    {
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            string current = null;
            float alpha = 0f;
            for (int i = 0; i < playable.GetInputCount(); i++)
            {
                float w = playable.GetInputWeight(i);
                if (w <= 0f) continue;
                var input = (ScriptPlayable<SubtitleBehaviour>)playable.GetInput(i);
                double t = input.GetTime(), d = input.GetDuration();
                // 每句字幕前后 0.25 秒淡入淡出
                float fade = (float)Math.Min(1.0, Math.Min(t / 0.25, (d - t) / 0.25));
                if (fade > alpha) { alpha = fade; current = input.GetBehaviour().text; }
            }
            CutsceneUI.Subtitle = current;
            CutsceneUI.SubtitleAlpha = Mathf.Clamp01(alpha);
        }

        public override void OnPlayableDestroy(Playable playable) => CutsceneUI.Subtitle = null;
    }
}
