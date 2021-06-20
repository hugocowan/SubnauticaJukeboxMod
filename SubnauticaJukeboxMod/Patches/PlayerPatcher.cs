using HarmonyLib;

namespace JukeboxSpotify.Patches
{
    [HarmonyPatch(typeof(Player))]
    class PlayerPatcher
    {

        [HarmonyPostfix]
        [HarmonyPatch("Awake")]
        public async static void AwakePostfix()
        {
            new SQL();
            await Spotify.SpotifyLogin();
        }
    }
}
