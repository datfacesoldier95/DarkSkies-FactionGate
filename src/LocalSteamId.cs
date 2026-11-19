using System;
using Steamworks;
using UnityEngine;

namespace FactionGate
{
    public static class LocalSteamId
    {
        private static ulong _cached;
        private static float _cachedAtUnscaled = -999f;

        public static ulong Get()
        {
            if (_cached != 0 && Time.unscaledTime - _cachedAtUnscaled < 2f)
                return _cached;

            var id = ResolveFromNetworkPlayer();
            if (id == 0)
                id = ResolveFromSteamworks();

            if (id != 0)
            {
                _cached = id;
                _cachedAtUnscaled = Time.unscaledTime;
            }

            return id;
        }

        private static ulong ResolveFromNetworkPlayer()
        {
            try
            {
                var players = UnityEngine.Object.FindObjectsOfType<NuclearOption.Networking.Player>();
                foreach (var p in players)
                {
                    if (p == null || !p.IsLocalPlayer)
                        continue;
                    if (p.SteamID != 0)
                        return p.SteamID;
                    if (p.CSteamID.m_SteamID != 0)
                        return p.CSteamID.m_SteamID;
                }
            }
            catch (Exception ex)
            {
                FactionGatePlugin.Log?.LogDebug($"LocalSteamId Player scan failed: {ex.Message}");
            }

            return 0;
        }

        private static ulong ResolveFromSteamworks()
        {
            try
            {
                if (!SteamAPI.IsSteamRunning())
                    return 0;
                return SteamUser.GetSteamID().m_SteamID;
            }
            catch (Exception ex)
            {
                FactionGatePlugin.Log?.LogDebug($"LocalSteamId Steamworks failed: {ex.Message}");
                return 0;
            }
        }
    }
}
