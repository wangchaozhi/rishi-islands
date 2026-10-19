using UnityEngine;

namespace Rishi.Core
{
    /// <summary>能被武器伤害的东西（靶子、野兽、怪物）。武器只认这个接口。</summary>
    public interface IDamageable
    {
        /// <summary>造成伤害，返回这一击是否击杀。</summary>
        bool TakeDamage(float amount);
    }
}
