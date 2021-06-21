using HarmonyLib;
using QModManager.Utility;

namespace JukeboxSpotify
{
    [HarmonyPatch(typeof(JukeboxInstance))]
    class JukeboxInstancePatcher
    {
        static AccessTools.FieldRef<JukeboxInstance, string> _fileRef = AccessTools.FieldRefAccess<JukeboxInstance, string>("_file");

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
    }
}
