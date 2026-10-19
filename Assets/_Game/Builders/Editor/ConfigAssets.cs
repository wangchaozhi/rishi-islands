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
            _ = DayNight; _ = Wind; _ = Storm; _ = Wildlife; _ = Sounds; // 没有的先建出默认配置
            AssetDatabase.SaveAssets();
            var folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(Folder);
            Selection.activeObject = folder;
            EditorGUIUtility.PingObject(folder);
        }
    }
}
