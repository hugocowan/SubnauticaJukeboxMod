namespace JukeboxSpotify
{
    internal static class MediaControllerFactory
    {
        public static IMediaController CreateDefault()
        {
            bool preferNativeWindowsMediaSession = Plugin.config == null || Plugin.config.preferNativeWindowsMediaSession;
            IMediaController controller = preferNativeWindowsMediaSession
                ? new WindowsMediaSessionController()
                : new SpotifyWebApiMediaController();

            Plugin.LogDebug("MediaControllerFactory selected backend '" + controller.ControllerName +
                "'. preferNativeWindowsMediaSession=" + preferNativeWindowsMediaSession + ", configLoaded=" + (Plugin.config != null));

            if (preferNativeWindowsMediaSession)
            {
                return controller;
            }

            return controller;
        }
    }
}