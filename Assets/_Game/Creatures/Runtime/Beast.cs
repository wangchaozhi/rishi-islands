using UnityEngine;
using Rishi.World;

namespace Rishi.Creatures
{
    /// <summary>
    /// 岛上的野兽（野猪、椰子蟹）：白天是普通形态，夜里变成「蚀化」形态——身体发黑、眼睛和裂纹透出暗红的光、
    /// 个头大一圈，换成更凶的参数（CreatureDef）。另外按移动速度摆动四肢、身体上下起伏。
    /// </summary>
    [RequireComponent(typeof(Creature))]
    public class Beast : MonoBehaviour
    {
        public Creature creature;
        public DayNightCycle dayNight;
        public CreatureDef dayDef, nightDef;

        [Header("蚀化外观")]
        [Tooltip("只在夜里出现的部件（发光的眼睛、裂纹）")]
        public GameObject[] eclipseOnly;
        [Tooltip("只在白天出现的部件（普通的眼睛）")]
        public GameObject[] dayOnly;
        public Renderer[] skin;
        [Tooltip("夜里身体换成的材质；白天恢复各部件原来的材质")]
        public Material nightMaterial;
        [SerializeField, HideInInspector] Material[] dayMaterials;
        public float nightScale = 1.12f;

        [Header("步态")]
        public Transform body;
        public Transform[] legs;
        [Tooltip("每条腿的相位（0~1），对角的腿同相")]
        public float[] legPhase;
        public float legSwing = 28f;
        public float strideLength = 0.9f;
        public float bob = 0.03f;

        public bool Eclipsed { get; private set; }

        Vector3 lastPos, baseScale, bodyBase;
        Quaternion[] legRest; // 腿的静止姿态（椰子蟹的腿往两边张开），摆动叠加在它上面
        float phase;
        bool initialized;

        void Start() => Init();

        void Init()
        {
            if (initialized) return;
            initialized = true;
            if (creature == null) creature = GetComponent<Creature>();
            baseScale = transform.localScale;
            if (body != null) bodyBase = body.localPosition;
            if (legs != null)
            {
                legRest = new Quaternion[legs.Length];
                for (int i = 0; i < legs.Length; i++) legRest[i] = legs[i] != null ? legs[i].localRotation : Quaternion.identity;
            }
            lastPos = transform.position;
            Apply(false);
        }

        /// <summary>太阳落到地平线下就算夜里。</summary>
        public static bool IsNight(float sunElevation) => sunElevation < -0.03f;

        void Update()
        {
            bool night = dayNight != null && IsNight(dayNight.SunElevation);
            if (night != Eclipsed) Apply(night);

            // 步态：走得越快，腿摆得越快
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            float speed = Vector3.ProjectOnPlane(transform.position - lastPos, Vector3.up).magnitude / dt;
            lastPos = transform.position;
            phase = Mathf.Repeat(phase + speed / Mathf.Max(strideLength, 0.05f) * dt, 1f);
            float moving = Mathf.Clamp01(speed / 1.2f);
            if (legs != null)
                for (int i = 0; i < legs.Length; i++)
                {
                    if (legs[i] == null) continue;
                    float p = legPhase != null && i < legPhase.Length ? legPhase[i] : (i % 2) * 0.5f;
                    var rest = legRest != null && i < legRest.Length ? legRest[i] : Quaternion.identity;
                    legs[i].localRotation = Quaternion.Euler(Mathf.Sin((phase + p) * Mathf.PI * 2f) * legSwing * moving, 0, 0) * rest;
                }
            if (body != null) body.localPosition = bodyBase + Vector3.up * Mathf.Abs(Mathf.Sin(phase * Mathf.PI * 2f)) * bob * moving;
        }

        /// <summary>切换白天 / 蚀化形态。</summary>
        public void Apply(bool night)
        {
            Eclipsed = night;
            if (creature != null) creature.def = night && nightDef != null ? nightDef : dayDef != null ? dayDef : creature.def;
            if (eclipseOnly != null) foreach (var g in eclipseOnly) if (g != null) g.SetActive(night);
            if (dayOnly != null) foreach (var g in dayOnly) if (g != null) g.SetActive(!night);
            if (skin != null)
            {
                if (dayMaterials == null || dayMaterials.Length != skin.Length)
                {
                    dayMaterials = new Material[skin.Length];
                    for (int i = 0; i < skin.Length; i++) dayMaterials[i] = skin[i] != null ? skin[i].sharedMaterial : null;
                }
                for (int i = 0; i < skin.Length; i++)
                    if (skin[i] != null) skin[i].sharedMaterial = night && nightMaterial != null ? nightMaterial : dayMaterials[i];
            }
            if (baseScale == Vector3.zero) baseScale = transform.localScale;
            transform.localScale = baseScale * (night ? nightScale : 1f);
        }

        /// <summary>离线截图用（编辑器里不跑 Start）。</summary>
        public void Preview(bool night)
        {
            if (baseScale == Vector3.zero) baseScale = transform.localScale;
            Apply(night);
        }
    }
}
