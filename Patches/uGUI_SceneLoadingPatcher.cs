using HarmonyLib;

namespace JukeboxSpotify
{
    [HarmonyPatch(typeof(uGUI_SceneLoading))]
    class uGUI_SceneLoadingPatcher
    {
        public static bool loadingDone = false;

        [HarmonyPostfix]
        [HarmonyPatch("End")]
        public static void EndPostfix()
        {
            loadingDone = true;
            Plugin.LogDebug("Scene loading completed. uGUI_SceneLoading.End fired and loadingDone was set to true.");
        }
    }
}
