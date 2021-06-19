using System;
using HarmonyLib;

namespace JukeboxSpotify
{
    [HarmonyPatch]
    class SetInfoPatcher
    {
        public static void MySetInfo(object instance, string id, Jukebox.TrackInfo info)
        {
            throw new NotImplementedException("It's a stub");
        }
    }
}
