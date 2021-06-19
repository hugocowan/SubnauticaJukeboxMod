using QModManager.API.ModLoading;
using HarmonyLib;


namespace JukeboxSpotify
{
    [QModCore]
    public class MainPatcher
    {
        public static bool? _isPlaying = null;

        [QModPatch]
        public static void Patch()
        {
            Harmony harmony = new Harmony("com.boogaliwoogali.subnautica.jukeboxspotify.mod");
            harmony.PatchAll();
        }
    }
}
