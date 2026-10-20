using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Rishi.Inventory;
using Rishi.World;

namespace Rishi.Builders
{
    /// <summary>
    /// 可以用砍刀砍开的灌木丛：丛林里散落约 30 丛挡路的密灌木；营地附近的林子里还有一圈灌木围住的补给箱
    /// （父亲藏的电池和子弹），得砍开才能进去。
    /// </summary>
    public static class ThicketBuilder
    {
        public static void Build(Terrain terrain, Vector3 spawn)
        {
            var root = new GameObject("Thickets").transform;
            var leafMat = LeafMaterial();
            var rng = new System.Random(2468);
            float R() => (float)rng.NextDouble();
            float H(Vector3 p) => terrain.SampleHeight(p) + terrain.transform.position.y;

            // 丛林里散落的
            int placed = 0;
            for (int i = 0; i < 6000 && placed < 30; i++)
            {
                var p = new Vector3((R() - 0.5f) * 500f, 0, (R() - 0.5f) * 500f);
                p.y = H(p);
                if (p.y < 4f || IslandStoryBuilder.InClearZone(p.x, p.z, 3f)) continue;
                if (Vector3.Distance(new Vector3(p.x, 0, p.z), new Vector3(spawn.x, 0, spawn.z)) < 30f) continue;
                if (Rishi.Core.SurfaceTag.TerrainSurface(terrain, p) != "leaves") continue;
                Thicket(root, p, R() * 360f, 0.9f + R() * 0.4f, leafMat, rng);
                placed++;
            }

            // 营地附近被灌木围住的补给箱
            var camp = GameObject.Find("FatherCamp");
            if (camp != null)
            {
                var c = camp.transform;
                var cache = c.TransformPoint(new Vector3(-9f, 0, 7f));
                cache.y = H(cache);
                var ring = new GameObject("HiddenCache").transform;
                ring.SetParent(root, true);
                ring.position = cache;
                for (int k = 0; k < 7; k++)
                {
                    float a = k / 7f * Mathf.PI * 2f;
                    var p = cache + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 2.1f;
                    p.y = H(p);
                    Thicket(ring, p, a * Mathf.Rad2Deg, 1.05f, leafMat, rng);
                }
                Crate(ring, cache, c.eulerAngles.y + 20f);
            }
            Debug.Log($"[Thickets] 散落 {placed} 丛，营地旁补给箱 {(camp != null ? "已放" : "没找到营地")}");
        }

        /// <summary>一丛密灌木：三四棵程序化高灌木挤在一起，带碰撞体（挡路）和砍的时候溅出的碎叶。</summary>
        static void Thicket(Transform parent, Vector3 pos, float yaw, float size, Material leafMat, System.Random rng)
        {
            var go = new GameObject("Thicket");
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            int n = 3 + rng.Next(2);
            for (int i = 0; i < n; i++)
            {
                var (mesh, mat) = JunglePlants.TallShrub(rng.Next(4));
                var s = new GameObject("Shrub");
                s.transform.SetParent(go.transform, false);
                float a = i / (float)n * Mathf.PI * 2f + (float)rng.NextDouble();
                s.transform.localPosition = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.45f * size;
                s.transform.localRotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                s.transform.localScale = Vector3.one * size * (0.9f + (float)rng.NextDouble() * 0.3f);
                s.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = s.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.On;
            }
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 1.1f * size, 0);
            col.size = new Vector3(1.7f, 2.2f, 1.7f) * size;
            go.AddComponent<Rishi.Core.SurfaceTag>().surface = "leaves";
            var chop = go.AddComponent<Choppable>();
            chop.hits = 3;
            chop.leaves = Leaves(go.transform, leafMat);
        }

        static ParticleSystem Leaves(Transform parent, Material mat)
        {
            var go = new GameObject("ChopLeaves");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0, 1f, 0);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.22f, 0.4f, 0.12f), new Color(0.36f, 0.5f, 0.16f));
            main.gravityModifier = 0.5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return ps;
        }

        /// <summary>碎叶的粒子材质：不透明的小色块（Lit 粒子，受光照）。</summary>
        static Material LeafMaterial()
        {
            const string path = "Assets/Art/Generated/Materials/ChopLeaf.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Particles/Lit"));
            m.SetColor("_BaseColor", Color.white);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>父亲藏的补给箱：两节电池、一盒子弹、一个椰子，还有一张纸条。</summary>
        static void Crate(Transform parent, Vector3 pos, float yaw)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Generated/Materials/Crate.mat");
            var crate = FPSSceneBuilder.PlaceModel("Models/wooden_crate_02", mat, parent, pos, yaw, staticCollider: true);
            crate.name = "CacheCrate";
            var loot = crate.AddComponent<LootContainer>();
            loot.displayName = "父亲藏的补给箱";
            loot.verb = "打开";
            loot.contents = new List<PlayerInventory.Stack>
            {
                new PlayerInventory.Stack { item = GameplaySetup.Item("battery"), count = 2 },
                new PlayerInventory.Stack { item = GameplaySetup.Item("ammo_9mm"), count = 10 },
                new PlayerInventory.Stack { item = GameplaySetup.Item("coconut"), count = 1 },
            };
            loot.openMessage = "箱底压着一张父亲的纸条：「夜里别走大路。它们沿着路走。」";
        }
    }
}
