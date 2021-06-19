using HarmonyLib;
using QModManager.Utility;
using SpotifyAPI.Web;

namespace JukeboxSpotify
{
    [HarmonyPatch(typeof(MusicManager), "OnGamePaused")]
    class MusicManagerOnGamePausedPatcher
    {
        [HarmonyPostfix]
        public async static void Postfix()
        {
            if (true == MainPatcher._isPlaying)
            {
                Logger.Log(Logger.Level.Info, "Game paused, pausing track", null, true);
                MainPatcher._isPlaying = false;
                var playbackRequest = new PlayerPausePlaybackRequest() { DeviceId = Spotify._device.Id };
                await Spotify._spotify.Player.PausePlayback(playbackRequest);
            }
        }
    }
}
