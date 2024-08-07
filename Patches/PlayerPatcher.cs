using HarmonyLib;

namespace JukeboxSpotify
{
    [HarmonyPatch(typeof(Player))]
    class PlayerPatcher
    {

        [HarmonyPostfix]
        [HarmonyPatch(typeof(LiveMixin), nameof(LiveMixin.Start))]
        public async static void AwakePostfix()
        {
            if (Plugin.config.enableModToggle && !Vars.spotifyLoginStarted) await Spotify.SpotifyLogin();
        }
    }
}
