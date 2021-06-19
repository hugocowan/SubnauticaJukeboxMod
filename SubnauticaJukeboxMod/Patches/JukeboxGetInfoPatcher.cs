using HarmonyLib;
using QModManager.Utility;
using System;

namespace JukeboxSpotify
{
    [HarmonyPatch(typeof(Jukebox), "GetInfo", typeof(string))]
    class JukeboxGetInfoPatcher
    {
        [HarmonyPostfix]
        public static void Postfix(string id, ref Jukebox.TrackInfo __result)
        {
            //Logger.Log(Logger.Level.Info, "id: " + id + " | info.label: " + info.label + " | info.length: " + info.length, null, true);
            Logger.Log(Logger.Level.Info, "id: " + id, null, true);
            Logger.Log(Logger.Level.Info, "old result.label: " + __result.label, null, true);
            Logger.Log(Logger.Level.Info, "old result.length: " + __result.length, null, true);

            __result = new Jukebox.TrackInfo() { label = MainPatcher._currentTrackTitle, length = MainPatcher._currentTrackLength };

            Logger.Log(Logger.Level.Info, "new result.label: " + __result.label, null, true);
            Logger.Log(Logger.Level.Info, "new result.length: " + __result.length, null, true);
        }
    }
}
