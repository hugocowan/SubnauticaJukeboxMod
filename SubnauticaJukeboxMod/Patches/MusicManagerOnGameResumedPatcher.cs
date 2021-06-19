using HarmonyLib;
using QModManager.Utility;
using SpotifyAPI.Web;

namespace JukeboxSpotify
{
    [HarmonyPatch(typeof(MusicManager), "OnGameResumed")]
    class MusicManagerOnGameResumedPatcher
    {
        [HarmonyPostfix]
        public async static void Postfix()
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
