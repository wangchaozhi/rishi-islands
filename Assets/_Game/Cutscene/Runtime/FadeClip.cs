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
    public class FadeClip : PlayableAsset, ITimelineClipAsset
    {
        [Range(0, 1)] public float from = 1f;
        [Range(0, 1)] public float to;
        public ClipCaps clipCaps => ClipCaps.None;
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner) => Playable.Create(graph);
    }
}
