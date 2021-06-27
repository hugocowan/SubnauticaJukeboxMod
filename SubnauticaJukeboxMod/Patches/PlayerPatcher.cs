using HarmonyLib;

namespace JukeboxSpotify
{
    [HarmonyPatch(typeof(Player))]
    class PlayerPatcher
    {

        [HarmonyPostfix]
        [HarmonyPatch("Awake")]
        public async static void AwakePostfix()
        {
            await Spotify.SpotifyLogin();
        }
    }
}
