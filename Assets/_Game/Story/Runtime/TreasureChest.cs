using System.Collections;
using UnityEngine;
using Rishi.Audio;
using Rishi.Core;
using Rishi.World;

namespace Rishi.Story
{
    /// <summary>宝箱：按 E 打开，箱盖掀起，太阳之心碎片升起并被收集。</summary>
    public class TreasureChest : Interactable
    {
        public Transform lid;
        public Vector3 lidOpenEuler = new Vector3(-105f, 0, 0);
        public Transform fragment;
        public Light glow;
        public NoteData note = new NoteData();
        public int objectiveStage = 5;
        public string objectiveText = "线索：「海水退去之处」。修好沉船的小艇，前往下一座岛（开发中）";

        bool opened;

        public override string Prompt => opened ? null : "[E] 打开石匣";

        public override void Interact()
        {
            if (opened) return;
            opened = true;
            StartCoroutine(Open());
        }

        IEnumerator Open()
        {
            Quaternion lidStart = lid != null ? lid.localRotation : Quaternion.identity;
            Quaternion lidEnd = lidStart * Quaternion.Euler(lidOpenEuler);
            // 用世界坐标移动碎片：箱子模型根节点带缩放和旋转
            Vector3 fragStart = fragment != null ? fragment.position : Vector3.zero;
            if (fragment != null) fragment.gameObject.SetActive(true);
            AudioManager.Play("chest_open", transform.position);

            for (float t = 0; t < 1f; t += Time.deltaTime / 1.4f)
            {
                float e = 1 - (1 - t) * (1 - t);
                if (lid != null) lid.localRotation = Quaternion.Slerp(lidStart, lidEnd, e);
                if (glow != null) glow.intensity = Mathf.Lerp(0, 6f, e);
                yield return null;
            }
            for (float t = 0; t < 1f; t += Time.deltaTime / 1.6f)
            {
                if (fragment != null)
                {
                    fragment.position = fragStart + Vector3.up * Mathf.SmoothStep(0, 0.6f, t);
                    fragment.Rotate(0, 180f * Time.deltaTime, 0, Space.World);
                }
                yield return null;
            }
            yield return new WaitForSeconds(0.4f);

            AudioManager.Play2D("fragment_get");
            var gs = GameState.Instance;
            if (gs != null)
            {
                gs.AddFragment();
                if (!string.IsNullOrEmpty(note.title)) gs.AddNote(note);
                gs.SetObjective(objectiveStage, objectiveText);
            }
            // 拿走碎片的代价：太阳以不自然的速度落下（之后会做成 CG05 入夜）
            var cycle = FindFirstObjectByType<DayNightCycle>();
            if (cycle != null && cycle.timeOfDay > 6f && cycle.timeOfDay < 18.5f)
            {
                cycle.RushTo(20.3f, 25f);
                gs?.Toast("……太阳在往下坠？");
            }
            if (fragment != null) fragment.gameObject.SetActive(false);
            for (float t = 0; t < 1f; t += Time.deltaTime / 2f)
            {
                if (glow != null) glow.intensity = Mathf.Lerp(6f, 0.8f, t);
                yield return null;
            }
        }
    }
}
