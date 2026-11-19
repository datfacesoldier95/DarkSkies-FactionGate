using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using NuclearOption.UI;
using TMPro;
using UnityEngine;

namespace FactionGate
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class FactionGatePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.darkskies.factiongate";
        public const string PluginName = "DarkSkies FactionGate";
        public const string PluginVersion = "1.2.4";

        public static FactionGatePlugin Instance { get; private set; }
        public static ManualLogSource Log { get; private set; }

        public static ConfigEntry<string> BoscaliPreset;
        public static ConfigEntry<string> PrimevaPreset;
        public static ConfigEntry<KeyCode> ReloadPresetsKey;

        private Harmony _harmony;
        private string _lastBoscali;
        private string _lastPrimeva;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            var pluginDir = Path.GetDirectoryName(Info.Location) ?? "";
            FactionPresets.SetRoot(pluginDir);
            FactionPresets.Reload();
            SkinProfileStore.SetRoot(pluginDir);
            if (!FactionSession.IsClientNonHost)
                SkinProfileStore.ReloadFromDisk();

            var acceptable = FactionPresets.AcceptableValues;
            BoscaliPreset = Config.Bind(
                "Factions",
                "BoscaliPreset",
                FactionPresets.DefaultId,
                new ConfigDescription(
                    "Replacement shown for Boscali on the SELECT FACTION join screen (host authoritative).",
                    new AcceptableValueList<string>(acceptable),
                    new ConfigurationManagerAttributes { Order = 200 }));

            PrimevaPreset = Config.Bind(
                "Factions",
                "PrimevaPreset",
                FactionPresets.DefaultId,
                new ConfigDescription(
                    "Replacement shown for Primeva on the SELECT FACTION join screen (host authoritative).",
                    new AcceptableValueList<string>(acceptable),
                    new ConfigurationManagerAttributes { Order = 190 }));

            ReloadPresetsKey = Config.Bind(
                "General",
                "ReloadPresetsKey",
                KeyCode.F10,
                new ConfigDescription(
                    "Reload presets.json + Flags folder, then re-apply / broadcast if host.",
                    null,
                    new ConfigurationManagerAttributes { Order = 10 }));

            SkinProfileUi.Bind(Config);

            _lastBoscali = BoscaliPreset.Value;
            _lastPrimeva = PrimevaPreset.Value;

            BoscaliPreset.SettingChanged += OnHostSettingChanged;
            PrimevaPreset.SettingChanged += OnHostSettingChanged;

            if (!FactionSession.IsClientNonHost)
                FactionAppearance.ApplyFromConfig();

            gameObject.AddComponent<FactionSteamSync>();

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();

            Log.LogInfo($"{PluginName} {PluginVersion} loaded. Presets: {FactionPresets.Ids.Count}");
        }

        private void Update()
        {
            if (Input.GetKeyDown(ReloadPresetsKey.Value))
            {
                FactionPresets.Reload();
                if (!FactionSession.IsClientNonHost)
                {
                    FactionAppearance.ApplyFromConfig();
                    FactionSteamSync.HostBroadcastPresets();
                }

                Log.LogInfo($"FactionGate presets reloaded via {ReloadPresetsKey.Value}.");
            }

            // Catch F1 ConfigurationManager edits without relying solely on SettingChanged.
            if (!FactionSession.IsClientNonHost)
            {
                var b = BoscaliPreset.Value;
                var p = PrimevaPreset.Value;
                if (!string.Equals(b, _lastBoscali, StringComparison.Ordinal) ||
                    !string.Equals(p, _lastPrimeva, StringComparison.Ordinal))
                {
                    _lastBoscali = b;
                    _lastPrimeva = p;
                    FactionAppearance.ApplyFromConfig();
                    FactionSteamSync.HostBroadcastPresets();
                }
            }
        }

        private void OnHostSettingChanged(object sender, EventArgs e)
        {
            if (FactionSession.IsClientNonHost)
                return;
            _lastBoscali = BoscaliPreset.Value;
            _lastPrimeva = PrimevaPreset.Value;
            FactionAppearance.ApplyFromConfig();
            FactionSteamSync.HostBroadcastPresets();
        }

        private void OnDestroy()
        {
            try
            {
                FactionAppearance.Restore();
            }
            catch
            {
                /* ignore */
            }

            _harmony?.UnpatchSelf();
            if (Instance == this)
                Instance = null;
        }

        [HarmonyPatch(typeof(LeaderboardFactionItem), "SetupHq")]
        private static class Patch_SetupHq
        {
            private static void Postfix(LeaderboardFactionItem __instance, FactionHQ hq)
            {
                try
                {
                    if (__instance == null || hq?.faction == null)
                        return;

                    // HQ.faction may be a different instance than FactionRegistry — mutate both.
                    FactionAppearance.ReapplyActive();
                    FactionAppearance.ApplyToFactionInstance(hq.faction);

                    // Big crest is JoinButton.targetGraphic; FactionFlag alone is not enough.
                    FactionAppearance.PaintJoinFactionItem(__instance, hq.faction);

                    var joinTitle = AccessTools.Field(typeof(LeaderboardFactionItem), "JoinTitleText");
                    var scoreTitle = AccessTools.Field(typeof(LeaderboardFactionItem), "ScoreTitleText");
                    if (joinTitle != null)
                        SetText(joinTitle.GetValue(__instance), hq.faction.factionExtendedName);
                    if (scoreTitle != null)
                        SetText(scoreTitle.GetValue(__instance), hq.faction.factionExtendedName);

                    Log?.LogInfo(
                        $"FactionGate painted join UI for {hq.faction.factionName} → {hq.faction.factionExtendedName} (incl. JoinButton)");
                }
                catch (Exception ex)
                {
                    Log?.LogWarning($"FactionGate SetupHq postfix: {ex}");
                }
            }

            private static void SetText(object textComponent, string value)
            {
                if (textComponent == null || value == null)
                    return;
                var type = textComponent.GetType();
                var prop = AccessTools.Property(type, "text");
                if (prop != null && prop.CanWrite)
                    prop.SetValue(textComponent, value, null);
            }
        }

        /// <summary>
        /// Map MFD side buttons (BDF/PALA) read factionTag via screen.shortName in Initialize.
        /// </summary>
        [HarmonyPatch(typeof(InfoPanel_Faction), "Initialize")]
        private static class Patch_InfoPanel_Initialize
        {
            private static void Postfix(InfoPanel_Faction __instance)
            {
                try
                {
                    if (__instance == null)
                        return;

                    FactionAppearance.ReapplyActive();

                    var hq = __instance.factionHQ;
                    if (hq?.faction == null)
                        return;

                    FactionAppearance.ApplyToFactionInstance(hq.faction);

                    var faction = hq.faction;
                    var screenField = AccessTools.Field(typeof(InfoPanel_Faction), "screen");
                    var nameField = AccessTools.Field(typeof(InfoPanel_Faction), "factionName");
                    var imageField = AccessTools.Field(typeof(InfoPanel_Faction), "factionImage");

                    var screen = screenField?.GetValue(__instance) as MFDScreen;
                    if (screen != null)
                    {
                        screen.shortName = faction.factionTag;
                        if (screen.label != null)
                            screen.label.text = faction.factionTag;
                        if (screen.virtualMFD != null)
                            screen.virtualMFD.SetupButtons();
                    }

                    var nameTmp = nameField?.GetValue(__instance) as TMPro.TextMeshProUGUI;
                    if (nameTmp != null && !string.IsNullOrEmpty(faction.factionExtendedName))
                        nameTmp.text = faction.factionExtendedName.ToUpperInvariant();

                    var img = imageField?.GetValue(__instance) as UnityEngine.UI.Image;
                    if (img != null)
                        FactionAppearance.PaintJoinImage(img, faction);

                    Log?.LogInfo(
                        $"FactionGate map panel {faction.factionName} tag={faction.factionTag}");
                }
                catch (Exception ex)
                {
                    Log?.LogWarning($"FactionGate InfoPanel Initialize postfix: {ex}");
                }
            }
        }
    }
}
