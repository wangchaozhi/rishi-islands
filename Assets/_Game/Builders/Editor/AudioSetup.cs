using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Rishi.Core;
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
    /// 编辑器工具：扫描音效文件夹，生成场景里的 AudioManager（同名带 _1/_2… 后缀的文件归为同一个音效的多个变体）。
    /// 正式音效到位后，把文件放进 Assets/Audio/Final 并用相同的文件名，就会优先使用正式音效。
    /// </summary>
    public static class AudioSetup
    {
        static readonly string[] Folders = { "Assets/Audio/Final", "Assets/Audio/Placeholder" };

        // id 前缀 -> (分类, 音量, 最小距离, 最大距离)
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

            var go = new GameObject("AudioManager");
            var am = go.AddComponent<AudioManager>();
            foreach (var kv in clips.OrderBy(k => k.Key))
            {
                var rule = Rules.FirstOrDefault(r => kv.Key.StartsWith(r.prefix));
                var s = new AudioManager.Sound { id = kv.Key, clips = kv.Value.ToArray() };
                if (rule.prefix != null)
                {
                    s.category = rule.cat;
                    s.volume = rule.vol;
                    s.minDistance = rule.min;
                    s.maxDistance = rule.max;
                }
                if (kv.Key.StartsWith("amb_") || kv.Key.StartsWith("puzzle_") || kv.Key.StartsWith("fragment_")) s.pitch = Vector2.one;
                am.sounds.Add(s);
            }
            Debug.Log($"[Audio] {am.sounds.Count} sounds: {string.Join(", ", am.sounds.Select(s => s.id + (s.clips.Length > 1 ? $"×{s.clips.Length}" : "")))}");
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
