using System;
using System.Collections.Generic;
using HarmonyLib;
using NuclearOption.Networking;
using UnityEngine;

namespace FactionGate
{
    public static class SkinProfileEnforcer
    {
        [ThreadStatic]
        private static bool _probing;

        public static bool IsProbing
        {
            get => _probing;
            set => _probing = value;
        }

        /// <summary>Active FactionGate preset id for a game side (Boscali/Primeva).</summary>
        public static string PresetIdForSide(string factionName)
        {
            if (string.IsNullOrEmpty(factionName))
                return FactionPresets.DefaultId;
            if (factionName.IndexOf("Boscali", StringComparison.OrdinalIgnoreCase) >= 0)
                return FactionAppearance.ActiveBoscaliId;
            if (factionName.IndexOf("Primeva", StringComparison.OrdinalIgnoreCase) >= 0)
                return FactionAppearance.ActivePrimevaId;
            return FactionPresets.DefaultId;
        }

        public static string PresetIdForPlayer(Player player)
        {
            try
            {
                var hq = player?.HQ;
                var name = hq?.faction?.factionName;
                return PresetIdForSide(name);
            }
            catch
            {
                return FactionPresets.DefaultId;
            }
        }

        public static string PresetIdForHq(FactionHQ hq)
        {
            try
            {
                return PresetIdForSide(hq?.faction?.factionName);
            }
            catch
            {
                return FactionPresets.DefaultId;
            }
        }

        /// <summary>Player aircraft use owner HQ; AI use the aircraft's Network/Map HQ.</summary>
        public static string PresetIdForAircraft(Aircraft aircraft)
        {
            if (aircraft == null)
                return FactionPresets.DefaultId;

            try
            {
                var player = aircraft.Player;
                if (player != null)
                    return PresetIdForPlayer(player);
            }
            catch
            {
                /* fall through to HQ */
            }

            FactionHQ hq = null;
            try { hq = aircraft.NetworkHQ; } catch { /* ignore */ }
            if (hq == null)
            {
                try { hq = aircraft.MapHQ; } catch { /* ignore */ }
            }

            if (hq == null)
            {
                try { hq = aircraft.Editor_HQ; } catch { /* ignore */ }
            }

            return PresetIdForHq(hq);
        }

        public static string AirframeKey(AircraftDefinition def)
        {
            if (def == null)
                return "";
            if (!string.IsNullOrWhiteSpace(def.jsonKey))
                return def.jsonKey.Trim();
            return def.name ?? "";
        }

        public static bool IsAllowed(string factionPresetId, AircraftDefinition def, LiveryKey key)
        {
            var airKey = AirframeKey(def);
            if (string.IsNullOrEmpty(factionPresetId) ||
                string.Equals(factionPresetId, FactionPresets.DefaultId, StringComparison.OrdinalIgnoreCase))
            {
                // No remapped faction profile selected for this side — do not filter.
                return true;
            }

            // Per-faction enable from Skin Profile Settings.
            if (!SkinProfileStore.IsFactionEnabled(factionPresetId))
                return true;

            if (!SkinProfileStore.TryGet(factionPresetId, airKey, out var profile))
            {
                // Profile enabled but no rules for this airframe yet → deny customs, allow vanilla default.
                return key.Type == LiveryKey.KeyType.Builtin;
            }

            if (key.Type == LiveryKey.KeyType.Builtin)
                return profile.allowVanilla;

            if (key.Type == LiveryKey.KeyType.Workshop)
                return SkinProfileStore.IsWorkshopAllowed(factionPresetId, airKey, key.Id);

            // AppData skins not managed in checklist — deny when profile is on.
            return false;
        }

        public static bool Sanitize(Player player, Aircraft aircraft, ref LiveryKey key)
        {
            if (aircraft == null)
                return true;
            var preset = player != null ? PresetIdForPlayer(player) : PresetIdForAircraft(aircraft);
            return SanitizeForPreset(preset, aircraft, ref key, isAi: player == null);
        }

        public static bool SanitizeAircraft(Aircraft aircraft, ref LiveryKey key)
        {
            if (aircraft == null)
                return true;
            return SanitizeForPreset(PresetIdForAircraft(aircraft), aircraft, ref key, isAi: aircraft.Player == null);
        }

        public static bool SanitizeForHq(FactionHQ hq, AircraftDefinition def, ref LiveryKey key)
        {
            if (def == null)
                return true;
            var preset = PresetIdForHq(hq);
            return SanitizeForPreset(preset, def, ref key, isAi: true, airframeLabel: AirframeKey(def));
        }

        private static bool SanitizeForPreset(string preset, Aircraft aircraft, ref LiveryKey key, bool isAi)
        {
            if (aircraft?.definition == null)
                return true;
            return SanitizeForPreset(preset, aircraft.definition, ref key, isAi, AirframeKey(aircraft.definition));
        }

        private static bool SanitizeForPreset(
            string preset,
            AircraftDefinition def,
            ref LiveryKey key,
            bool isAi,
            string airframeLabel)
        {
            if (def == null)
                return true;
            if (!SkinProfileStore.IsFactionEnabled(preset))
                return true;
            if (IsAllowed(preset, def, key))
                return true;

            try
            {
                IsProbing = true;
                var options = new List<ValueTuple<LiveryKey, string>>();
                LoadoutSelector.GetLiveryOptions(options, def, null, true);
                foreach (var opt in options)
                {
                    if (IsAllowed(preset, def, opt.Item1))
                    {
                        key = opt.Item1;
                        FactionGatePlugin.Log?.LogInfo(
                            $"FactionGate replaced disallowed {(isAi ? "AI" : "player")} livery with {key} for {preset}/{airframeLabel}");
                        return true;
                    }
                }
            }
            finally
            {
                IsProbing = false;
            }

            key = new LiveryKey(0);
            FactionGatePlugin.Log?.LogWarning(
                $"FactionGate blocked {(isAi ? "AI" : "player")} livery; falling back to builtin 0 for {preset}/{airframeLabel}");
            return true;
        }
    }

    [HarmonyPatch(typeof(LoadoutSelector), nameof(LoadoutSelector.GetLiveryOptions))]
    public static class Patch_GetLiveryOptions_SkinProfile
    {
        [HarmonyPostfix]
        public static void Postfix(List<ValueTuple<LiveryKey, string>> resultsList, AircraftDefinition aircraft)
        {
            try
            {
                if (SkinProfileEnforcer.IsProbing)
                    return;
                if (resultsList == null || resultsList.Count == 0)
                    return;

                Player local = null;
                try
                {
                    var players = UnityEngine.Object.FindObjectsOfType<Player>();
                    foreach (var p in players)
                    {
                        if (p != null && p.IsLocalPlayer)
                        {
                            local = p;
                            break;
                        }
                    }
                }
                catch
                {
                    /* ignore */
                }

                var preset = local != null
                    ? SkinProfileEnforcer.PresetIdForPlayer(local)
                    : FactionAppearance.ActiveBoscaliId;

                // If local has no HQ yet (pre-join hangar rare), don't filter.
                if (local?.HQ == null)
                    return;

                if (string.Equals(preset, FactionPresets.DefaultId, StringComparison.OrdinalIgnoreCase))
                    return;

                for (var i = resultsList.Count - 1; i >= 0; i--)
                {
                    if (!SkinProfileEnforcer.IsAllowed(preset, aircraft, resultsList[i].Item1))
                        resultsList.RemoveAt(i);
                }

                if (resultsList.Count == 0)
                    resultsList.Add(new ValueTuple<LiveryKey, string>(new LiveryKey(0), "Default"));
            }
            catch (Exception ex)
            {
                FactionGatePlugin.Log?.LogWarning($"FactionGate GetLiveryOptions filter: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(Aircraft), nameof(Aircraft.SetLiveryKey))]
    public static class Patch_SetLiveryKey_SkinProfile
    {
        [HarmonyPrefix]
        public static void Prefix(Aircraft __instance, ref LiveryKey liveryKey)
        {
            try
            {
                if (__instance == null)
                    return;
                // Players and AI — resolve faction from owner HQ or aircraft HQ.
                SkinProfileEnforcer.SanitizeAircraft(__instance, ref liveryKey);
            }
            catch (Exception ex)
            {
                FactionGatePlugin.Log?.LogWarning($"FactionGate SetLiveryKey: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(Aircraft), "set_NetworkLiveryKey")]
    public static class Patch_NetworkLiveryKey_SkinProfile
    {
        [HarmonyPrefix]
        public static void Prefix(Aircraft __instance, ref LiveryKey value)
        {
            try
            {
                if (__instance == null)
                    return;

                var player = __instance.Player;
                if (player != null)
                {
                    SkinProfileEnforcer.Sanitize(player, __instance, ref value);
                    return;
                }

                // AI: host/server is authoritative; still sanitize on apply so late HQ is respected.
                if (!NetworkAuthority.IsServerActive && !FactionSession.IsHostSource)
                    return;

                SkinProfileEnforcer.SanitizeAircraft(__instance, ref value);
            }
            catch (Exception ex)
            {
                FactionGatePlugin.Log?.LogWarning($"FactionGate NetworkLiveryKey: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Mission/AI spawn passes LiveryKey + FactionHQ before the Aircraft exists — sanitize here too.
    /// </summary>
    [HarmonyPatch(typeof(Spawner), nameof(Spawner.SpawnAircraft))]
    public static class Patch_Spawner_SpawnAircraft_SkinProfile
    {
        [HarmonyPrefix]
        public static void Prefix(
            Player player,
            GameObject prefab,
            ref LiveryKey livery,
            FactionHQ HQ)
        {
            try
            {
                if (prefab == null)
                    return;

                AircraftDefinition def = null;
                try
                {
                    var ac = prefab.GetComponent<Aircraft>();
                    def = ac != null ? ac.definition : null;
                }
                catch
                {
                    /* ignore */
                }

                if (def == null)
                    return;

                var spawnHq = player != null && player.HQ != null ? player.HQ : HQ;
                SkinProfileEnforcer.SanitizeForHq(spawnHq, def, ref livery);
            }
            catch (Exception ex)
            {
                FactionGatePlugin.Log?.LogWarning($"FactionGate SpawnAircraft livery: {ex.Message}");
            }
        }
    }
}
