using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace EnhancedCoilhead
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.NAME, PluginInfo.VERSION)]
    [BepInDependency("ainavt.lc.lethalconfig", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("LethalNetworkAPI")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;

            Tuning.Bind(Config);
            NetSync.Init(Config);

#if HAS_LETHALCONFIG
            if (Chainloader.PluginInfos.ContainsKey("ainavt.lc.lethalconfig"))
                LethalConfigSetup.Register();
#endif

            new Harmony(PluginInfo.GUID).PatchAll();
            Log.LogInfo($"{PluginInfo.NAME} {PluginInfo.VERSION} loaded");
        }
    }

    internal static class PluginInfo
    {
        public const string GUID = "com.phakin.enhancedcoilhead";
        public const string NAME = "EnhancedCoilhead";
        public const string VERSION = "0.1.0";
    }
}
