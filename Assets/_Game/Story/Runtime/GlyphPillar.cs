using UnityEngine;
using Rishi.Audio;
using Rishi.Core;
using Rishi.World;

namespace Rishi.Story
{
    /// <summary>可转动的符文石柱：四个面各刻一个符文，按 E 顺时针转 90°。</summary>
    public class GlyphPillar : Interactable
    {
        [Tooltip("四个面上的符文名，按 0°/90°/180°/270° 朝向玩家的顺序")]
        public string[] faceGlyphs = { "海浪", "太阳", "群山", "月亮" };
        public int currentFace;
        public int correctFace;
        public Transform rotor;
        public float turnSpeed = 240f;

        public PillarPuzzle puzzle;
        public bool Locked { get; set; }
        public bool IsCorrect => currentFace == correctFace;
        public bool Turning => Mathf.Abs(Mathf.DeltaAngle(rotor.localEulerAngles.y, TargetAngle)) > 0.5f;

        float TargetAngle => -currentFace * 90f;

        public override string Prompt => Locked ? null : $"[E] 转动石柱（当前：{faceGlyphs[currentFace]}）";

        void Start()
        {
            rotor.localRotation = Quaternion.Euler(0, TargetAngle, 0);
        }

        public override void Interact()
        {
            if (Locked || Turning) return;
            currentFace = (currentFace + 1) % 4;
            AudioManager.Play("stone_turn", rotor.position);
        }

        void Update()
        {
            var target = Quaternion.Euler(0, TargetAngle, 0);
            if (rotor.localRotation != target)
            {
                rotor.localRotation = Quaternion.RotateTowards(rotor.localRotation, target, turnSpeed * Time.deltaTime);
                if (rotor.localRotation == target && puzzle != null) puzzle.Check();
            }
        }
    }
}
