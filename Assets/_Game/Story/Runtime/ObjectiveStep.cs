using UnityEngine;

namespace Rishi.Story
{
    /// <summary>用 UnityEvent 推进目标（其他模块的事件连到 Apply，例如小艇修好、到达环礁）。</summary>
    public class ObjectiveStep : MonoBehaviour
    {
        public int objectiveStage;
        [TextArea] public string objectiveText;

        public void Apply() => GameState.Instance?.SetObjective(objectiveStage, objectiveText);
    }
}
