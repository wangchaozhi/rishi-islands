using UnityEngine;

namespace Rishi.Core
{
    /// <summary>能被砍刀砍的东西（灌木丛、藤蔓）。只有近战武器会调用它。</summary>
    public interface IChoppable
    {
        /// <summary>砍一刀，返回这一刀是否把它砍倒了。</summary>
        bool Chop(Vector3 point, Vector3 direction);
        /// <summary>已经被砍倒。</summary>
        bool Cut { get; }
    }
}
