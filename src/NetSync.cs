using System;
using System.Globalization;
using LethalNetworkAPI;
using Unity.Netcode;
using UnityEngine;

namespace EnhancedCoilhead
{
    // Multiplayer glue (via LethalNetworkAPI):
    //  1. host -> clients: config snapshot, so everyone runs the host's values
    //  2. client -> host: voice noises, so the host-side AI can hear clients talking
    internal static class NetSync
    {
        public const int VoiceNoiseId = 75; // StartOfRound.DetectVoiceChatAmplitude

        private static LNetworkMessage<string> _config;   // host -> clients (config snapshot)
        private static LNetworkMessage<string> _request;  // client -> host (please send config)
        private static LNetworkMessage<string> _noise;    // client -> host (voice noise)

        private static bool _requested;
        private static bool _hostDirty = true;
        private static bool _hadOverrides;

        public static void Init(BepInEx.Configuration.ConfigFile config)
        {
            _config  = LNetworkMessage<string>.Create("EnhancedCoilhead.Config",
                onClientReceived: snapshot => OnConfigFromServer(snapshot));
            _request = LNetworkMessage<string>.Create("EnhancedCoilhead.ConfigRequest",
                onServerReceived: (data, sender) => _config.SendClients(Tuning.Snapshot()));
            _noise   = LNetworkMessage<string>.Create("EnhancedCoilhead.Noise",
                onServerReceived: (data, sender) => OnNoiseFromClient(data));

            config.SettingChanged += (_, __) => _hostDirty = true;
        }

        // Called every frame from Plugin.Update
        public static void Tick()
        {
            var nm = NetworkManager.Singleton;
            bool online = nm != null && nm.IsListening;

            if (!online || (!nm.IsServer && !nm.IsConnectedClient))
            {
                _requested = false;
                if (_hadOverrides) { Tuning.ClearOverrides(); _hadOverrides = false; }
                return;
            }

            if (nm.IsServer)
            {
                // push to everyone when the host changes a value
                if (_hostDirty)
                {
                    _hostDirty = false;
                    _config.SendClients(Tuning.Snapshot());
                }
            }
            else if (!_requested)
            {
                _requested = true;
                _request.SendServer("hello");
            }
        }

        // --- config ---
        private static void OnConfigFromServer(string snapshot)
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsServer) return; // host uses its own file
            Tuning.ApplySnapshot(snapshot);
            _hadOverrides = true;
            Plugin.Log.LogInfo("[sync] applied host config");
        }

        // --- voice noise relay ---
        // Client side: called when this client's copy of a Coil-Head detected a noise.
        public static void RelayVoiceNoise(SpringManAI coil, Vector3 pos, float loudness)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.IsServer || !nm.IsConnectedClient) return;

            var inv = CultureInfo.InvariantCulture;
            string data = string.Join("|", new[]
            {
                coil.NetworkObjectId.ToString(inv),
                pos.x.ToString("R", inv), pos.y.ToString("R", inv), pos.z.ToString("R", inv),
                loudness.ToString("R", inv),
            });
            _noise.SendServer(data);
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
}
