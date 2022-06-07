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
            if (MainPatcher.Config.enableModToggle) await Spotify.SpotifyLogin();
        }
    }
}
