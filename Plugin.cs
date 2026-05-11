using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Reflection;
using System;
using Nautilus.Handlers;

namespace JukeboxSpotify
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("com.snmodding.nautilus")]
    public class Plugin : BaseUnityPlugin
    {
        public new static ManualLogSource Logger { get; private set; }
        public static JukeboxConfig config { get; set; }
        private static Assembly Assembly { get; } = Assembly.GetExecutingAssembly();

        public static bool IsDebugLoggingEnabled => Logger != null && config?.logging == true;

        internal static IMediaController MediaController
        {
            get => Vars.mediaController;
            set => Vars.mediaController = value ?? new NullMediaController();
        }

        public static void LogDebug(string message)
        {
            if (IsDebugLoggingEnabled)
            {
                Logger.LogInfo(message);
            }
        }

        public static void LogDebugError(string message, Exception exception = null)
        {
            if (!IsDebugLoggingEnabled)
            {
                return;
            }

            Logger.LogError(exception == null ? message : message + ": " + exception);
        }

        private void Awake()
        {
            // set project-scoped logger instance
            Logger = base.Logger;

            // Initialize custom prefabs
            //InitializePrefabs();
            config = OptionsPanelHandler.RegisterModOptions<JukeboxConfig>();
            MediaController = MediaControllerFactory.CreateDefault();
            LogDebug("Registered mod options. preferNativeWindowsMediaSession=" + config.preferNativeWindowsMediaSession +
                ", includeArtist=" + config.includeArtist + ", pauseOnLeave=" + config.pauseOnLeave + ", logging=" + config.logging);
            LogDebug("Bootstrapped media controller: " + MediaController.ControllerName);
            // register harmony patches, if there are any
            Harmony.CreateAndPatchAll(Assembly, $"{PluginInfo.PLUGIN_GUID}");
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        }

        //private void InitializePrefabs()
        //{
        //    YeetKnifePrefab.Register();
        //}
    }
}