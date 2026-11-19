using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace FactionGate
{
    public static class SkinProfileUi
    {
        public static ConfigEntry<string> EditFaction;
        public static ConfigEntry<string> EditAirframe;
        public static ConfigEntry<string> EditorPanel;

        private static SkinFactionProfile _draft;
        private static string _draftFactionId = "";
        private static Vector2 _airframeScroll;
        private static Vector2 _unassignedScroll;
        private static Vector2 _assignedScroll;
        private static Vector2 _summaryScroll;
        private static List<WorkshopSkinEntry> _catalog = new List<WorkshopSkinEntry>();
        private static string _catalogAirframe = "";
        private static string _status = "";
        private const float SkinBoxHeight = 160f;

        public static void Bind(ConfigFile config)
        {
            var factionLabels = BuildFactionLabels();
            AirframeCatalog.Refresh();

            EditFaction = config.Bind(
                "Skin Profile Settings",
                "SelectFaction",
                factionLabels.Length > 0 ? factionLabels[0] : FactionPresets.DefaultId,
                new ConfigDescription(
                    "Faction whose skin profile you are editing. Changing this loads that faction's saved config into the editor.",
                    new AcceptableValueList<string>(factionLabels),
                    new ConfigurationManagerAttributes { Order = 100, DispName = "Faction Selection" }));

            // Persisted selection only — drawn inside the editor panel so CM does not crush the layout.
            EditAirframe = config.Bind(
                "Skin Profile Settings",
                "SelectAirframe",
                AirframeCatalog.Labels.Length > 0 ? AirframeCatalog.Labels[0] : "—",
                new ConfigDescription(
                    "Last selected airframe for the skin editor.",
                    null,
                    new ConfigurationManagerAttributes
                    {
                        Order = 90,
                        Browsable = false,
                    }));

            EditorPanel = config.Bind(
                "Skin Profile Settings",
                "FactionSkinEditor",
                "",
                new ConfigDescription(
                    "Faction skin profile editor.",
                    null,
                    new ConfigurationManagerAttributes
                    {
                        Order = 80,
                        DispName = "Profile Editor",
                        CustomDrawer = DrawEditorPanel,
                        HideDefaultButton = true,
                        HideSettingName = true,
                    }));

            EditFaction.SettingChanged += (_, __) => LoadDraftForCurrentFaction();

            LoadDraftForCurrentFaction();
        }

        public static string CurrentFactionId()
        {
            var v = EditFaction?.Value ?? "";
            var open = v.LastIndexOf('(');
            var close = v.LastIndexOf(')');
            if (open >= 0 && close > open)
                return v.Substring(open + 1, close - open - 1).Trim();
            return FactionPresets.NormalizeId(v);
        }

        public static string CurrentAirframeKey()
        {
            return AirframeCatalog.KeyFromLabel(EditAirframe?.Value ?? "");
        }

        private static string[] BuildFactionLabels()
        {
            var list = new List<string>();
            foreach (var id in FactionPresets.Ids)
            {
                if (!FactionPresets.TryGet(id, out var p))
                    continue;
                list.Add(p.DisplayName + " (" + p.Id + ")");
            }

            if (list.Count == 0)
                list.Add(FactionPresets.DefaultId);
            return list.ToArray();
        }

        private static void LoadDraftForCurrentFaction()
        {
            var factionId = CurrentFactionId();
            _draftFactionId = factionId;
            _draft = SkinProfileStore.CloneFactionProfile(factionId);
            _status = string.IsNullOrEmpty(factionId)
                ? ""
                : "Loaded profile for " + factionId + (_draft.enabled ? " (enabled)" : " (disabled)");
            RefreshCatalog();
        }

        private static void EnsureDraft()
        {
            var factionId = CurrentFactionId();
            if (_draft == null ||
                !string.Equals(_draftFactionId, factionId, StringComparison.OrdinalIgnoreCase))
            {
                LoadDraftForCurrentFaction();
            }
        }

        private static void RefreshCatalog()
        {
            var airframe = CurrentAirframeKey();
            _catalogAirframe = airframe;
            _catalog = string.IsNullOrEmpty(airframe)
                ? new List<WorkshopSkinEntry>()
                : WorkshopSkinCatalog.ListForAirframe(airframe);
        }

        /// <summary>
        /// ConfigurationManager invokes CustomDrawer inside an open BeginHorizontal (name | value).
        /// End that row first or every control packs onto one crushed line.
        /// </summary>
        private static void BeginFullWidthDrawer()
        {
            GUILayout.EndHorizontal();
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        }

        private static void EndFullWidthDrawer()
        {
            GUILayout.EndVertical();
            GUILayout.BeginHorizontal();
        }

        private static void DrawEditorPanel(ConfigEntryBase entry)
        {
            BeginFullWidthDrawer();
            try
            {
                if (FactionSession.IsClientNonHost)
                {
                    GUILayout.Label("Joiners receive the host skin profile automatically. Host edits only.");
                    return;
                }

                EnsureDraft();
                AirframeCatalog.Refresh();

                var airframe = CurrentAirframeKey();
                if (!string.Equals(airframe, _catalogAirframe, StringComparison.OrdinalIgnoreCase))
                    RefreshCatalog();

                // --- Enable ---
                _draft.enabled = GUILayout.Toggle(_draft.enabled, "Enable Faction Profile?");
                GUILayout.Space(8);

                // --- Airframe ---
                GUILayout.Label("Select Airframe");
                GUILayout.Label("Selected: " + (EditAirframe.Value ?? "—"));
                DrawAirframeList();
                GUILayout.Space(8);

                airframe = CurrentAirframeKey();
                SkinAirframeProfile air = null;
                if (string.IsNullOrEmpty(airframe) || airframe == "—")
                {
                    GUILayout.Label("Select an airframe to edit Unassigned / Assigned skins.");
                }
                else
                {
                    air = SkinProfileStore.GetAirframeFromFaction(_draft, airframe);

                    // Box 1 — Unassigned for selected airframe
                    GUILayout.Label("Unassigned Skins  (click to assign to this airframe)");
                    _unassignedScroll = GUILayout.BeginScrollView(
                        _unassignedScroll,
                        GUI.skin.box,
                        GUILayout.Height(SkinBoxHeight),
                        GUILayout.ExpandWidth(true));
                    DrawSkinButtons(GetAvailable(air), skin =>
                    {
                        if (!air.allowedWorkshopIds.Exists(x =>
                                string.Equals(x, skin.WorkshopId, StringComparison.Ordinal)))
                            air.allowedWorkshopIds.Add(skin.WorkshopId);
                        _status = "Added: " + skin.DisplayName + " (not saved yet)";
                    }, emptyCatalog: _catalog.Count == 0);
                    GUILayout.EndScrollView();

                    if (GUILayout.Button("Refresh workshop list", GUILayout.Width(180)))
                        RefreshCatalog();

                    GUILayout.Space(8);

                    // Box 2 — Assigned for selected airframe
                    GUILayout.Label("Assigned Skins  (click to unassign from this airframe)");
                    _assignedScroll = GUILayout.BeginScrollView(
                        _assignedScroll,
                        GUI.skin.box,
                        GUILayout.Height(SkinBoxHeight),
                        GUILayout.ExpandWidth(true));
                    DrawSkinButtons(GetAssigned(air), skin =>
                    {
                        air.allowedWorkshopIds.RemoveAll(x =>
                            string.Equals(x, skin.WorkshopId, StringComparison.Ordinal));
                        _status = "Removed: " + skin.DisplayName + " (not saved yet)";
                    }, emptyCatalog: false);
                    GUILayout.EndScrollView();

                    GUILayout.Space(8);
                    air.allowVanilla = GUILayout.Toggle(air.allowVanilla, "Vanilla Skins Enabled?");
                }

                GUILayout.Space(8);

                // Box 3 — Faction-wide summary (all airframes on this faction draft)
                GUILayout.Label("Faction Skin Summary");
                _summaryScroll = GUILayout.BeginScrollView(
                    _summaryScroll,
                    GUI.skin.box,
                    GUILayout.Height(SkinBoxHeight),
                    GUILayout.ExpandWidth(true));
                DrawFactionSkinSummary();
                GUILayout.EndScrollView();

                GUILayout.Space(10);
                if (GUILayout.Button("Save Profile", GUILayout.Height(28), GUILayout.ExpandWidth(true)))
                    SaveCurrentProfile();
                if (GUILayout.Button("Clear Profile", GUILayout.Height(28), GUILayout.ExpandWidth(true)))
                    ClearCurrentProfile();

                if (!string.IsNullOrEmpty(_status))
                {
                    GUILayout.Space(4);
                    GUILayout.Label(_status);
                }
            }
            finally
            {
                EndFullWidthDrawer();
            }
        }

        private static void DrawAirframeList()
        {
            var labels = AirframeCatalog.Labels;
            if (labels == null || labels.Length == 0 || (labels.Length == 1 && labels[0] == "—"))
            {
                GUILayout.Label("No airframes found yet. Open F1 again after the main menu/encyclopedia has loaded.");
                if (GUILayout.Button("Rescan airframes", GUILayout.Width(160)))
                    AirframeCatalog.Refresh();
                return;
            }

            _airframeScroll = GUILayout.BeginScrollView(
                _airframeScroll,
                GUI.skin.box,
                GUILayout.Height(130),
                GUILayout.ExpandWidth(true));
            foreach (var label in labels)
            {
                var selected = string.Equals(label, EditAirframe.Value, StringComparison.Ordinal);
                if (GUILayout.Toggle(selected, label) && !selected)
                {
                    EditAirframe.Value = label;
                    RefreshCatalog();
                }
            }

            GUILayout.EndScrollView();
        }

        private static List<WorkshopSkinEntry> GetAvailable(SkinAirframeProfile air)
        {
            if (air.allowedWorkshopIds == null)
                air.allowedWorkshopIds = new List<string>();
            var assignedIds = new HashSet<string>(air.allowedWorkshopIds, StringComparer.Ordinal);
            var available = new List<WorkshopSkinEntry>();
            foreach (var skin in _catalog)
            {
                if (!assignedIds.Contains(skin.WorkshopId))
                    available.Add(skin);
            }

            return available;
        }

        private static List<WorkshopSkinEntry> GetAssigned(SkinAirframeProfile air)
        {
            if (air.allowedWorkshopIds == null)
                air.allowedWorkshopIds = new List<string>();

            var assigned = new List<WorkshopSkinEntry>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var skin in _catalog)
            {
                if (!air.allowedWorkshopIds.Exists(x =>
                        string.Equals(x, skin.WorkshopId, StringComparison.Ordinal)))
                    continue;
                if (!seen.Add(skin.WorkshopId))
                    continue;
                assigned.Add(skin);
            }

            foreach (var id in air.allowedWorkshopIds)
            {
                if (!seen.Add(id))
                    continue;
                assigned.Add(new WorkshopSkinEntry
                {
                    WorkshopId = id,
                    DisplayName = "Workshop " + id + " (not subscribed here)",
                });
            }

            return assigned;
        }

        private static void DrawSkinButtons(
            List<WorkshopSkinEntry> skins,
            Action<WorkshopSkinEntry> onClick,
            bool emptyCatalog)
        {
            if (emptyCatalog && skins.Count == 0)
            {
                GUILayout.Label("No workshop skins found for this airframe on this PC.");
                return;
            }

            if (skins.Count == 0)
            {
                GUILayout.Label("(none)");
                return;
            }

            foreach (var skin in skins)
            {
                if (GUILayout.Button(skin.DisplayName, GUILayout.ExpandWidth(true)))
                    onClick(skin);
            }
        }

        private static void DrawFactionSkinSummary()
        {
            var lines = BuildFactionSummaryLines();
            if (lines.Count == 0)
            {
                GUILayout.Label("(no custom skins assigned on this faction)");
                return;
            }

            foreach (var line in lines)
                GUILayout.Label(line);
        }

        private static List<string> BuildFactionSummaryLines()
        {
            var lines = new List<string>();
            if (_draft?.airframes == null)
                return lines;

            // Stable order by airframe display name.
            var keys = new List<string>(_draft.airframes.Keys);
            keys.Sort((a, b) =>
                string.Compare(AirframeDisplayName(a), AirframeDisplayName(b), StringComparison.OrdinalIgnoreCase));

            foreach (var airKey in keys)
            {
                if (!_draft.airframes.TryGetValue(airKey, out var air) || air?.allowedWorkshopIds == null)
                    continue;

                var airName = AirframeDisplayName(airKey);
                var catalog = WorkshopSkinCatalog.ListForAirframe(airKey);
                foreach (var id in air.allowedWorkshopIds)
                {
                    if (string.IsNullOrWhiteSpace(id))
                        continue;
                    var skinName = id;
                    foreach (var skin in catalog)
                    {
                        if (!string.Equals(skin.WorkshopId, id, StringComparison.Ordinal))
                            continue;
                        skinName = skin.DisplayName;
                        break;
                    }

                    lines.Add(airName + " - " + skinName);
                }
            }

            return lines;
        }

        private static string AirframeDisplayName(string airframeKey)
        {
            if (string.IsNullOrWhiteSpace(airframeKey))
                return "?";
            foreach (var e in AirframeCatalog.Entries)
            {
                if (string.Equals(e.Key, airframeKey, StringComparison.OrdinalIgnoreCase))
                    return string.IsNullOrWhiteSpace(e.DisplayName) ? e.Key : e.DisplayName;
            }

            return airframeKey;
        }

        private static void SaveCurrentProfile()
        {
            EnsureDraft();
            var factionId = CurrentFactionId();
            if (string.IsNullOrEmpty(factionId))
            {
                _status = "Select a faction first.";
                return;
            }

            SkinProfileStore.SaveFactionPreset(factionId, _draft);
            FactionSteamSync.HostBroadcastPresets();
            _status = "Saved profile for " + factionId +
                      (_draft.enabled ? " (profile enabled)" : " (profile disabled)") +
                      " and broadcast to joiners.";
            _draft = SkinProfileStore.CloneFactionProfile(factionId);
            _draftFactionId = factionId;
        }

        private static void ClearCurrentProfile()
        {
            EnsureDraft();
            var factionId = CurrentFactionId();
            if (string.IsNullOrEmpty(factionId))
            {
                _status = "Select a faction first.";
                return;
            }

            if (_draft.airframes == null)
                _draft.airframes = new Dictionary<string, SkinAirframeProfile>(StringComparer.OrdinalIgnoreCase);

            var clearedAirframes = 0;
            foreach (var kv in _draft.airframes)
            {
                if (kv.Value == null)
                    continue;
                if (kv.Value.allowedWorkshopIds == null)
                    kv.Value.allowedWorkshopIds = new List<string>();
                if (kv.Value.allowedWorkshopIds.Count == 0)
                    continue;
                kv.Value.allowedWorkshopIds.Clear();
                clearedAirframes++;
            }

            SkinProfileStore.SaveFactionPreset(factionId, _draft);
            FactionSteamSync.HostBroadcastPresets();
            _draft = SkinProfileStore.CloneFactionProfile(factionId);
            _draftFactionId = factionId;
            _status = clearedAirframes > 0
                ? "Cleared custom skins on " + clearedAirframes + " airframe(s) for " + factionId + "."
                : "No custom skins to clear for " + factionId + ".";
        }
    }
}
