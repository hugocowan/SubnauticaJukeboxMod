using QModManager.API.ModLoading;
using SMLHelper.V2.Handlers;
using HarmonyLib;


namespace JukeboxSpotify
{
    [QModCore]
    public class MainPatcher
    {
        public static Config Config { get; private set; }

        [QModPatch]
        public static void Patch()
        {
            Config = OptionsPanelHandler.Main.RegisterModOptions<Config>();

            JukeboxPatcher.pauseOnLeaving = Config.PauseOnLeaveToggleValue;

            Harmony harmony = new Harmony("com.boogaliwoogali.subnautica.jukeboxspotify.mod");
            harmony.PatchAll();
        }
    }
}
