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
            if (true == Spotify.isPlaying)
            {
                Logger.Log(Logger.Level.Info, "Game paused, pausing track", null, true);
                Spotify.isPlaying = false;
                await Spotify.client.Player.PausePlayback(new PlayerPausePlaybackRequest() { DeviceId = Spotify.device.Id });
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch("OnGameResumed")]
        public async static void OnGameResumedPostfix()
        {
            if (false == Spotify.isPlaying)
            {
                Logger.Log(Logger.Level.Info, "Game resumed, resuming track", null, true);
                Spotify.isPlaying = true;
                await Spotify.client.Player.ResumePlayback(new PlayerResumePlaybackRequest() { DeviceId = Spotify.device.Id });
            }
        }

    }
}
