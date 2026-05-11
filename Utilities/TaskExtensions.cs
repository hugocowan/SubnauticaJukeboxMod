using System;
using System.Threading.Tasks;

namespace JukeboxSpotify
{
    internal static class TaskExtensions
    {
        public static async void Forget(this Task task, string operationName)
        {
            try
            {
                await task;
            }
            catch (Exception e)
            {
                if (Plugin.config?.logging == true)
                {
                    Plugin.Logger.LogError($"{operationName} failed: {e}");
                }
            }
        }
    }
}