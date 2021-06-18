using QModManager.API.ModLoading;
using HarmonyLib;


namespace JukeboxSpotify
{
    [QModCore]
    public class MainPatcher
    {
        [QModPatch]
        public async static void Patch()
        {
            new SQL();
            await Spotify.SpotifyLogin();
            Harmony harmony = new Harmony("com.boogaliwoogali.subnautica.jukeboxspotify.mod");
            harmony.PatchAll();
        }
    }
}
