using HarmonyLib;

namespace JukeboxSpotify
{
    [HarmonyPatch(typeof(Player), "Awake")]
    class PlayerAwakePatcher
    {
        [HarmonyPostfix]
        public async static void InitiateDBAndSpotify()
        {
            new SQL();
            await Spotify.SpotifyLogin();
        }
    }
}
