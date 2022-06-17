using HarmonyLib;

namespace JukeboxSpotify
{
    [HarmonyPatch(typeof(MusicManager))]
    class MusicManagerPatcher
    {
        [HarmonyPostfix]
        [HarmonyPatch("OnGamePaused")]
        public static void OnGamePausedPostfix()
        {
            if (!MainPatcher.Config.enableModToggle || JukeboxInstance.all.Count == 0 || Vars.noTrack || null == Vars.client) return;
            Vars.menuPause = true;
            Vars.wasPlayingBeforeMenuPause = (Vars.jukeboxIsRunning && !Vars.jukeboxIsPaused);
        }

        [HarmonyPostfix]
        [HarmonyPatch("OnGameResumed")]
        public static void OnGameResumedPostfix()
        {
            if (!MainPatcher.Config.enableModToggle || JukeboxInstance.all.Count == 0 || Vars.noTrack || null == Vars.client) return;
            Vars.menuPause = false;
        }
    }
}