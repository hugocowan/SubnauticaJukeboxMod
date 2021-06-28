using HarmonyLib;
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
            if (true == Spotify.jukeboxIsPlaying)
            {
                //Logger.Log(Logger.Level.Info, "Game paused, pausing track", null, true);
                Spotify.jukeboxIsPlaying = false;
                await Spotify.client.Player.PausePlayback(new PlayerPausePlaybackRequest() { DeviceId = MainPatcher.Config.deviceId });
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch("OnGameResumed")]
        public async static void OnGameResumedPostfix()
        {
            if (false == Spotify.jukeboxIsPlaying)
            {
                //Logger.Log(Logger.Level.Info, "Game resumed, resuming track", null, true);
                Spotify.jukeboxIsPlaying = true;
                await Spotify.client.Player.ResumePlayback(new PlayerResumePlaybackRequest() { DeviceId = MainPatcher.Config.deviceId });
            }
        }

    }
}
