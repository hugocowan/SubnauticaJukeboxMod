using HarmonyLib;
using QModManager.Utility;
using SpotifyAPI.Web;

namespace JukeboxSpotify
{
    [HarmonyPatch(typeof(MusicManager))]
    class MusicManagerPatcher
    {
        [HarmonyPostfix]
        [HarmonyPatch("OnGamePaused")]
        public async static void OnGamePausedPostfix()
        {
            if (true == MainPatcher._isPlaying)
            {
                Logger.Log(Logger.Level.Info, "Game paused, pausing track", null, true);
                MainPatcher._isPlaying = false;
                var playbackRequest = new PlayerPausePlaybackRequest() { DeviceId = Spotify._device.Id };
                await Spotify._spotify.Player.PausePlayback(playbackRequest);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch("OnGameResumed")]
        public async static void OnGameResumedPostfix()
        {
            if (false == MainPatcher._isPlaying)
            {
                Logger.Log(Logger.Level.Info, "Game resumed, resuming track", null, true);
                MainPatcher._isPlaying = true;
                var playbackRequest = new PlayerResumePlaybackRequest() { DeviceId = Spotify._device.Id };
                await Spotify._spotify.Player.ResumePlayback(playbackRequest);
            }
        }

    }
}
