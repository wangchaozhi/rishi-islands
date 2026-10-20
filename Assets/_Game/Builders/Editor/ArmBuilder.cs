using UnityEngine;
using UnityEngine.Rendering;

namespace Rishi.Builders
{
    /// <summary>
    /// 程序化的第一人称手臂（占位，正式模型到位后替换）：卷起袖子的衬衫、小臂、手掌、四根两节的手指和拇指。
    /// 只画画面里能看到的部分：手腕往后 0.6 米左右就出画了。
    /// </summary>
    public static class ArmBuilder
    {
        static Material Skin => FPSSceneBuilder.Painted("ArmSkin", new Color(0.68f, 0.48f, 0.36f), 0f, 0.32f);
        static Material Sleeve => FPSSceneBuilder.Painted("ArmSleeve", new Color(0.26f, 0.27f, 0.21f), 0f, 0.12f);

        /// <summary>
        /// 生成一条手臂。wrist：手腕位置（父物体局部坐标）；elbowDir：从手腕指向手肘的方向（往后、往下、往外）；
        /// back：手背朝向；grip：0 = 手指伸直，1 = 握紧；left：左手（拇指在另一侧）。
        /// </summary>
        public static GameObject Arm(Transform parent, string name, Vector3 wrist, Vector3 elbowDir, Vector3 back, float grip, bool left, int layer)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = wrist;
            // 局部坐标：+Z 指向手指，+Y 是手背，X 横跨手掌
            var fwd = -elbowDir.normalized;
            root.transform.localRotation = Quaternion.LookRotation(fwd, Vector3.ProjectOnPlane(back, fwd).normalized);
            float ps = parent != null ? parent.lossyScale.x : 1f;
            if (Mathf.Abs(ps) > 1e-4f) root.transform.localScale = Vector3.one / ps; // 枪模可能缩放过，手臂保持真实大小
            var t = root.transform;

            // 小臂（露出的皮肤）和卷起的袖子
            Segment(t, "Forearm", new Vector3(0, 0, -0.2f), new Vector3(0, 0, 0.0f), 0.026f, Skin);
            Segment(t, "Sleeve", new Vector3(0, 0.003f, -0.62f), new Vector3(0, 0.003f, -0.17f), 0.043f, Sleeve);
            Segment(t, "Cuff", new Vector3(0, 0.003f, -0.2f), new Vector3(0, 0.003f, -0.155f), 0.047f, Sleeve);

            // 手掌
            Rounded(t, "Palm", new Vector3(0, -0.004f, 0.045f), new Vector3(0.07f, 0.03f, 0.092f), Skin);
            Rounded(t, "Wrist", new Vector3(0, -0.002f, 0.004f), new Vector3(0.055f, 0.036f, 0.05f), Skin);
            // 四根手指：每根两节，按握紧程度往掌心（-Y）弯
            float[] lengths = { 0.038f, 0.044f, 0.042f, 0.034f };
            float side = left ? -1f : 1f; // 右手的食指在 -X 一侧（手背朝上时）
            for (int i = 0; i < 4; i++)
            {
                float x = side * (-0.026f + i * 0.0175f);
                var a = new Vector3(x, -0.002f, 0.083f);
                var d1 = Quaternion.Euler(grip * 70f, 0, 0) * Vector3.forward;
                var b = a + d1 * lengths[i] * 0.55f;
                var d2 = Quaternion.Euler(grip * 150f, 0, 0) * Vector3.forward;
                var c = b + d2 * lengths[i] * 0.45f;
                Segment(t, "Finger" + i, a, b, 0.0095f, Skin);
                Segment(t, "Finger" + i + "Tip", b, c, 0.0088f, Skin);
            }
            // 拇指：从掌根侧面伸出，握紧时往掌心收
            var ta = new Vector3(side * -0.034f, -0.008f, 0.02f);
            var td = Quaternion.Euler(grip * 35f, side * -(35f - grip * 20f), 0) * Vector3.forward;
            Segment(t, "Thumb", ta, ta + td * 0.05f, 0.011f, Skin);

            foreach (var tr in root.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = layer;
            foreach (var r in root.GetComponentsInChildren<Renderer>()) { r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = true; }
            return root;
        }

        /// <summary>两点之间的一段胶囊。</summary>
        static void Segment(Transform parent, string name, Vector3 a, Vector3 b, float radius, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            var d = b - a;
            go.transform.localPosition = (a + b) * 0.5f;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d.sqrMagnitude > 1e-8f ? d : Vector3.up);
            go.transform.localScale = new Vector3(radius * 2f, (d.magnitude + radius * 2f) * 0.5f, radius * 2f);
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        /// <summary>圆润的扁块（手掌、手腕）：沿 Z 放倒的胶囊，size 是宽、厚、长。</summary>
        static void Rounded(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(90f, 0, 0); // 胶囊的长轴（Y）转到 Z
            go.transform.localScale = new Vector3(size.x, size.z * 0.5f, size.y);
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        static void Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
