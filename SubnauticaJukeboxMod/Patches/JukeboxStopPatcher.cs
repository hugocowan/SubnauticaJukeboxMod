using HarmonyLib;
using SpotifyAPI.Web;

namespace JukeboxSpotify
{
    [HarmonyPatch(typeof(Jukebox), "StopInternal")]
    class JukeboxStopPatcher
    {
        [HarmonyPostfix]
        public async static void Postfix()
        {
            MainPatcher._isPlaying = null;
            MainPatcher._isPaused = false;
            await Spotify._spotify.Player.PausePlayback(new PlayerPausePlaybackRequest() { DeviceId = Spotify._device.Id });
            await Spotify._spotify.Player.SeekTo(new PlayerSeekToRequest(0));
        }
    }
}