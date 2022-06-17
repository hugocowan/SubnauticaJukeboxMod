using DebounceThrottle;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;

namespace JukeboxSpotify
{
    internal static class Vars
    {
        public static ThrottleDispatcher volumeThrottler = new ThrottleDispatcher(333);
        public static bool repeatTrack;
        public static bool justStarted;
        public static uint startingPosition = 0;
        public static SpotifyClient client;
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
        public static string currentTrackTitle = "Spotify Jukebox Mod";
        public static uint currentTrackLength = 0;
        public static float timeTrackStarted = 0;
        public static float playPauseTimeout = 0;
        public static int spotifyVolume = 100;
        public static float jukeboxVolume = Jukebox.volume;
        public static bool resetJukebox;
        public static bool spotifyShuffleState;
        public static bool noTrack;
        public static bool beyondFiveMins;
        public static bool positionDrag;
        public static JukeboxInstance currentInstance = null;
        public static int volumeModifier = 1;
        public static int stopCounter = 0;
        public static float getTrackTimer = 0;
        public static float refreshSessionTimer = 0;
        public static float refreshSessionExpiryTime = 3600;
        public static float volumeTimer = 0;
        public static float jukeboxActionTimer = 0;
        public static float currentPosition = 0;
        public static EmbedIOAuthServer _server;

        public static void reset()
        {
            _server = null;
            volumeThrottler = new ThrottleDispatcher(333);
            repeatTrack = false;
            justStarted = false;
            startingPosition = 0;
            client = null;
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
            currentTrackTitle = "Spotify Jukebox Mod";
            currentTrackLength = 0;
            timeTrackStarted = 0;
            playPauseTimeout = 0;
            spotifyVolume = 100;
            jukeboxVolume = Jukebox.volume;
            resetJukebox = false;
            spotifyShuffleState = false;
            noTrack = false;
            beyondFiveMins = false;
            positionDrag = false;
            currentInstance = null;
            volumeModifier = 1;
            stopCounter = 0;
            getTrackTimer = 0;
            refreshSessionTimer = 0;
            refreshSessionExpiryTime = 3600;
            volumeTimer = 0;
            jukeboxActionTimer = 0;
            currentPosition = 0;
        }
    }
}