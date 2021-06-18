using System;
using QModManager.Utility;

namespace JukeboxSpotify
{
    class ErrorHandler
    {
        public ErrorHandler(Exception e, string message)
        {
            var error = new Exception(string.Format(e.Message));
            Logger.Log(Logger.Level.Warn, message + ": " + error, null, true);
        }
    }
}
