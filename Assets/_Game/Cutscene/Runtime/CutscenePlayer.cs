using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using Rishi.Audio;
using Rishi.Core;
using Rishi.World;

namespace Rishi.Cutscene
{
    /// <summary>
    /// 过场播放器：播放 Timeline，期间切到过场摄像机（Cinemachine）、锁定玩家、隐藏 HUD、显示电影黑边；
    /// 可按 Esc 跳过；结束后切回第一人称并触发 onFinished。
    /// </summary>
    [RequireComponent(typeof(PlayableDirector))]
    public class CutscenePlayer : MonoBehaviour
    {
        public static bool Active { get => GameSignals.CutsceneActive; private set => GameSignals.CutsceneActive = value; }

        public bool playOnStart;
        public bool skippable = true;
        [Tooltip("带 CinemachineBrain 的过场摄像机")]
        public Camera cutsceneCamera;
        [Tooltip("被镜头路径轨道驱动的 Cinemachine 摄像机")]
        public CinemachineCamera rig;
        public UnityEvent onFinished = new UnityEvent();
        [Tooltip("过场专用电影后处理（景深、运动模糊、胶片颗粒等），播放时打开")]
        public UnityEngine.Rendering.Volume cinematicVolume;

        PlayableDirector director;
        readonly List<Behaviour> disabled = new List<Behaviour>();
        bool finished = true;

        void Awake()
        {
            director = GetComponent<PlayableDirector>();
            director.playOnAwake = false;
            if (FindFirstObjectByType<CutsceneUI>() == null) new GameObject("CutsceneUI").AddComponent<CutsceneUI>();
            if (cutsceneCamera != null) cutsceneCamera.gameObject.SetActive(false);
        }

        void Start()
        {
            if (playOnStart) Play();
        }

        public void Play()
        {
            Active = true;
            finished = false;

            // 关掉游戏中的摄像机和监听器，换成过场摄像机
            disabled.Clear();
            foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (cam.enabled && cam != cutsceneCamera) { cam.enabled = false; disabled.Add(cam); }
            foreach (var l in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (l.enabled && (cutsceneCamera == null || l.gameObject != cutsceneCamera.gameObject)) { l.enabled = false; disabled.Add(l); }
            if (cutsceneCamera != null) cutsceneCamera.gameObject.SetActive(true);
            if (rig != null) rig.gameObject.SetActive(true);

            CutsceneUI.SkipHint = skippable ? "按 Esc 跳过" : null;
            if (cinematicVolume != null) cinematicVolume.weight = 1f;
            director.stopped += OnStopped;
            director.time = 0;
            director.Play();
        }

        void Update()
        {
            if (finished) return;
            CutsceneUI.Letterbox = Mathf.MoveTowards(CutsceneUI.Letterbox, 1f, Time.unscaledDeltaTime * 1.5f);
            if (rig != null) rig.Lens.FieldOfView = CameraPathMixer.CurrentFov;

            var kb = Keyboard.current;
            if (skippable && kb != null && kb.escapeKey.wasPressedThisFrame) director.Stop();
            else if (director.state != PlayState.Playing) Finish();
        }

        void OnStopped(PlayableDirector d) => Finish();

        void Finish()
        {
            if (finished) return;
            finished = true;
            director.stopped -= OnStopped;

            foreach (var b in disabled) if (b != null) b.enabled = true;
            disabled.Clear();
            if (rig != null) rig.gameObject.SetActive(false);
            if (cutsceneCamera != null) cutsceneCamera.gameObject.SetActive(false);

            Active = false;
            if (cinematicVolume != null) cinematicVolume.weight = 0f;
            CutsceneUI.Letterbox = 0f;
            CutsceneUI.Fade = 0f;
            CutsceneUI.Subtitle = null;
            CutsceneUI.SkipHint = null;
            onFinished.Invoke();
        }
    }
}
