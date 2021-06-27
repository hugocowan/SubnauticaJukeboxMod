using SMLHelper.V2.Json;
using SMLHelper.V2.Options.Attributes;

namespace JukeboxSpotify
{
    [Menu("JukeboxSpotify", LoadOn = MenuAttribute.LoadEvents.MenuRegistered | MenuAttribute.LoadEvents.MenuOpened)]
    public class Config : ConfigFile
    {
        [Toggle("Pause Jukebox when you leave")]
        public bool PauseOnLeaveToggleValue = true;

        public string clientId;

        public string clientSecret;

        public string refreshToken;

        public string deviceId;
    }
}
