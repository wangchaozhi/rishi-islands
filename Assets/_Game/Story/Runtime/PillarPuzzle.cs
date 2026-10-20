using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Rishi.Audio;
using Rishi.Core;
using Rishi.World;

namespace Rishi.Story
{
    /// <summary>
    /// 石柱谜题：所有石柱转到正确符文后，符文发光、石门沉入地下、目标推进。
    /// </summary>
    public class PillarPuzzle : MonoBehaviour
    {
        public GlyphPillar[] pillars;
        public Transform door;
        public float doorSinkDepth = 3.6f;
        public float doorDuration = 4f;
        public Renderer[] glowRenderers;
        [ColorUsage(false, true)] public Color glowColor = new Color(4f, 2.2f, 0.6f);
        public Light revealLight;
        public int objectiveStage = 4;
        public string objectiveText = "进入神殿，打开石匣";
        [Tooltip("机关解开时触发（接 CG03 石门开启的过场）")]
        public UnityEvent onSolved = new UnityEvent();

        public bool Solved { get; private set; }

        public void Check()
        {
            if (Solved) return;
            foreach (var p in pillars)
                if (!p.IsCorrect || p.Turning) return;
            Solved = true;
            foreach (var p in pillars) p.Locked = true;
            StartCoroutine(OpenSequence());
        }

        IEnumerator OpenSequence()
        {
            onSolved.Invoke();
            GameState.Instance?.Toast("石柱发出低沉的轰鸣……");
            AudioManager.Play("puzzle_solved", transform.position);
            var block = new MaterialPropertyBlock();
            for (float t = 0; t < 1f; t += Time.deltaTime / 1.2f)
            {
                block.SetColor("_EmissionColor", glowColor * t);
                foreach (var r in glowRenderers) r.SetPropertyBlock(block);
                yield return null;
            }

            AudioManager.Play("stone_door", door.position);
            Vector3 start = door.localPosition;
            Vector3 end = start + Vector3.down * doorSinkDepth;
            Vector3 shakeBase = start;
            for (float t = 0; t < 1f; t += Time.deltaTime / doorDuration)
            {
                float e = t * t * (3 - 2 * t);
                Vector3 shake = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)) * 0.015f * (1 - t);
                door.localPosition = Vector3.Lerp(start, end, e) + shake;
                if (revealLight != null) revealLight.intensity = Mathf.Lerp(0, 2.5f, e);
                yield return null;
            }
            door.localPosition = end;
            door.gameObject.SetActive(false);
            GameState.Instance?.SetObjective(objectiveStage, objectiveText);
        }
    }
}
