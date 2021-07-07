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
            if (!MainPatcher.Config.enableModToggle || Spotify.noTrack || null == Spotify.client) return;
            Spotify.menuPause = true;
        }

        [HarmonyPostfix]
        [HarmonyPatch("OnGameResumed")]
        public static void OnGameResumedPostfix()
        {
            if (!MainPatcher.Config.enableModToggle || Spotify.noTrack || null == Spotify.client) return;
            Spotify.menuPause = false;
        }

    }
}