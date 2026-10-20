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
    public class CameraMoveClip : PlayableAsset, ITimelineClipAsset
    {
        public Vector3 startPosition;
        public Vector3 startEuler;
        public Vector3 endPosition;
        public Vector3 endEuler;
        [Tooltip("x = 时间进度 0~1，y = 位移进度 0~1")]
        public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public float startFov = 60f;
        public float endFov = 60f;

        public ClipCaps clipCaps => ClipCaps.Blending;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var p = ScriptPlayable<CameraMoveBehaviour>.Create(graph);
            p.GetBehaviour().clip = this;
            return p;
        }
    }
}
