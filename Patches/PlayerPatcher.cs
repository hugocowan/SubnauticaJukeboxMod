using HarmonyLib;

namespace JukeboxSpotify
{
    [HarmonyPatch(typeof(Player))]
    class PlayerPatcher
    {

        [HarmonyPostfix]
        [HarmonyPatch(typeof(LiveMixin), nameof(LiveMixin.Start))]
        public async static void AwakePostfix()
        {
            if (Plugin.config.enableModToggle && !Vars.mediaControllerInitializationStarted)
            {
                Plugin.LogDebug("Player patch triggered media-controller initialization from LiveMixin.Start.");
                await MediaPlaybackCoordinator.InitializeAsync();
                return;
            }

            Plugin.LogDebug("Player patch skipped media-controller initialization. enableModToggle=" + Plugin.config.enableModToggle +
                ", mediaControllerInitializationStarted=" + Vars.mediaControllerInitializationStarted + ".");
        }
    }
}
