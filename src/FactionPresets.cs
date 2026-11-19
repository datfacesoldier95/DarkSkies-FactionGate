using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace FactionGate
{
    public sealed class FactionPreset
    {
        public string Id;
        public string DisplayName;
        public string Tag;
        public string Flag;
        public string FlagPath;
        public Sprite Sprite;
        public Texture2D Texture;
    }

    public sealed class PresetFile
    {
        public List<PresetEntry> presets;
    }

    public sealed class PresetEntry
    {
        public string id;
        public string displayName;
        public string tag;
        public string flag;
    }

    public static class FactionPresets
    {
        public const string DefaultId = "Default";

        private static readonly Dictionary<string, FactionPreset> ById =
            new Dictionary<string, FactionPreset>(StringComparer.OrdinalIgnoreCase);

        private static readonly List<string> OrderedIds = new List<string>();
        private static string _rootDir;
        private static GameObject _spriteHost;

        public static IReadOnlyList<string> Ids => OrderedIds;
        public static string[] AcceptableValues
        {
            get
            {
                var list = new List<string> { DefaultId };
                list.AddRange(OrderedIds);
                return list.ToArray();
            }
        }

        public static void SetRoot(string pluginDir)
        {
            _rootDir = pluginDir ?? "";
        }

        public static void Reload()
        {
            foreach (var p in ById.Values)
                DestroySprite(p);

            ById.Clear();
            OrderedIds.Clear();

            var presetsPath = Path.Combine(_rootDir ?? "", "presets.json");
            if (!File.Exists(presetsPath))
            {
                FactionGatePlugin.Log?.LogWarning($"FactionGate presets.json missing: {presetsPath}");
                return;
            }

            try
            {
                var json = File.ReadAllText(presetsPath);
                var file = JsonConvert.DeserializeObject<PresetFile>(json);
                if (file?.presets == null)
                {
                    FactionGatePlugin.Log?.LogWarning("FactionGate presets.json has no presets array.");
                    return;
                }

                var flagsDir = Path.Combine(_rootDir ?? "", "Flags");
                foreach (var entry in file.presets)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.id))
                        continue;
                    if (string.Equals(entry.id, DefaultId, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var preset = new FactionPreset
                    {
                        Id = entry.id.Trim(),
                        DisplayName = string.IsNullOrWhiteSpace(entry.displayName)
                            ? entry.id.Trim()
                            : entry.displayName.Trim(),
                        Tag = string.IsNullOrWhiteSpace(entry.tag)
                            ? entry.id.Trim().ToUpperInvariant()
                            : entry.tag.Trim().ToUpperInvariant(),
                        Flag = entry.flag ?? "",
                    };

                    if (!string.IsNullOrWhiteSpace(entry.flag))
                        preset.FlagPath = Path.Combine(flagsDir, entry.flag);

                    EnsureSprite(preset);

                    ById[preset.Id] = preset;
                    OrderedIds.Add(preset.Id);
                }

                FactionGatePlugin.Log?.LogInfo(
                    $"FactionGate loaded {OrderedIds.Count} preset(s) from {presetsPath}");
            }
            catch (Exception ex)
            {
                FactionGatePlugin.Log?.LogError($"FactionGate failed to load presets: {ex}");
            }
        }

        public static bool TryGet(string id, out FactionPreset preset)
        {
            preset = null;
            if (string.IsNullOrWhiteSpace(id) ||
                string.Equals(id, DefaultId, StringComparison.OrdinalIgnoreCase))
                return false;
            if (!ById.TryGetValue(id.Trim(), out preset) || preset == null)
                return false;

            EnsureSprite(preset);
            return preset.Sprite != null;
        }

        public static string NormalizeId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return DefaultId;
            id = id.Trim();
            if (string.Equals(id, DefaultId, StringComparison.OrdinalIgnoreCase))
                return DefaultId;
            return ById.ContainsKey(id) ? ById[id].Id : DefaultId;
        }

        /// <summary>Reload from disk if Unity destroyed the runtime texture after a scene change.</summary>
        public static Sprite EnsureSprite(FactionPreset preset)
        {
            if (preset == null)
                return null;

            if (preset.Sprite != null)
            {
                try
                {
                    // Destroyed Unity objects compare equal to null via overloaded operator.
                    if (preset.Sprite)
                        return preset.Sprite;
                }
                catch
                {
                    /* fall through and recreate */
                }
            }

            DestroySprite(preset);

            if (!string.IsNullOrEmpty(preset.FlagPath) && File.Exists(preset.FlagPath))
                AssignLoaded(preset, LoadSprite(preset.FlagPath, preset.Id));
            else
                AssignLoaded(preset, MakePlaceholderSprite(preset.Id));

            return preset.Sprite;
        }

        private static void AssignLoaded(FactionPreset preset, Sprite sprite)
        {
            preset.Sprite = sprite;
            preset.Texture = sprite != null ? sprite.texture : null;
            KeepAlive(preset.Texture);
            KeepAlive(preset.Sprite);
        }

        private static void KeepAlive(UnityEngine.Object obj)
        {
            if (obj == null)
                return;
            obj.hideFlags = HideFlags.HideAndDontSave;
            EnsureSpriteHost();
        }

        private static void EnsureSpriteHost()
        {
            if (_spriteHost != null)
                return;
            _spriteHost = new GameObject("FactionGate_SpriteHost");
            UnityEngine.Object.DontDestroyOnLoad(_spriteHost);
            _spriteHost.hideFlags = HideFlags.HideAndDontSave;
        }

        private static void DestroySprite(FactionPreset preset)
        {
            if (preset == null)
                return;
            try
            {
                if (preset.Sprite != null)
                    UnityEngine.Object.Destroy(preset.Sprite);
            }
            catch
            {
                /* ignore */
            }

            try
            {
                if (preset.Texture != null)
                    UnityEngine.Object.Destroy(preset.Texture);
            }
            catch
            {
                /* ignore */
            }

            preset.Sprite = null;
            preset.Texture = null;
        }

        private static Sprite LoadSprite(string path, string name)
        {
            try
            {
                var bytes = File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(tex, bytes, false))
                {
                    UnityEngine.Object.Destroy(tex);
                    FactionGatePlugin.Log?.LogWarning($"FactionGate could not decode flag: {path}");
                    return MakePlaceholderSprite(name);
                }

                tex.name = "FactionGate_" + name;
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                tex.hideFlags = HideFlags.HideAndDontSave;
                var sprite = Sprite.Create(
                    tex,
                    new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                sprite.name = "FactionGateSprite_" + name;
                sprite.hideFlags = HideFlags.HideAndDontSave;
                return sprite;
            }
            catch (Exception ex)
            {
                FactionGatePlugin.Log?.LogWarning($"FactionGate load flag failed ({path}): {ex.Message}");
                return MakePlaceholderSprite(name);
            }
        }

        private static Sprite MakePlaceholderSprite(string name)
        {
            var hash = (name ?? "x").Aggregate(0, (h, c) => unchecked(h * 31 + c));
            var r = (byte)(64 + (hash & 0x7F));
            var g = (byte)(64 + ((hash >> 7) & 0x7F));
            var b = (byte)(64 + ((hash >> 14) & 0x7F));
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var pixels = Enumerable.Repeat(new Color32(r, g, b, 255), 64 * 64).ToArray();
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            tex.name = "FactionGate_ph_" + name;
            tex.hideFlags = HideFlags.HideAndDontSave;
            var sprite = Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
