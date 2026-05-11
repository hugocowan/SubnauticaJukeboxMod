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
            if (!Plugin.config.enableModToggle || JukeboxInstance.all.Count == 0 || Vars.noTrack || !Vars.HasActiveMediaController) return;
            Vars.menuPause = true;
            Vars.wasPlayingBeforeMenuPause = (Vars.jukeboxIsRunning && !Vars.jukeboxIsPaused);
        }

        [HarmonyPostfix]
        [HarmonyPatch("OnGameResumed")]
        public static void OnGameResumedPostfix()
        {
            if (!Plugin.config.enableModToggle || JukeboxInstance.all.Count == 0 || Vars.noTrack || !Vars.HasActiveMediaController) return;
            Vars.menuPause = false;
        }
    }
}