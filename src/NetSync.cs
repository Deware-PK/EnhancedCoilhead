using System;
using System.Globalization;
using BepInEx.Configuration;
using HarmonyLib;
using LethalNetworkAPI;
using LethalNetworkAPI.Utils;
using Unity.Netcode;
using UnityEngine;

namespace EnhancedCoilhead
{
    // Multiplayer glue (via LethalNetworkAPI):
    //  1. host -> clients: config snapshot, so everyone runs the host's values
    //  2. client -> host: voice noises, so the host-side AI can hear clients talking
    //
    // Fully event driven (no per-frame polling), so it keeps working even if the BepInEx
    // manager object (and with it Plugin.Update) gets destroyed:
    //  - LNetworkUtils.OnNetworkStart  -> local client connected / host started
    //  - ConfigFile.SettingChanged     -> host edited a synced value
    //  - GameNetworkManager.SetInstanceValuesBackToDefault (postfix) -> left the lobby
    internal static class NetSync
    {
        public const int VoiceNoiseId = 75; // StartOfRound.DetectVoiceChatAmplitude

        private static LNetworkMessage<string> _config;   // host -> clients (config snapshot)
        private static LNetworkMessage<string> _request;  // client -> host (please send config; payload = client mod version)
        private static LNetworkMessage<string> _noise;    // client -> host (voice noise)

        public static void Init(ConfigFile config)
        {
            _config  = LNetworkMessage<string>.Create("EnhancedCoilhead.Config",
                onClientReceived: OnConfigFromServer);
            _request = LNetworkMessage<string>.Create("EnhancedCoilhead.ConfigRequest",
                onServerReceived: OnConfigRequest);
            _noise   = LNetworkMessage<string>.Create("EnhancedCoilhead.Noise",
                onServerReceived: (data, sender) => OnNoiseFromClient(data));

            config.SettingChanged += OnSettingChanged;
            LNetworkUtils.OnNetworkStart += OnNetworkStart;
        }

        // ---------------- connection lifecycle ----------------

        // Fired by LethalNetworkAPI when the local client connects to a server, or the host starts one.
        private static void OnNetworkStart(bool isServer)
        {
            Tuning.ClearOverrides(); // new session: never carry values over from a previous lobby

            if (isServer)
            {
                Plugin.Log.LogInfo("[sync] hosting: clients will receive this machine's config");
                return;
            }

            try
            {
                _request.SendServer(PluginInfo.VERSION);
                Plugin.Log.LogInfo("[sync] connected: requested host config");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[sync] could not request host config: {e.Message}");
            }
        }

        // Postfix on GameNetworkManager.SetInstanceValuesBackToDefault (every leave / kick / failed join path).
        internal static void OnLocalDisconnect()
        {
            if (Tuning.ClearOverrides())
                Plugin.Log.LogInfo("[sync] left lobby: cleared host config, back to local values");
        }

        // ---------------- config ----------------

        // Host side: a client just joined and asks for the config.
        private static void OnConfigRequest(string clientVersion, ulong sender)
        {
            if (clientVersion != PluginInfo.VERSION)
                Plugin.Log.LogWarning($"[sync] client {sender} runs {PluginInfo.NAME} {clientVersion}, host runs {PluginInfo.VERSION}");

            try
            {
                _config.SendClient(Tuning.Snapshot(), sender);
                Plugin.Log.LogInfo($"[sync] sent config to client {sender}");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[sync] could not send config to client {sender}: {e.Message}");
            }
        }

        // Host side: a synced value changed (config file / LethalConfig) while hosting -> push to everyone.
        private static void OnSettingChanged(object sender, SettingChangedEventArgs e)
        {
            if (!IsHostOnline()) return;
            if (e?.ChangedSetting == null || !Tuning.IsSynced(e.ChangedSetting)) return;

            try
            {
                _config.SendClients(Tuning.Snapshot());
                Plugin.Log.LogInfo($"[sync] {e.ChangedSetting.Definition.Section}.{e.ChangedSetting.Definition.Key} changed: pushed config to all clients");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[sync] could not push config: {ex.Message}");
            }
        }

        // Client side
        private static void OnConfigFromServer(string snapshot)
        {
            // LethalNetworkAPI also delivers SendClients to the host itself; the host keeps its own file.
            if (LNetworkUtils.IsConnected && LNetworkUtils.IsHostOrServer) return;

            int n = Tuning.ApplySnapshot(snapshot);
            Plugin.Log.LogInfo($"[sync] applied host config ({n} values)");
        }

        private static bool IsHostOnline() => LNetworkUtils.IsConnected && LNetworkUtils.IsHostOrServer;

        // ---------------- voice noise relay ----------------

        // Client side: called when this client's copy of a Coil-Head detected a noise.
        public static void RelayVoiceNoise(SpringManAI coil, Vector3 pos, float loudness)
        {
            if (!LNetworkUtils.IsConnected || LNetworkUtils.IsHostOrServer) return;

            var inv = CultureInfo.InvariantCulture;
            string data = string.Join("|", new[]
            {
                coil.NetworkObjectId.ToString(inv),
                pos.x.ToString("R", inv), pos.y.ToString("R", inv), pos.z.ToString("R", inv),
                loudness.ToString("R", inv),
            });

            try { _noise.SendServer(data); }
            catch (Exception e) { Plugin.Log.LogWarning($"[sync] could not relay voice noise: {e.Message}"); }
        }

        // Host side
        private static void OnNoiseFromClient(string data)
        {
            try
            {
                var inv = CultureInfo.InvariantCulture;
                var p = data.Split('|');
                ulong netId = ulong.Parse(p[0], inv);
                var pos = new Vector3(float.Parse(p[1], inv), float.Parse(p[2], inv), float.Parse(p[3], inv));
                float loud = float.Parse(p[4], inv);

                var nm = NetworkManager.Singleton;
                if (nm == null || !nm.IsServer) return;
                if (!nm.SpawnManager.SpawnedObjects.TryGetValue(netId, out var no) || no == null) return;
                var coil = no.GetComponent<SpringManAI>();
                if (coil == null) return;

                CoilBrain.Get(coil).Hear(coil, pos, loud, VoiceNoiseId);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[sync] bad noise message from a client: {e.Message}");
            }
        }
    }

    // Every way of leaving a lobby (quit, kicked, host left, join refused) ends here.
    [HarmonyPatch(typeof(GameNetworkManager), "SetInstanceValuesBackToDefault")]
    internal static class LeaveLobby_Patch
    {
        [HarmonyPostfix]
        private static void Postfix() => NetSync.OnLocalDisconnect();
    }
}
