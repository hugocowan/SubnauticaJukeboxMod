using Nautilus.Json;
using Nautilus.Options;
using Nautilus.Options.Attributes;

namespace JukeboxSpotify
{
    [Menu("JukeboxSpotify", LoadOn = MenuAttribute.LoadEvents.MenuRegistered | MenuAttribute.LoadEvents.MenuOpened)]
    public class JukeboxConfig : ConfigFile
    {
        [Toggle("Enable/Disable the Jukebox mod"), OnChange(nameof(MyCheckboxToggleEvent))]
        public bool enableModToggle = true;

        [Toggle("Pause Jukebox when you leave")]
        public bool pauseOnLeave = true;

        [Toggle("Include Artist name in title")]
        public bool includeArtist = true;

        [Toggle("Double press ■ for song start")]
        public bool stopTwiceForStart = false;

        [Toggle("Prefer native Windows media session"), OnChange(nameof(OnBackendPreferenceChanged))]
        public bool preferNativeWindowsMediaSession = true;

        [Toggle("Enable logging (for debugging)")]
        public bool logging = true;

        public string clientId;

        public string clientSecret;

        public string refreshToken;

        public string deviceId;

        private async void OnBackendPreferenceChanged(ToggleChangedEventArgs e)
        {
            Plugin.LogDebug("Backend preference changed. preferNativeWindowsMediaSession=" + e.Value + ". Resetting jukebox state and reinitializing media control.");

            if (!enableModToggle)
            {
                Plugin.LogDebug("Skipping backend reinitialization because the mod is disabled.");
                return;
            }

            Vars.manualPause = true;
            Vars.resetJukebox = true;
            Plugin.MediaController = MediaControllerFactory.CreateDefault();
            await MediaPlaybackCoordinator.InitializeAsync();
        }

        private async void MyCheckboxToggleEvent(ToggleChangedEventArgs e)
        {
            Plugin.LogDebug("Mod enabled toggle changed. enabled=" + e.Value);
            Vars.manualPause = true;

            if (!e.Value)
            {
                Plugin.LogDebug("Marking jukebox for reset because the mod was disabled.");
                Vars.resetJukebox = true;
            }
            else
            {
                Plugin.LogDebug("Reinitializing media playback because the mod was enabled.");
                await MediaPlaybackCoordinator.InitializeAsync();
            }
        }
    }
}
