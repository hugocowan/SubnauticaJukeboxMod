using System;
using QModManager.Utility;

namespace JukeboxSpotify
{
    class ErrorHandler
    {
        public ErrorHandler(Exception e, string message)
        {
            Logger.Log(Logger.Level.Warn, message + ": ", e, false);
        }
    }
}
