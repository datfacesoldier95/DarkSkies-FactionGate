using System;
using UnityEngine;
using UnityEngine.UI;

namespace FactionGate
{
    /// <summary>
    /// Mutates Boscali/Primeva display fields only (factionExtendedName + logos).
    /// Does not change factionName used for mission lookup.
    /// </summary>
    public static class FactionAppearance
    {
        private const string BoscaliName = "Boscali";
        private const string PrimevaName = "Primeva";

        private static string _origBoscaliExtended;
        private static string _origPrimevaExtended;
        private static string _origBoscaliTag;
        private static string _origPrimevaTag;
        private static Sprite _origBoscaliLogo;
        private static Sprite _origPrimevaLogo;
        private static Sprite _origBoscaliHeader;
        private static Sprite _origPrimevaHeader;
        private static Sprite _origBoscaliGray;
        private static Sprite _origPrimevaGray;
        private static bool _cached;

        private static string _activeBoscali = FactionPresets.DefaultId;
        private static string _activePrimeva = FactionPresets.DefaultId;

        public static string ActiveBoscaliId => _activeBoscali;
        public static string ActivePrimevaId => _activePrimeva;

        public static void ApplyFromConfig()
        {
            if (FactionSession.IsClientNonHost)
                return;

            Apply(
                FactionGatePlugin.BoscaliPreset?.Value ?? FactionPresets.DefaultId,
                FactionGatePlugin.PrimevaPreset?.Value ?? FactionPresets.DefaultId);
        }

        public static void Apply(string boscaliId, string primevaId, bool quiet = false)
        {
            var b = FactionPresets.NormalizeId(boscaliId);
            var p = FactionPresets.NormalizeId(primevaId);
            var changed = !string.Equals(b, _activeBoscali, StringComparison.Ordinal) ||
                          !string.Equals(p, _activePrimeva, StringComparison.Ordinal) ||
                          !_cached;
            _activeBoscali = b;
            _activePrimeva = p;
            EnsureOriginalsCached();
            ApplyOne(BoscaliName, _activeBoscali, true);
            ApplyOne(PrimevaName, _activePrimeva, false);
            if (!quiet && changed)
            {
                FactionGatePlugin.Log?.LogInfo(
                    $"FactionGate appearance Boscali={_activeBoscali} Primeva={_activePrimeva}");
            }

            // Join screen often builds before Steam payload arrives — refresh crests after remap.
            if (changed)
                RefreshVisibleJoinUi();
        }

        public static void ReapplyActive()
        {
            Apply(_activeBoscali, _activePrimeva, quiet: true);
        }

        public static void Restore()
        {
            if (!_cached)
                return;
            ApplyOne(BoscaliName, FactionPresets.DefaultId, true);
            ApplyOne(PrimevaName, FactionPresets.DefaultId, false);
            _activeBoscali = FactionPresets.DefaultId;
            _activePrimeva = FactionPresets.DefaultId;
        }

        public static string BuildSyncJson()
        {
            var payload = new SyncDto
            {
                boscali = _activeBoscali,
                primeva = _activePrimeva,
                skins = SkinProfileStore.Data,
            };
            return Newtonsoft.Json.JsonConvert.SerializeObject(payload);
        }

        public static void ApplyFromHostJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return;
            try
            {
                var dto = Newtonsoft.Json.JsonConvert.DeserializeObject<SyncDto>(json);
                if (dto == null)
                    return;
                Apply(dto.boscali, dto.primeva);
                if (dto.skins != null)
                {
                    var skinsJson = Newtonsoft.Json.JsonConvert.SerializeObject(dto.skins);
                    SkinProfileStore.ApplyJson(skinsJson, fromHost: true);
                }

                // Apply() already refreshes when ids change; force again for late join UI.
                RefreshVisibleJoinUi();
            }
            catch (Exception ex)
            {
                FactionGatePlugin.Log?.LogWarning($"FactionGate host JSON apply failed: {ex.Message}");
            }
        }

        public static bool IsCustomPreset(Faction faction)
        {
            var id = PresetIdForFaction(faction);
            return !string.Equals(id, FactionPresets.DefaultId, StringComparison.OrdinalIgnoreCase) &&
                   FactionPresets.TryGet(id, out _);
        }

        /// <summary>
        /// Re-paint any open SELECT FACTION items (joiners often open the menu before host sync).
        /// </summary>
        public static void RefreshVisibleJoinUi()
        {
            try
            {
                var items = UnityEngine.Object.FindObjectsOfType<NuclearOption.UI.LeaderboardFactionItem>(true);
                if (items == null || items.Length == 0)
                    return;

                foreach (var item in items)
                {
                    if (item == null)
                        continue;
                    var hq = item.HQ;
                    if (hq?.faction == null)
                        continue;

                    ApplyToFactionInstance(hq.faction);
                    PaintJoinFactionItem(item, hq.faction);
                    SetJoinTitleTexts(item, hq.faction.factionExtendedName);
                }

                FactionGatePlugin.Log?.LogInfo(
                    $"FactionGate refreshed {items.Length} join faction item(s) after remap.");
            }
            catch (Exception ex)
            {
                FactionGatePlugin.Log?.LogDebug($"FactionGate join UI refresh skipped: {ex.Message}");
            }
        }

        private static void SetJoinTitleTexts(NuclearOption.UI.LeaderboardFactionItem item, string title)
        {
            if (item == null || title == null)
                return;

            SetTmpText(
                HarmonyLib.AccessTools.Field(typeof(NuclearOption.UI.LeaderboardFactionItem), "JoinTitleText")
                    ?.GetValue(item),
                title);
            SetTmpText(
                HarmonyLib.AccessTools.Field(typeof(NuclearOption.UI.LeaderboardFactionItem), "ScoreTitleText")
                    ?.GetValue(item),
                title);
        }

        private static void SetTmpText(object textComponent, string value)
        {
            if (textComponent == null || value == null)
                return;
            if (textComponent is TMPro.TMP_Text tmp)
            {
                tmp.text = value;
                return;
            }

            var prop = HarmonyLib.AccessTools.Property(textComponent.GetType(), "text");
            if (prop != null && prop.CanWrite)
                prop.SetValue(textComponent, value, null);
        }

        public static string PresetIdForFaction(Faction faction)
        {
            if (faction == null)
                return FactionPresets.DefaultId;
            var name = faction.factionName ?? "";
            if (name.IndexOf("Boscali", StringComparison.OrdinalIgnoreCase) >= 0)
                return _activeBoscali;
            if (name.IndexOf("Primeva", StringComparison.OrdinalIgnoreCase) >= 0)
                return _activePrimeva;
            return FactionPresets.DefaultId;
        }

        /// <summary>Apply remap to a specific Faction instance (HQ.faction may differ from registry).</summary>
        public static void ApplyToFactionInstance(Faction faction)
        {
            if (faction == null)
                return;

            CacheOriginalFrom(faction);

            var presetId = PresetIdForFaction(faction);
            if (string.Equals(presetId, FactionPresets.DefaultId, StringComparison.OrdinalIgnoreCase) ||
                !FactionPresets.TryGet(presetId, out var preset))
            {
                RestoreInstance(faction);
                return;
            }

            var sprite = FactionPresets.EnsureSprite(preset);
            faction.factionExtendedName = preset.DisplayName;
            if (!string.IsNullOrEmpty(preset.Tag))
                faction.factionTag = preset.Tag;
            if (sprite != null)
            {
                faction.factionColorLogo = sprite;
                faction.factionGrayscaleLogo = sprite;
                faction.factionHeaderSprite = sprite;
            }
        }

        public static Sprite ResolveSprite(Faction faction)
        {
            if (faction == null)
                return null;

            ApplyToFactionInstance(faction);

            var presetId = PresetIdForFaction(faction);
            if (!string.Equals(presetId, FactionPresets.DefaultId, StringComparison.OrdinalIgnoreCase) &&
                FactionPresets.TryGet(presetId, out var preset))
            {
                return FactionPresets.EnsureSprite(preset);
            }

            return faction.factionColorLogo;
        }

        public static void PaintJoinImage(Image img, Faction faction)
        {
            if (img == null || faction == null)
                return;

            // Never touch prefab art for Default — stretching the vanilla crest looks broken.
            if (!IsCustomPreset(faction))
                return;

            var sprite = ResolveSprite(faction);
            if (sprite == null)
                return;

            img.sprite = sprite;
            img.overrideSprite = sprite;
            img.color = Color.white;
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            img.enabled = true;
            img.material = null;
        }

        /// <summary>
        /// The big SELECT FACTION crest is JoinButton.targetGraphic, not only FactionFlag.
        /// </summary>
        public static void PaintJoinFactionItem(NuclearOption.UI.LeaderboardFactionItem item, Faction faction)
        {
            if (item == null || faction == null)
                return;

            // Leave vanilla BDF/PALA crests alone until a custom preset is active.
            if (!IsCustomPreset(faction))
                return;

            var sprite = ResolveSprite(faction);
            if (sprite == null)
                return;

            var flagField = HarmonyLib.AccessTools.Field(typeof(NuclearOption.UI.LeaderboardFactionItem), "FactionFlag");
            var buttonField = HarmonyLib.AccessTools.Field(typeof(NuclearOption.UI.LeaderboardFactionItem), "JoinButton");
            var headerField = HarmonyLib.AccessTools.Field(typeof(NuclearOption.UI.LeaderboardFactionItem), "JoinHeader");

            PaintJoinImage(flagField?.GetValue(item) as Image, faction);

            var button = buttonField?.GetValue(item) as Button;
            if (button != null)
            {
                PaintJoinImage(button.image, faction);
                PaintJoinImage(button.targetGraphic as Image, faction);

                // Sprite-swap transitions would flash the prefab BDF crest otherwise.
                var state = button.spriteState;
                state.highlightedSprite = sprite;
                state.pressedSprite = sprite;
                state.selectedSprite = sprite;
                state.disabledSprite = sprite;
                button.spriteState = state;
            }

            var header = headerField?.GetValue(item) as GameObject;
            if (header != null)
            {
                var images = header.GetComponentsInChildren<Image>(true);
                foreach (var img in images)
                    PaintJoinImage(img, faction);

                var raws = header.GetComponentsInChildren<RawImage>(true);
                foreach (var raw in raws)
                {
                    if (raw == null || sprite.texture == null)
                        continue;
                    raw.texture = sprite.texture;
                    raw.color = Color.white;
                }
            }

            // JoinButton may sit outside JoinHeader — paint its own children too.
            if (button != null)
            {
                var images = button.GetComponentsInChildren<Image>(true);
                foreach (var img in images)
                    PaintJoinImage(img, faction);
            }
        }

        private sealed class SyncDto
        {
#pragma warning disable CS0649
            public string boscali;
            public string primeva;
            public SkinProfilesFile skins;
#pragma warning restore CS0649
        }

        private static void EnsureOriginalsCached()
        {
            if (_cached)
                return;

            try
            {
                var boscali = FactionRegistry.FactionFromName(BoscaliName);
                var primeva = FactionRegistry.FactionFromName(PrimevaName);
                if (boscali != null)
                    CacheOriginalFrom(boscali);
                if (primeva != null)
                    CacheOriginalFrom(primeva);
            }
            catch (Exception ex)
            {
                FactionGatePlugin.Log?.LogDebug($"FactionGate cache originals deferred: {ex.Message}");
            }
        }

        private static void CacheOriginalFrom(Faction faction)
        {
            if (faction == null)
                return;
            var name = faction.factionName ?? "";
            if (name.IndexOf("Boscali", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (_origBoscaliExtended == null)
                    _origBoscaliExtended = faction.factionExtendedName;
                if (_origBoscaliTag == null)
                    _origBoscaliTag = faction.factionTag;
                if (_origBoscaliLogo == null)
                    _origBoscaliLogo = faction.factionColorLogo;
                if (_origBoscaliHeader == null)
                    _origBoscaliHeader = faction.factionHeaderSprite;
                if (_origBoscaliGray == null)
                    _origBoscaliGray = faction.factionGrayscaleLogo;
                _cached = true;
            }
            else if (name.IndexOf("Primeva", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (_origPrimevaExtended == null)
                    _origPrimevaExtended = faction.factionExtendedName;
                if (_origPrimevaTag == null)
                    _origPrimevaTag = faction.factionTag;
                if (_origPrimevaLogo == null)
                    _origPrimevaLogo = faction.factionColorLogo;
                if (_origPrimevaHeader == null)
                    _origPrimevaHeader = faction.factionHeaderSprite;
                if (_origPrimevaGray == null)
                    _origPrimevaGray = faction.factionGrayscaleLogo;
                _cached = true;
            }
        }

        private static void RestoreInstance(Faction faction)
        {
            var name = faction.factionName ?? "";
            if (name.IndexOf("Boscali", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (_origBoscaliExtended != null)
                    faction.factionExtendedName = _origBoscaliExtended;
                if (_origBoscaliTag != null)
                    faction.factionTag = _origBoscaliTag;
                if (_origBoscaliLogo != null)
                    faction.factionColorLogo = _origBoscaliLogo;
                if (_origBoscaliHeader != null)
                    faction.factionHeaderSprite = _origBoscaliHeader;
                if (_origBoscaliGray != null)
                    faction.factionGrayscaleLogo = _origBoscaliGray;
            }
            else if (name.IndexOf("Primeva", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (_origPrimevaExtended != null)
                    faction.factionExtendedName = _origPrimevaExtended;
                if (_origPrimevaTag != null)
                    faction.factionTag = _origPrimevaTag;
                if (_origPrimevaLogo != null)
                    faction.factionColorLogo = _origPrimevaLogo;
                if (_origPrimevaHeader != null)
                    faction.factionHeaderSprite = _origPrimevaHeader;
                if (_origPrimevaGray != null)
                    faction.factionGrayscaleLogo = _origPrimevaGray;
            }
        }

        private static void ApplyOne(string factionName, string presetId, bool isBoscali)
        {
            Faction faction;
            try
            {
                faction = FactionRegistry.FactionFromName(factionName);
            }
            catch
            {
                return;
            }

            if (faction == null)
                return;

            ApplyToFactionInstance(faction);
        }
    }
}

