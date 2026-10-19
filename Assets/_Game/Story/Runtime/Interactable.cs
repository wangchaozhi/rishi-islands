using UnityEngine;
using Rishi.Audio;
using Rishi.Core;
using Rishi.World;

namespace Rishi.Story
{
    /// <summary>可互动物体基类：玩家看着它按 E 触发。</summary>
    public abstract class Interactable : MonoBehaviour
    {
        /// <summary>准星对准时显示的提示，返回 null 表示当前不可互动。</summary>
        public abstract string Prompt { get; }
        public abstract void Interact();
    }
}
