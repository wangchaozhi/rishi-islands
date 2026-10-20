using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Rishi.Audio;
using Rishi.Core;
using Rishi.World;

namespace Rishi.Cutscene
{
    /// <summary>Timeline 黑场轨道：片段内黑场不透明度从 from 渐变到 to（1 = 全黑）。片段之外保持最后一个片段的结束值。</summary>
    [TrackColor(0.2f, 0.2f, 0.2f)]
    [TrackClipType(typeof(FadeClip))]
    public class FadeTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            var mixer = ScriptPlayable<FadeMixer>.Create(graph, inputCount);
            var b = mixer.GetBehaviour();
            b.clips = new (double start, double end, float from, float to)[inputCount];
            int i = 0;
            foreach (var c in GetClips())
            {
                var fc = (FadeClip)c.asset;
                b.clips[i++] = (c.start, c.end, fc.from, fc.to);
            }
            Array.Sort(b.clips, (x, y) => x.start.CompareTo(y.start));
            return mixer;
        }
    }

    public class FadeMixer : PlayableBehaviour
    {
        public (double start, double end, float from, float to)[] clips;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            double t = playable.GetGraph().GetRootPlayable(0).GetTime();
            // 第一个片段之前取它的起始值；两个片段之间保持前一个的结束值
            float alpha = clips.Length > 0 ? clips[0].from : 0f;
            foreach (var c in clips)
            {
                if (t < c.start) break;
                if (t >= c.end) { alpha = c.to; continue; }
                float k = (float)((t - c.start) / Math.Max(1e-4, c.end - c.start));
                alpha = Mathf.Lerp(c.from, c.to, k * k * (3 - 2 * k));
                break;
            }
            CutsceneUI.Fade = alpha;
        }

        public override void OnPlayableDestroy(Playable playable) => CutsceneUI.Fade = 0f;
    }
}
