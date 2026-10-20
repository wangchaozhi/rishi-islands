using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Rishi.Core;
using Rishi.Inventory;
using Rishi.World;
using Rishi.Audio;
using Rishi.Player;
using Rishi.Weapons;
using Rishi.UI;
using Rishi.Story;
using Rishi.Cutscene;
using Rishi.Wildlife;
using Rishi.Rendering;

namespace Rishi.Builders
{
    /// <summary>
    /// 编辑器工具：扫描音效文件夹，更新音效表 Assets/Config/SoundLibrary.asset（同名带 _1/_2… 后缀的文件归为同一个音效的多个变体），
    /// 并生成场景里的 AudioManager。
    /// 正式音效到位后，把文件放进 Assets/Audio/Final 并用相同的文件名，就会优先使用正式音效。
    /// </summary>
    public static class AudioSetup
    {
        static readonly string[] Folders = { "Assets/Audio/Final", "Assets/Audio/Placeholder" };

        // 新音效的默认参数：id 前缀 -> (分类, 音量, 最小距离, 最大距离)
        static readonly (string prefix, SoundCategory cat, float vol, float min, float max)[] Rules =
        {
            ("amb_", SoundCategory.Ambience, 1f, 1f, 50f),
            ("step_", SoundCategory.Sfx, 0.55f, 1f, 20f),
            ("gun_shot", SoundCategory.Sfx, 0.85f, 2f, 120f),
            ("gun_", SoundCategory.Sfx, 0.7f, 1f, 15f),
            ("impact_", SoundCategory.Sfx, 0.7f, 1f, 35f),
            ("stone_door", SoundCategory.Sfx, 1f, 6f, 80f),
            ("stone_", SoundCategory.Sfx, 0.9f, 2f, 30f),
            ("puzzle_", SoundCategory.Music, 0.8f, 5f, 80f),
            ("fragment_", SoundCategory.Music, 0.8f, 1f, 30f),
            ("note_", SoundCategory.Sfx, 0.6f, 1f, 10f),
            ("chest_", SoundCategory.Sfx, 0.9f, 1.5f, 25f),
            ("crab_", SoundCategory.Sfx, 0.45f, 1f, 12f),
            ("fish_", SoundCategory.Sfx, 0.6f, 2f, 30f),
            ("bat_", SoundCategory.Sfx, 0.45f, 2f, 40f),
            ("knock_three", SoundCategory.Sfx, 1f, 6f, 80f),
            ("heartbeat", SoundCategory.Sfx, 0.9f, 1f, 5f),
            ("splash_", SoundCategory.Sfx, 0.8f, 1.5f, 30f),
            ("water_exit", SoundCategory.Sfx, 0.6f, 1f, 15f),
            ("swim_", SoundCategory.Sfx, 0.6f, 1f, 10f),
            ("dive_", SoundCategory.Sfx, 0.7f, 1f, 10f),
            ("surface_", SoundCategory.Sfx, 0.7f, 1f, 10f),
            ("bubbles", SoundCategory.Sfx, 0.5f, 1f, 8f),
            ("gasp", SoundCategory.Voice, 0.9f, 1f, 5f),
            ("drown", SoundCategory.Voice, 1f, 1f, 5f),
            ("item_", SoundCategory.Sfx, 0.7f, 1f, 8f),
            ("bag_", SoundCategory.Sfx, 0.6f, 1f, 5f),
            ("ui_", SoundCategory.Sfx, 0.5f, 1f, 5f),
            ("weapon_", SoundCategory.Sfx, 0.6f, 1f, 8f),
            ("machete_", SoundCategory.Sfx, 0.8f, 1f, 25f),
            ("speargun_", SoundCategory.Sfx, 0.85f, 1f, 30f),
            ("spear_hit", SoundCategory.Sfx, 0.8f, 1f, 30f),
            ("clam_", SoundCategory.Sfx, 0.8f, 1f, 15f),
            ("fish_scatter", SoundCategory.Sfx, 0.5f, 1f, 15f),
            ("flashlight_", SoundCategory.Sfx, 0.7f, 1f, 5f),
            ("fire_", SoundCategory.Sfx, 0.85f, 1.5f, 20f),
            ("creature_", SoundCategory.Sfx, 0.9f, 2f, 40f),
            ("shade_scream", SoundCategory.Sfx, 1f, 4f, 70f),
            ("shade_", SoundCategory.Sfx, 0.9f, 2.5f, 40f),
            ("peter_", SoundCategory.Sfx, 1f, 3f, 40f),
            ("boar_", SoundCategory.Sfx, 0.9f, 2f, 40f),
            ("bush_", SoundCategory.Sfx, 0.9f, 1.5f, 25f),
            ("boat_", SoundCategory.Sfx, 0.8f, 2f, 40f),
            ("ccrab_", SoundCategory.Sfx, 0.85f, 1.5f, 25f),
            ("player_", SoundCategory.Voice, 0.9f, 1f, 5f),
            ("breath_", SoundCategory.Voice, 0.8f, 1f, 5f),
            ("battery_", SoundCategory.Sfx, 0.7f, 1f, 5f),
        };

        public static AudioManager CreateManager()
        {
            var clips = new Dictionary<string, List<AudioClip>>();
            foreach (var folder in Folders)
            {
                if (!AssetDatabase.IsValidFolder(folder)) continue;
                foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    string name = Path.GetFileNameWithoutExtension(path);
                    var m = Regex.Match(name, @"^(.+)_\d+$");
                    string id = m.Success ? m.Groups[1].Value : name;
                    if (clips.ContainsKey(id) && folder.EndsWith("Placeholder") && clips[id].Any(c => AssetDatabase.GetAssetPath(c).Contains("/Final/"))) continue;
                    if (!clips.TryGetValue(id, out var list)) clips[id] = list = new List<AudioClip>();
                    list.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(path));
                    ConfigureImport(path, id);
                }
            }

            // 商店音效（Water Splash Pack, Rocklynn Productions）替换对应的临时音效
            const string splash = "Assets/Rocklynn Productions - Water Splash Pack/";
            var store = new Dictionary<string, string[]>
            {
                ["splash_enter"] = new[] { "WaterSplash_In_Short1", "WaterSplash_In_Short2", "WaterSplash_In_Short3", "WaterSplash_In_Short4", "WaterSplash_In_Short5" },
                ["splash_big"] = new[] { "WaterSplash_Long", "WaterSplash_Medium" },
                ["splash_small"] = new[] { "WaterSplash_In_Short3", "WaterSplash_In_Short5" },
                ["water_exit"] = new[] { "WaterSplash_Out_Short1", "WaterSplash_Out_Short2", "WaterSplash_Out_Short3" },
                ["surface_breach"] = new[] { "WaterEmerge_Out_Medium", "WaterEmerge_Out_Slow" },
                ["dive_under"] = new[] { "WaterSubmerge_In", "WaterSplash_Underwater" },
                ["swim_stroke"] = new[] { "WaterSplash_Out_Short1", "WaterSplash_Out_Short2", "WaterSplash_Out_Short3" },
                ["swim_under"] = new[] { "Underwater_Medium" },
                ["amb_underwater"] = new[] { "Underwater_Long" },
            };
            foreach (var kv in store)
            {
                var list = kv.Value.Select(n => AssetDatabase.LoadAssetAtPath<AudioClip>(splash + n + ".wav")).Where(c => c != null).ToList();
                if (list.Count == 0) continue;
                foreach (var c in list) ConfigureImport(AssetDatabase.GetAssetPath(c), kv.Key);
                clips[kv.Key] = list;
            }

            // 更新音效表：已有条目只换音频文件，保留 Inspector 里调过的参数；新 id 按前缀规则给默认值
            var library = ConfigAssets.Sounds;
            library.sounds.RemoveAll(s => !clips.ContainsKey(s.id));
            foreach (var kv in clips.OrderBy(k => k.Key))
            {
                var entry = library.Find(kv.Key);
                if (entry != null) { entry.clips = kv.Value.ToArray(); continue; }
                entry = new SoundLibrary.Sound { id = kv.Key, clips = kv.Value.ToArray() };
                var rule = Rules.FirstOrDefault(r => kv.Key.StartsWith(r.prefix));
                if (rule.prefix != null)
                {
                    entry.category = rule.cat;
                    entry.volume = rule.vol;
                    entry.minDistance = rule.min;
                    entry.maxDistance = rule.max;
                }
                if (kv.Key.StartsWith("amb_") || kv.Key.StartsWith("puzzle_") || kv.Key.StartsWith("fragment_") || kv.Key == "heartbeat" || kv.Key == "knock_three") entry.pitch = Vector2.one;
                library.sounds.Add(entry);
            }
            library.sounds.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            EditorUtility.SetDirty(library);

            var go = new GameObject("AudioManager");
            var am = go.AddComponent<AudioManager>();
            am.library = library;
            Debug.Log($"[Audio] {library.sounds.Count} sounds: {string.Join(", ", library.sounds.Select(s => s.id + (s.clips.Length > 1 ? $"×{s.clips.Length}" : "")))}");
            return am;
        }

        static void ConfigureImport(string path, string id)
        {
            if (!(AssetImporter.GetAtPath(path) is AudioImporter ai)) return;
            var settings = ai.defaultSampleSettings;
            var want = id.StartsWith("amb_") ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            if (settings.loadType == want && ai.forceToMono) return;
            settings.loadType = want;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            ai.defaultSampleSettings = settings;
            ai.forceToMono = true;
            ai.SaveAndReimport();
        }

        /// <summary>给玩家加上脚步声和环境声。</summary>
        public static void SetupPlayer(GameObject player, DayNightCycle dayNight)
        {
            player.AddComponent<Footsteps>();
            if (dayNight != null) player.AddComponent<AmbientController>().dayNight = dayNight;
        }

        public static void Tag(GameObject go, string surface) => go.AddComponent<SurfaceTag>().surface = surface;

        public static void ReverbZone(Transform parent, Vector3 localPos, float min, float max, AudioReverbPreset preset)
        {
            var z = new GameObject("ReverbZone").AddComponent<AudioReverbZone>();
            z.transform.SetParent(parent, false);
            z.transform.localPosition = localPos;
            z.minDistance = min;
            z.maxDistance = max;
            z.reverbPreset = preset;
        }
    }
}
