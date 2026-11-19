using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace FactionGate
{
    public sealed class SkinProfilesFile
    {
        /// <summary>Legacy master switch (pre-1.2). Migrated onto per-faction enabled flags.</summary>
        public bool enabled;

        public Dictionary<string, SkinFactionProfile> factions =
            new Dictionary<string, SkinFactionProfile>(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class SkinFactionProfile
    {
        public bool enabled;
        public Dictionary<string, SkinAirframeProfile> airframes =
            new Dictionary<string, SkinAirframeProfile>(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class SkinAirframeProfile
    {
        public bool allowVanilla = true;
        public List<string> allowedWorkshopIds = new List<string>();
    }

    public static class SkinProfileStore
    {
        private static string _path;
        private static SkinProfilesFile _data = new SkinProfilesFile();
        private static bool _loadedFromHost;

        public static SkinProfilesFile Data => _data;
        public static string FilePath => _path;

        /// <summary>True if any faction profile is enabled (legacy helper / logging).</summary>
        public static bool Enabled
        {
            get
            {
                if (_data?.factions == null)
                    return false;
                foreach (var kv in _data.factions)
                {
                    if (kv.Value != null && kv.Value.enabled)
                        return true;
                }

                return _data.enabled;
            }
        }

        public static void SetRoot(string pluginDir)
        {
            _path = Path.Combine(pluginDir ?? "", "skin-profiles.json");
        }

        public static void ReloadFromDisk()
        {
            if (string.IsNullOrEmpty(_path) || !File.Exists(_path))
            {
                _data = new SkinProfilesFile();
                return;
            }

            try
            {
                var json = File.ReadAllText(_path);
                ApplyJson(json, fromHost: false);
            }
            catch (Exception ex)
            {
                FactionGatePlugin.Log?.LogError($"FactionGate skin-profiles load failed: {ex}");
                _data = new SkinProfilesFile();
            }
        }

        public static void ApplyJson(string json, bool fromHost)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                _data = new SkinProfilesFile();
                return;
            }

            var parsed = JsonConvert.DeserializeObject<SkinProfilesFile>(json);
            _data = parsed ?? new SkinProfilesFile();
            if (_data.factions == null)
                _data.factions = new Dictionary<string, SkinFactionProfile>(StringComparer.OrdinalIgnoreCase);

            NormalizeLoadedData();
            _loadedFromHost = fromHost;
            FactionGatePlugin.Log?.LogInfo(
                $"FactionGate skin-profiles applied (host={fromHost}, anyEnabled={Enabled}, factions={_data.factions.Count})");
        }

        private static void NormalizeLoadedData()
        {
            foreach (var kv in _data.factions)
            {
                var faction = kv.Value;
                if (faction == null)
                    continue;
                if (faction.airframes == null)
                    faction.airframes = new Dictionary<string, SkinAirframeProfile>(StringComparer.OrdinalIgnoreCase);
                foreach (var airKv in faction.airframes)
                {
                    if (airKv.Value == null)
                        continue;
                    if (airKv.Value.allowedWorkshopIds == null)
                        airKv.Value.allowedWorkshopIds = new List<string>();
                }
            }

            // Pre-1.2 files used a global enabled flag and no per-faction enabled.
            // If the legacy flag is on, turn on every faction that already has airframe rules.
            if (_data.enabled)
            {
                foreach (var kv in _data.factions)
                {
                    if (kv.Value == null)
                        continue;
                    if (kv.Value.airframes != null && kv.Value.airframes.Count > 0)
                        kv.Value.enabled = true;
                }
            }

            RefreshLegacyEnabledFlag();
        }

        private static void RefreshLegacyEnabledFlag()
        {
            var any = false;
            if (_data?.factions != null)
            {
                foreach (var kv in _data.factions)
                {
                    if (kv.Value != null && kv.Value.enabled)
                    {
                        any = true;
                        break;
                    }
                }
            }

            _data.enabled = any;
        }

        public static string ToJson()
        {
            RefreshLegacyEnabledFlag();
            return JsonConvert.SerializeObject(_data ?? new SkinProfilesFile(), Formatting.Indented);
        }

        public static void SaveToDisk()
        {
            if (string.IsNullOrEmpty(_path))
                return;
            if (FactionSession.IsClientNonHost)
                return;

            try
            {
                File.WriteAllText(_path, ToJson());
            }
            catch (Exception ex)
            {
                FactionGatePlugin.Log?.LogWarning($"FactionGate skin-profiles save failed: {ex.Message}");
            }
        }

        public static bool IsFactionEnabled(string factionId)
        {
            if (string.IsNullOrWhiteSpace(factionId) ||
                string.Equals(factionId, FactionPresets.DefaultId, StringComparison.OrdinalIgnoreCase))
                return false;
            if (_data?.factions == null)
                return false;
            return _data.factions.TryGetValue(factionId, out var faction) &&
                   faction != null &&
                   faction.enabled;
        }

        public static SkinFactionProfile CloneFactionProfile(string factionId)
        {
            factionId = (factionId ?? "").Trim();
            var clone = new SkinFactionProfile
            {
                enabled = false,
                airframes = new Dictionary<string, SkinAirframeProfile>(StringComparer.OrdinalIgnoreCase),
            };

            if (string.IsNullOrEmpty(factionId) ||
                _data?.factions == null ||
                !_data.factions.TryGetValue(factionId, out var src) ||
                src == null)
                return clone;

            clone.enabled = src.enabled;
            if (src.airframes == null)
                return clone;

            foreach (var kv in src.airframes)
            {
                if (kv.Value == null)
                    continue;
                clone.airframes[kv.Key] = CloneAirframe(kv.Value);
            }

            return clone;
        }

        public static SkinAirframeProfile GetAirframeFromFaction(SkinFactionProfile faction, string airframeKey)
        {
            if (faction == null)
                return new SkinAirframeProfile();
            if (faction.airframes == null)
                faction.airframes = new Dictionary<string, SkinAirframeProfile>(StringComparer.OrdinalIgnoreCase);

            airframeKey = (airframeKey ?? "").Trim();
            if (string.IsNullOrEmpty(airframeKey))
                return new SkinAirframeProfile();

            if (!faction.airframes.TryGetValue(airframeKey, out var air) || air == null)
            {
                air = new SkinAirframeProfile { allowVanilla = true };
                faction.airframes[airframeKey] = air;
            }

            if (air.allowedWorkshopIds == null)
                air.allowedWorkshopIds = new List<string>();

            return air;
        }

        /// <summary>Overwrite one faction profile from an editor draft, persist, ready to broadcast.</summary>
        public static void SaveFactionPreset(string factionId, SkinFactionProfile draft)
        {
            if (FactionSession.IsClientNonHost)
                return;

            factionId = (factionId ?? "").Trim();
            if (string.IsNullOrEmpty(factionId) || draft == null)
                return;

            if (_data == null)
                _data = new SkinProfilesFile();
            if (_data.factions == null)
                _data.factions = new Dictionary<string, SkinFactionProfile>(StringComparer.OrdinalIgnoreCase);

            _data.factions[factionId] = CloneFactionProfileData(draft);
            SaveToDisk();
            FactionGatePlugin.Log?.LogInfo(
                $"FactionGate saved skin preset for {factionId} (enabled={draft.enabled}, airframes={draft.airframes?.Count ?? 0})");
        }

        public static bool TryGet(string factionId, string airframeKey, out SkinAirframeProfile profile)
        {
            profile = null;
            if (_data?.factions == null)
                return false;
            if (!_data.factions.TryGetValue(factionId ?? "", out var faction) || faction?.airframes == null)
                return false;
            return faction.airframes.TryGetValue(airframeKey ?? "", out profile) && profile != null;
        }

        public static bool IsWorkshopAllowed(string factionId, string airframeKey, ulong workshopId)
        {
            if (!IsFactionEnabled(factionId))
                return true;
            if (!TryGet(factionId, airframeKey, out var profile))
                return false;
            var id = workshopId.ToString();
            foreach (var allowed in profile.allowedWorkshopIds)
            {
                if (string.Equals(allowed, id, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        public static bool IsVanillaAllowed(string factionId, string airframeKey)
        {
            if (!IsFactionEnabled(factionId))
                return true;
            if (!TryGet(factionId, airframeKey, out var profile))
                return false;
            return profile.allowVanilla;
        }

        private static SkinFactionProfile CloneFactionProfileData(SkinFactionProfile src)
        {
            var clone = new SkinFactionProfile
            {
                enabled = src.enabled,
                airframes = new Dictionary<string, SkinAirframeProfile>(StringComparer.OrdinalIgnoreCase),
            };
            if (src.airframes == null)
                return clone;
            foreach (var kv in src.airframes)
            {
                if (kv.Value == null)
                    continue;
                clone.airframes[kv.Key] = CloneAirframe(kv.Value);
            }

            return clone;
        }

        private static SkinAirframeProfile CloneAirframe(SkinAirframeProfile src)
        {
            return new SkinAirframeProfile
            {
                allowVanilla = src.allowVanilla,
                allowedWorkshopIds = src.allowedWorkshopIds != null
                    ? new List<string>(src.allowedWorkshopIds)
                    : new List<string>(),
            };
        }
    }
}
