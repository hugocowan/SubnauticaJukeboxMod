using DebounceThrottle;
using HarmonyLib;
using QModManager.Utility;
using SpotifyAPI.Web;

namespace JukeboxSpotify
{
    [HarmonyPatch(typeof(JukeboxInstance))]
    class JukeboxInstancePatcher
    {
        static AccessTools.FieldRef<JukeboxInstance, string> _fileRef = AccessTools.FieldRefAccess<JukeboxInstance, string>("_file");
        static DebounceDispatcher _debounceTimer = new DebounceDispatcher(100);

        [HarmonyPrefix]
        [HarmonyPatch("SetLabel")]
        static void SetLabelPrefix(ref string text)
        {
            text = Spotify._currentTrackTitle;
        }

        [HarmonyPrefix]
        [HarmonyPatch("SetLength")]
        static void SetLengthPrefix(ref uint length)
        {
            length = Spotify._currentTrackLength;
        }

        [HarmonyPrefix]
        [HarmonyPatch("UpdateUI")]
        static void UpdateUIPrefix(JukeboxInstance __instance)
        {
            if (Spotify._jukeboxInstanceNeedsUpdating)
            {
                Spotify._jukeboxInstanceNeedsUpdating = false;
                _fileRef(__instance) = Spotify._currentTrackTitle;
                JukeboxInstance.NotifyInfo(Spotify._currentTrackTitle, new Jukebox.TrackInfo() { label = Spotify._currentTrackTitle, length = Spotify._currentTrackLength });
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch("OnVolume")]
        public static void OnVolumePostfix(float ___volume)
        {
            int volumePercentage = (int) (___volume * 100);

            //Logger.Log(Logger.Level.Info, "OnVolume ___volume: " + volumePercentage, null, true); // volume format is e.g. 0.23456.

            _debounceTimer.Debounce(() => Spotify._spotify.Player.SetVolume(new PlayerVolumeRequest(volumePercentage)));
            
            Jukebox.volume = 0;
        }
    }
}
