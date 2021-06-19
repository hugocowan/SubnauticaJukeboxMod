using HarmonyLib;
using QModManager.Utility;
using SpotifyAPI.Web;

namespace JukeboxSpotify
{
    [HarmonyPatch(typeof(Jukebox), "GetNext")]
    class JukeboxGetNextPatcher
    {
        [HarmonyPostfix]
        public async static void Postfix()
        {
            Logger.Log(Logger.Level.Info, "Skipping track", null, true);
            await Spotify._spotify.Player.SkipNext();
            if (null == MainPatcher._isPlaying || false == MainPatcher._isPlaying)
            {
                var playbackRequest = new PlayerPausePlaybackRequest() { DeviceId = Spotify._device.Id };
                await Spotify._spotify.Player.PausePlayback(playbackRequest);
            }
        }
    }
}
