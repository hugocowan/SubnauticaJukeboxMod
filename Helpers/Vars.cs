using DebounceThrottle;
using System;

namespace JukeboxSpotify
{
    internal static class Vars
    {
        public static ThrottleDispatcher volumeThrottler = new ThrottleDispatcher(TimeSpan.FromMilliseconds(333));
        public static IMediaController mediaController = new NullMediaController();
        public static bool repeatTrack;
        public static bool justStarted;
        public static uint startingPosition = 0;
        public static bool mediaControllerInitializationStarted;
        public static bool playingOnStartup;
        public static bool newJukeboxInstance;
        public static bool jukeboxIsRunning;
        public static bool manualPause;
        public static bool manualPlay;
        public static bool jukeboxIsPaused;
        public static bool menuPause;
        public static bool distancePause;
        public static bool wasPlayingBeforeMenuPause;
        public static bool jukeboxNeedsUpdating;
        public static string defaultTrack = "event:/jukebox/jukebox_takethedive";
        public static string currentTrackTitle = "OS Media Jukebox";
        public static uint currentTrackLength = 0;
        public static float timeTrackStarted = 0;
        public static float playPauseTimestamp = 0;
        public static int sourceVolume = 100;
        public static float jukeboxVolume = Jukebox.volume;
        public static bool resetJukebox;
        public static bool sourceShuffleState;
        public static bool noTrack;
        public static bool beyondFiveMins;
        public static bool positionDrag;
        public static JukeboxInstance currentInstance = null;
        public static int volumeModifier = 1;
        public static int stopCounter = 0;
        public static float getTrackTimer = 0;
        public static float volumeTimer = 0;
        public static float jukeboxActionTimestamp = 0;
        public static float currentPosition = 0;

        public static bool HasActiveMediaController => mediaController != null && mediaController.IsReady;

        public static void reset()
        {
            volumeThrottler = new ThrottleDispatcher(TimeSpan.FromMilliseconds(333));
            mediaController = new NullMediaController();
            repeatTrack = false;
            justStarted = false;
            startingPosition = 0;
            mediaControllerInitializationStarted = false;
            playingOnStartup = false;
            newJukeboxInstance = false;
            jukeboxIsRunning = false;
            manualPause = false;
            manualPlay = false;
            jukeboxIsPaused = false;
            menuPause = false;
            distancePause = false;
            wasPlayingBeforeMenuPause = false;
            jukeboxNeedsUpdating = false;
            currentTrackTitle = "OS Media Jukebox";
            currentTrackLength = 0;
            timeTrackStarted = 0;
            playPauseTimestamp = 0;
            sourceVolume = 100;
            jukeboxVolume = Jukebox.volume;
            resetJukebox = false;
            sourceShuffleState = false;
            noTrack = false;
            beyondFiveMins = false;
            positionDrag = false;
            currentInstance = null;
            volumeModifier = 1;
            stopCounter = 0;
            getTrackTimer = 0;
            volumeTimer = 0;
            jukeboxActionTimestamp = 0;
            currentPosition = 0;
        }
    }
}