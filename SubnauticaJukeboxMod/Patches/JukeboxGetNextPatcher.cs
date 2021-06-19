using HarmonyLib;
using SpotifyAPI.Web;

namespace JukeboxSpotify
{
    [HarmonyPatch(typeof(Jukebox), "GetNext")]
    class JukeboxGetNextPatcher
    {
        [HarmonyPostfix]
        public async static void Postfix(JukeboxInstance jukebox, bool forward)
        {
            
            if (forward)
            {
                await Spotify._spotify.Player.SkipNext(new PlayerSkipNextRequest() { DeviceId = Spotify._device.Id });
            }
            else
            {
                await Spotify._spotify.Player.SkipPrevious(new PlayerSkipPreviousRequest() { DeviceId = Spotify._device.Id });
            }

            if (null == MainPatcher._isPlaying || false == MainPatcher._isPlaying)
            {
                await Spotify._spotify.Player.PausePlayback(new PlayerPausePlaybackRequest() { DeviceId = Spotify._device.Id });
            }
        }
    }
}
