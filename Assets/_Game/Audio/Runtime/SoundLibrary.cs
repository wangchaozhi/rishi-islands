using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rishi.Audio
{
    /// <summary>
    /// 音效表：每个音效 id 的变体、分类、音量、音高范围、3D 衰减距离。AudioManager 读取它。
    /// 资产在 Assets/Config/SoundLibrary.asset，由 Tools/Island 的「声音」步骤扫描音效文件夹生成；
    /// 重新扫描只更新音频文件，已有条目在 Inspector 里调过的音量、距离等会保留。
    /// </summary>
    [CreateAssetMenu(menuName = "Rishi/音效表", fileName = "SoundLibrary")]
    public class SoundLibrary : ScriptableObject
    {
        [Serializable]
        public class Sound
        {
            public string id;
            public AudioClip[] clips;
            public SoundCategory category = SoundCategory.Sfx;
            [Range(0, 1.5f)] public float volume = 1f;
            public Vector2 pitch = new Vector2(0.94f, 1.06f);
            public float minDistance = 1.5f;
            public float maxDistance = 45f;
        }

        public List<Sound> sounds = new List<Sound>();

        public Sound Find(string id) => string.IsNullOrEmpty(id) ? null : sounds.Find(s => s.id == id);
    }
}
