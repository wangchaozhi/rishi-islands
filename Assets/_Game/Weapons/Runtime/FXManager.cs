using UnityEngine;
using Rishi.Audio;
using Rishi.Core;
using Rishi.Player;
using Rishi.World;

namespace Rishi.Weapons
{
    /// <summary>
    /// 射击特效：枪口火光、命中火花、命中烟尘、弹孔。所有粒子共用几个世界空间粒子系统，按需 Emit，开销很小。
    /// </summary>
    public class FXManager : MonoBehaviour
    {
        public static FXManager Instance { get; private set; }

        public Material additiveMaterial;   // 火花 / 枪口火光（柔和圆点贴图）
        public Material dustMaterial;       // 烟尘
        public Material bulletHoleMaterial; // 弹孔
        public int maxBulletHoles = 80;
        public int weaponLayer = 8;

        ParticleSystem sparks, flash, dust;
        Transform[] holes;
        int nextHole;

        void Awake()
        {
            Instance = this;
            sparks = CreateSystem("Sparks", additiveMaterial, true, 1.2f);
            flash = CreateSystem("MuzzleFlash", additiveMaterial, false, 0f);
            flash.gameObject.layer = weaponLayer; // 和枪模一起由武器摄像机渲染，位置才对得上
            dust = CreateSystem("Dust", dustMaterial, false, -0.05f);

            holes = new Transform[maxBulletHoles];
            var mesh = BuildQuad();
            for (int i = 0; i < holes.Length; i++)
            {
                var go = new GameObject("BulletHole");
                go.transform.SetParent(transform);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = bulletHoleMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.SetActive(false);
                holes[i] = go.transform;
            }
        }

        public void MuzzleFlash(Vector3 pos, Vector3 dir)
        {
            var p = new ParticleSystem.EmitParams { position = pos, startLifetime = 0.05f, velocity = dir * 0.5f };
            p.startColor = new Color(1f, 0.75f, 0.4f, 1f);
            p.startSize = 0.22f;
            flash.Emit(p, 1);
            p.startColor = new Color(1f, 0.9f, 0.7f, 1f);
            p.startSize = 0.09f;
            flash.Emit(p, 1);
            for (int i = 0; i < 4; i++)
            {
                p.position = pos + dir * Random.Range(0.02f, 0.12f);
                p.startSize = Random.Range(0.05f, 0.11f);
                p.startColor = new Color(1f, 0.6f, 0.25f, 0.8f);
                flash.Emit(p, 1);
            }
        }

        public void Impact(RaycastHit hit, bool metal)
        {
            Vector3 n = hit.normal;
            int sparkCount = metal ? 14 : 6;
            for (int i = 0; i < sparkCount; i++)
            {
                var p = new ParticleSystem.EmitParams
                {
                    position = hit.point + n * 0.01f,
                    velocity = (n + Random.insideUnitSphere * 0.9f).normalized * Random.Range(2.5f, 7f),
                    startLifetime = Random.Range(0.15f, 0.45f),
                    startSize = Random.Range(0.012f, 0.025f),
                    startColor = new Color(1f, Random.Range(0.55f, 0.8f), 0.3f, 1f),
                };
                sparks.Emit(p, 1);
            }
            for (int i = 0; i < 5; i++)
            {
                var p = new ParticleSystem.EmitParams
                {
                    position = hit.point + n * 0.05f,
                    velocity = (n + Random.insideUnitSphere * 0.5f) * Random.Range(0.3f, 1.2f),
                    startLifetime = Random.Range(0.6f, 1.2f),
                    startSize = Random.Range(0.15f, 0.35f),
                    startColor = new Color(0.55f, 0.52f, 0.48f, 0.45f),
                    rotation = Random.Range(0f, 360f),
                };
                dust.Emit(p, 1);
            }
        }

        public void BulletHole(RaycastHit hit)
        {
            var t = holes[nextHole];
            nextHole = (nextHole + 1) % holes.Length;
            t.gameObject.SetActive(true);
            t.SetParent(hit.collider.transform, true);
            t.position = hit.point + hit.normal * 0.003f;
            t.rotation = Quaternion.LookRotation(-hit.normal) * Quaternion.Euler(0, 0, Random.Range(0f, 360f));
            float size = Random.Range(0.06f, 0.09f);
            var ls = hit.collider.transform.lossyScale;
            t.localScale = new Vector3(size / ls.x, size / ls.y, size / ls.z);
        }

        ParticleSystem CreateSystem(string name, Material mat, bool stretched, float gravity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 1000;
            main.gravityModifier = gravity;

            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            if (!stretched)
            {
                var size = ps.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, gravity < 0 ? 2.5f : 0.6f));
            }

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (stretched)
            {
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.velocityScale = 0.025f;
                r.lengthScale = 1f;
            }
            ps.Play();
            return ps;
        }

        static Mesh BuildQuad()
        {
            var m = new Mesh { name = "BulletHoleQuad" };
            m.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
