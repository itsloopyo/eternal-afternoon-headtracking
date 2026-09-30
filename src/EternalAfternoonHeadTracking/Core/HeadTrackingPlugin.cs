using BepInEx;

namespace EternalAfternoonHeadTracking
{
    /// <summary>
    /// BepInEx entry point. The mod runs on GameObjects of its own (see ModLoader), so the
    /// plugin only starts it.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, HeadTrackingMod.ModVersion)]
    public sealed class HeadTrackingPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.cameraunlock.eternalafternoon.headtracking";
        public const string PluginName = "Eternal Afternoon Head Tracking";

        private void Awake()
        {
            ModLoader.Initialize();
        }
    }
}
