using HarmonyLib;

namespace JukeboxSpotify
{
    [HarmonyPatch(typeof(uGUI_SceneLoading))]
    class uGUI_SceneLoadingPatcher
    {
        public static bool loadingDone = false;

        [HarmonyPostfix]
        [HarmonyPatch("End")]
        public static void EndPostfix(bool fade = true)
        {
            loadingDone = true;
        }
    }
}
