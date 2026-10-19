using UnityEngine;
using Rishi.Core;

namespace Rishi.Audio
{
    /// <summary>水下所有声音变闷：给 AudioListener 加低通滤波，入水时把截止频率压低。挂在带 AudioListener 的摄像机上。</summary>
    [RequireComponent(typeof(AudioListener))]
    public class UnderwaterAudio : MonoBehaviour
    {
        public float dryCutoff = 22000f;
        public float wetCutoff = 650f;
        public float wetResonance = 1.3f;

        AudioLowPassFilter filter;
        float blend;

        void Awake()
        {
            filter = gameObject.AddComponent<AudioLowPassFilter>();
            filter.cutoffFrequency = dryCutoff;
        }

        void Update()
        {
            blend = Mathf.MoveTowards(blend, GameSignals.HeadUnderwater ? 1f : 0f, Time.deltaTime * 6f);
            // 按对数插值，听感上过渡更均匀
            filter.cutoffFrequency = Mathf.Exp(Mathf.Lerp(Mathf.Log(dryCutoff), Mathf.Log(wetCutoff), blend));
            filter.lowpassResonanceQ = Mathf.Lerp(1f, wetResonance, blend);
            filter.enabled = blend > 0.001f;
        }
    }
}
