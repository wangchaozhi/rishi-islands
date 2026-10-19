using System.Collections;
using UnityEngine;
using Rishi.Audio;
using Rishi.Core;
using Rishi.World;

namespace Rishi.Story
{
    /// <summary>开场独白：开场 CG 结束后弹出（没有 CG 时进入游戏片刻后自动弹出）。</summary>
    public class OpeningMonologue : MonoBehaviour
    {
        public float delay = 1.5f;
        [Tooltip("勾选时等开场 CG 调用 Show()，不自动弹出")]
        public bool waitForCutscene;
        public NoteData note = new NoteData();

        void Start()
        {
            if (!waitForCutscene) Show();
        }

        public void Show() => StartCoroutine(ShowAfterDelay());

        IEnumerator ShowAfterDelay()
        {
            yield return new WaitForSeconds(delay);
            GameState.Instance?.AddNote(note);
        }
    }
}
