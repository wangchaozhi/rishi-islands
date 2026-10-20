using System;
using UnityEditor;
using UnityEngine;
using Rishi.Audio;
using Rishi.World;
using Rishi.Wildlife;

namespace Rishi.Builders
{
    /// <summary>
    /// 配置资产（ScriptableObject）都放在 Assets/Config。只在不存在时创建默认值，
    /// 重建场景时不会覆盖策划在 Inspector 里调过的数值。
    /// </summary>
    public static class ConfigAssets
    {
        public const string Folder = "Assets/Config";

        public static DayNightConfig DayNight => LoadOrCreate<DayNightConfig>("DayNight");
        public static WindConfig Wind => LoadOrCreate<WindConfig>("Wind");
        public static WeatherProfile Storm => LoadOrCreate("Weather_Storm", WeatherProfile.CreateStorm);
        public static WildlifeConfig Wildlife => LoadOrCreate<WildlifeConfig>("Wildlife");
        public static SoundLibrary Sounds => LoadOrCreate<SoundLibrary>("SoundLibrary");

        /// <summary>动态天气的调度和六种天气（Weather_Clear / Cloudy / Overcast / Rain / Thunderstorm / Fog）。</summary>
        public static WeatherConfig Weather => LoadOrCreate("WeatherConfig", () =>
        {
            var c = ScriptableObject.CreateInstance<WeatherConfig>();
            foreach (var (id, weight, night, frag) in WeatherConfig.DefaultEntries)
                c.weathers.Add(new WeatherConfig.Entry { profile = WeatherProfileAsset(id), weight = weight, nightMultiplier = night, perFragment = frag });
            c.startWeather = c.weathers[0].profile;
            return c;
        });

        /// <summary>测试生物（F10 生成），用来试生物框架。</summary>
        public static Rishi.Creatures.CreatureDef TestCreature => LoadOrCreate("Creature_Test", () =>
        {
            var c = ScriptableObject.CreateInstance<Rishi.Creatures.CreatureDef>();
            c.displayName = "测试生物";
            c.lightReaction = Rishi.Creatures.LightReaction.Flinch;
            return c;
        });

        public static WeatherProfile WeatherProfileAsset(string id) => LoadOrCreate("Weather_" + id, () => WeatherProfile.CreateDefault(id));

        /// <summary>
        /// 发光材质：URP Unlit + HDR 底色（泛光会把它晕开）。不用 Lit 的自发光，因为 Lit 的 _EMISSION 关键字
        /// 在批处理生成时会被清掉，存下来的材质就不发光了。已存在时更新颜色。
        /// </summary>
        public static UnityEngine.Material Glow(string name, UnityEngine.Color hdr)
        {
            string path = $"Assets/Art/Generated/Materials/{name}.mat";
            var shader = UnityEngine.Shader.Find("Universal Render Pipeline/Unlit");
            var m = AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(path);
            if (m == null) { m = new UnityEngine.Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader;
            m.SetColor("_BaseColor", hdr);
            EditorUtility.SetDirty(m);
            return m;
        }

        public static T LoadOrCreate<T>(string name, Func<T> create = null) where T : ScriptableObject
        {
            string path = $"{Folder}/{name}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "Config");
            asset = create != null ? create() : ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            Debug.Log($"[Config] 新建配置 {path}");
            return asset;
        }

        [MenuItem("Tools/Island/打开配置文件夹")]
        static void Reveal()
        {
            _ = DayNight; _ = Wind; _ = Storm; _ = Wildlife; _ = Sounds; _ = Weather; // 没有的先建出默认配置
            AssetDatabase.SaveAssets();
            var folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(Folder);
            Selection.activeObject = folder;
            EditorGUIUtility.PingObject(folder);
        }
    }
}
