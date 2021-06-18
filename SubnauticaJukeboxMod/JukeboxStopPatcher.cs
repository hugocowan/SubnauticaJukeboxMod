using HarmonyLib;

namespace JukeboxSpotify
{
    [HarmonyPatch(typeof(Jukebox))]
    [HarmonyPatch("Stop")]
    class JukeboxStopPatcher
    {
    }
}
