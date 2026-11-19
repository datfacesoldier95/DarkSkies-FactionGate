using System;
using System.Collections.Generic;
using System.Linq;

namespace FactionGate
{
    public sealed class AirframeEntry
    {
        public string Key;
        public string DisplayName;
        public AircraftDefinition Definition;
    }

    public static class AirframeCatalog
    {
        private static readonly List<AirframeEntry> _entries = new List<AirframeEntry>();
        private static string[] _labels = { "—" };

        public static IReadOnlyList<AirframeEntry> Entries => _entries;
        public static string[] Labels => _labels;

        public static void Refresh()
        {
            _entries.Clear();
            try
            {
                var enc = Encyclopedia.i;
                if (enc?.aircraft == null)
                {
                    _labels = new[] { "—" };
                    return;
                }

                foreach (var def in enc.aircraft)
                {
                    if (def == null)
                        continue;
                    var key = !string.IsNullOrWhiteSpace(def.jsonKey) ? def.jsonKey.Trim() : def.name;
                    if (string.IsNullOrWhiteSpace(key))
                        continue;
                    var display = !string.IsNullOrWhiteSpace(def.unitName) ? def.unitName.Trim() : key;
                    _entries.Add(new AirframeEntry
                    {
                        Key = key,
                        DisplayName = display,
                        Definition = def,
                    });
                }

                _entries.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
                _labels = _entries.Select(FormatLabel).ToArray();
                if (_labels.Length == 0)
                    _labels = new[] { "—" };
            }
            catch (Exception ex)
            {
                FactionGatePlugin.Log?.LogDebug($"AirframeCatalog refresh: {ex.Message}");
                _labels = new[] { "—" };
            }
        }

        public static string FormatLabel(AirframeEntry e)
        {
            if (e == null)
                return "—";
            return e.DisplayName + "  [" + e.Key + "]";
        }

        public static string KeyFromLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label) || label == "—")
                return "";
            var open = label.LastIndexOf('[');
            var close = label.LastIndexOf(']');
            if (open >= 0 && close > open)
                return label.Substring(open + 1, close - open - 1).Trim();
            return label.Trim();
        }

        public static string LabelFromKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return _labels.Length > 0 ? _labels[0] : "—";
            foreach (var e in _entries)
            {
                if (string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase))
                    return FormatLabel(e);
            }

            return key;
        }

        public static AircraftDefinition FindDefinition(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;
            foreach (var e in _entries)
            {
                if (string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase))
                    return e.Definition;
            }

            try
            {
                var enc = Encyclopedia.i;
                if (enc?.aircraft == null)
                    return null;
                foreach (var def in enc.aircraft)
                {
                    if (def == null)
                        continue;
                    if (string.Equals(def.jsonKey, key, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(def.name, key, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(def.unitName, key, StringComparison.OrdinalIgnoreCase))
                        return def;
                }
            }
            catch
            {
                /* ignore */
            }

            return null;
        }
    }
}
