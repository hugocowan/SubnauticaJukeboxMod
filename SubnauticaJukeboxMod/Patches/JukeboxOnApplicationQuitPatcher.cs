using HarmonyLib;
using SpotifyAPI.Web;

namespace JukeboxSpotify
{
    [HarmonyPatch(typeof(Jukebox), "OnApplicationQuit")]
    class JukeboxOnApplicationQuitPatcher
    {
        [HarmonyPostfix]
        public async static void Postfix()
        {
            MainPatcher._isPlaying = null;
            var playbackRequest = new PlayerPausePlaybackRequest() { DeviceId = Spotify._device.Id };
            await Spotify._spotify.Player.PausePlayback(playbackRequest);
        }
    }
}
