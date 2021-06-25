using SMLHelper.V2.Json;
using SMLHelper.V2.Options;
using SMLHelper.V2.Options.Attributes;

namespace JukeboxSpotify
{
    [Menu("JukeboxSpotify", LoadOn = MenuAttribute.LoadEvents.MenuRegistered | MenuAttribute.LoadEvents.MenuOpened)]
    public class Config : ConfigFile
    {
        [Toggle("Pause/Resume Jukebox on leaving/returning"), OnChange(nameof(MyCheckboxToggleEvent))]
        public bool PauseOnLeaveToggleValue = true;


        private void MyCheckboxToggleEvent(ToggleChangedEventArgs e)
        {
            JukeboxPatcher.pauseOnLeaving = e.Value;
        }
    }
}
