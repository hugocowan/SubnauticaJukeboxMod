using HarmonyLib;
using QModManager.Utility;

namespace JukeboxSpotify
{
    [HarmonyPatch(typeof(Jukebox))]
    [HarmonyPatch("Stop")]
    class JukeboxStopPatcher
    {
        [HarmonyPostfix]
        public async static void Postfix()
        {
            Logger.Log(Logger.Level.Info, "Pausing track", null, true);
            await Spotify._spotify.Player.PausePlayback();
        }
    }
}
