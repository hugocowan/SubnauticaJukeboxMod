using HarmonyLib;
using QModManager.Utility;
using SpotifyAPI.Web;

namespace JukeboxSpotify
{
    [HarmonyPatch(typeof(PlatformUtils), "OnDestroy")]
    class PlatformUtilsOnDestroyPatcher
    {
        [HarmonyPostfix]
        public async static void Postfix()
        {
            Logger.Log(Logger.Level.Info, "Game shutting down, we pausin'", null, true);
            MainPatcher._isPlaying = null;
            var playbackRequest = new PlayerPausePlaybackRequest() { DeviceId = Spotify._device.Id };
            await Spotify._spotify.Player.PausePlayback(playbackRequest);
        }
    }
}
