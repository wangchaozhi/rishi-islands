using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Rishi.Audio;
using Rishi.Core;
using Rishi.World;

namespace Rishi.Cutscene
{
    /// <summary>
    /// Timeline 镜头路径轨道：绑定一个 Transform（过场摄像机），每个片段把它从起点位姿平滑移动到终点位姿。
    /// 相邻片段之间直接切镜头；两个片段重叠时会混合（软切）。
    /// </summary>
    [TrackColor(0.4f, 0.75f, 1f)]
    [TrackBindingType(typeof(Transform))]
    [TrackClipType(typeof(CameraMoveClip))]
    public class CameraPathTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount) =>
            ScriptPlayable<CameraPathMixer>.Create(graph, inputCount);
    }

    public class CameraMoveBehaviour : PlayableBehaviour
    {
        public CameraMoveClip clip;

        public void Evaluate(double time, double duration, out Vector3 pos, out Quaternion rot, out float fov)
        {
            float k = clip.ease.Evaluate(Mathf.Clamp01((float)(time / Math.Max(duration, 1e-4))));
            pos = Vector3.LerpUnclamped(clip.startPosition, clip.endPosition, k);
            rot = Quaternion.SlerpUnclamped(Quaternion.Euler(clip.startEuler), Quaternion.Euler(clip.endEuler), k);
            fov = Mathf.LerpUnclamped(clip.startFov, clip.endFov, k);
        }
    }

    public class CameraPathMixer : PlayableBehaviour
    {
        /// <summary>当前帧的视野，过场摄像机会读取它。</summary>
        public static float CurrentFov = 60f;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            var target = playerData as Transform;
            if (target == null) return;

            Vector3 pos = Vector3.zero;
            Quaternion rot = Quaternion.identity;
            float fov = 0f, total = 0f;
            bool first = true;
            for (int i = 0; i < playable.GetInputCount(); i++)
            {
                float w = playable.GetInputWeight(i);
                if (w <= 0f) continue;
                var input = (ScriptPlayable<CameraMoveBehaviour>)playable.GetInput(i);
                input.GetBehaviour().Evaluate(input.GetTime(), input.GetDuration(), out var p, out var r, out var f);
                pos += p * w;
                rot = first ? r : Quaternion.Slerp(rot, r, w / (total + w));
                fov += f * w;
                total += w;
                first = false;
            }
            if (total <= 0f) return;
            target.SetPositionAndRotation(pos / total, rot);
            CurrentFov = fov / total;
        }
    }
}
