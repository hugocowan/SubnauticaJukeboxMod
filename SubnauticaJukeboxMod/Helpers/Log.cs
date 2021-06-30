using System;
using QModManager.Utility;

namespace JukeboxSpotify
{
    class Log
    {
        public Log(string message, Exception e = null)
        {
            if (MainPatcher.Config.logging)
            {
                Logger.Log(Logger.Level.Info, message, e, true);
            }
        }
    }
}
