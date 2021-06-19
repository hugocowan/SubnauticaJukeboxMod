using HarmonyLib;
using SpotifyAPI.Web;

namespace JukeboxSpotify
{
    [HarmonyPatch(typeof(Jukebox), "UpdateStudio")]
    class JukeboxUpdateStudioPatcher
    {
        [HarmonyPostfix]
        public static void Postfix(ref bool ____paused)
        {
            if (____paused && MainPatcher._isPaused == false)
            {
                MainPatcher._isPaused = true;
                Spotify._spotify.Player.PausePlayback(new PlayerPausePlaybackRequest() { DeviceId = Spotify._device.Id });
            } else if (!____paused && true == MainPatcher._isPaused && true == MainPatcher._isPlaying)
            {
                MainPatcher._isPaused = false;
                Spotify._spotify.Player.ResumePlayback(new PlayerResumePlaybackRequest() { DeviceId = Spotify._device.Id });
            }
        }
    }
}

