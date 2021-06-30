using System;
using QModManager.Utility;

namespace JukeboxSpotify
{
    class Error
    {
        public Error(string message, Exception e = null)
        {
            if (MainPatcher.Config.logging) Logger.Log(Logger.Level.Warn, message + ": ", e, true);
        }
    }
}
