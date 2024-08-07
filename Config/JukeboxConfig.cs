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

        [Toggle("Enable logging (for debugging)")]
        public bool logging = false;

        public string clientId;

        public string clientSecret;

        public string refreshToken;

        public string deviceId;

        private async void MyCheckboxToggleEvent(ToggleChangedEventArgs e)
        {
            Vars.manualPause = true;

            if (!e.Value)
            {
                Vars.resetJukebox = true;
            }
            else
            {
                await Spotify.SpotifyLogin();
            }
        }
    }
}
