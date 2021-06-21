using QModManager.API.ModLoading;
using HarmonyLib;


namespace JukeboxSpotify
{
    [QModCore]
    public class MainPatcher
    {
        [QModPatch]
        public static void Patch()
        {
            Harmony harmony = new Harmony("com.boogaliwoogali.subnautica.jukeboxspotify.mod");
            harmony.PatchAll();
        }
    }
}
