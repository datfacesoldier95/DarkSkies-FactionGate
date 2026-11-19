using System;
using System.Collections.Generic;

namespace FactionGate
{
    public sealed class WorkshopSkinEntry
    {
        public string DisplayName;
        public string WorkshopId;
        public ulong WorkshopIdValue;
    }

    public static class WorkshopSkinCatalog
    {
        public static List<WorkshopSkinEntry> ListForAirframe(string airframeKey)
        {
            var result = new List<WorkshopSkinEntry>();
            var def = AirframeCatalog.FindDefinition(airframeKey);
            if (def == null)
                return result;

            try
            {
                // Bypass our own hangar filter so the editor always lists the full workshop set.
                SkinProfileEnforcer.IsProbing = true;
                var options = new List<ValueTuple<LiveryKey, string>>();
                LoadoutSelector.GetLiveryOptions(options, def, null, true);
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var opt in options)
                {
                    var key = opt.Item1;
                    if (key.Type != LiveryKey.KeyType.Workshop)
                        continue;
                    var id = key.Id.ToString();
                    if (!seen.Add(id))
                        continue;

                    var label = opt.Item2 ?? id;
                    // Hangar appends " (workshop)" — strip for cleaner checklist.
                    const string suffix = " (workshop)";
                    if (label.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                        label = label.Substring(0, label.Length - suffix.Length);

                    result.Add(new WorkshopSkinEntry
                    {
                        DisplayName = label,
                        WorkshopId = id,
                        WorkshopIdValue = key.Id,
                    });
                }

                result.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                FactionGatePlugin.Log?.LogDebug($"WorkshopSkinCatalog: {ex.Message}");
            }
            finally
            {
                SkinProfileEnforcer.IsProbing = false;
            }

            return result;
        }
    }
}
