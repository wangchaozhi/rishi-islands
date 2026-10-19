using System.Collections.Generic;
using UnityEngine;
using Rishi.Core;
using Rishi.World;

namespace Rishi.Audio
{
    public enum SoundCategory { Ambience, Sfx, Music, Voice }

    /// <summary>
    /// 音效管理：按 id 播放（同一 id 可有多个变体随机挑选）、2D / 3D、对象池、分组音量。
    /// 音效参数在 SoundLibrary（Assets/Config/SoundLibrary.asset）。
    /// 场景里放一个即可，其他脚本用 AudioManager.Play(...)，没有管理器时静默跳过。
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Tooltip("音效表（Assets/Config/SoundLibrary.asset）")]
        public SoundLibrary library;
        [Range(0, 1)] public float masterVolume = 1f;
        [Range(0, 1)] public float ambienceVolume = 0.8f;
        [Range(0, 1)] public float sfxVolume = 1f;
        [Range(0, 1)] public float musicVolume = 0.7f;
        [Range(0, 1)] public float voiceVolume = 1f;
        public int poolSize = 32;

        readonly Dictionary<string, SoundLibrary.Sound> map = new Dictionary<string, SoundLibrary.Sound>();
        readonly List<AudioSource> pool = new List<AudioSource>();

        void Awake()
        {
            Instance = this;
            if (library != null)
                foreach (var s in library.sounds)
                    if (!string.IsNullOrEmpty(s.id)) map[s.id] = s;
            for (int i = 0; i < poolSize; i++)
            {
                var go = new GameObject("Voice" + i);
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.rolloffMode = AudioRolloffMode.Logarithmic;
                src.dopplerLevel = 0f;
                pool.Add(src);
            }
        }

        public float CategoryVolume(SoundCategory c) => masterVolume * c switch
        {
            SoundCategory.Ambience => ambienceVolume,
            SoundCategory.Music => musicVolume,
            SoundCategory.Voice => voiceVolume,
            _ => sfxVolume,
        };

        public SoundLibrary.Sound Get(string id) => map.TryGetValue(id, out var s) ? s : null;

        public AudioClip RandomClip(string id)
        {
            var s = Get(id);
            return s == null || s.clips == null || s.clips.Length == 0 ? null : s.clips[UnityEngine.Random.Range(0, s.clips.Length)];
        }

        /// <summary>在世界坐标播放 3D 音效。</summary>
        public static AudioSource Play(string id, Vector3 position, float volumeScale = 1f) => Instance?.PlayInternal(id, position, true, volumeScale);

        /// <summary>播放 2D 音效（玩家自己的声音、界面音效）。</summary>
        public static AudioSource Play2D(string id, float volumeScale = 1f) => Instance?.PlayInternal(id, Vector3.zero, false, volumeScale);

        AudioSource PlayInternal(string id, Vector3 position, bool spatial, float volumeScale)
        {
            var s = Get(id);
            var clip = RandomClip(id);
            if (s == null || clip == null) return null;
            var src = FreeSource();
            src.transform.position = position;
            src.clip = clip;
            src.loop = false;
            src.spatialBlend = spatial ? 1f : 0f;
            src.minDistance = s.minDistance;
            src.maxDistance = s.maxDistance;
            src.volume = s.volume * volumeScale * CategoryVolume(s.category);
            src.pitch = UnityEngine.Random.Range(s.pitch.x, s.pitch.y);
            src.Play();
            return src;
        }

        AudioSource FreeSource()
        {
            AudioSource oldest = pool[0];
            foreach (var src in pool)
            {
                if (!src.isPlaying) return src;
                if (src.time > oldest.time) oldest = src;
            }
            return oldest; // 全占满时抢占播放最久的那个
        }
    }
}
