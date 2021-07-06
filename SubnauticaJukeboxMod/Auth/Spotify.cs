using DebounceThrottle;
using QModManager.API;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace JukeboxSpotify
{
    class Spotify
    {
        private static EmbedIOAuthServer _server;
        public static DebounceDispatcher trackDebouncer = new DebounceDispatcher(1000);
        public static ThrottleDispatcher volumeThrottler = new ThrottleDispatcher(333);
        public static bool repeatTrack = false;
        public static bool justStarted;
        public static uint startingPosition = 0;
        public static SpotifyClient client;
        public static bool playingOnStartup = false;
        public static bool spotifyIsPlaying = false;
        public static bool jukeboxIsPlaying;
        public static bool manualJukeboxPause = false;
        public static bool manualJukeboxPlay = false;
        public static bool manualSpotifyPlay = false;
        public static bool manualSpotifyPause = false;
        public static bool jukeboxIsPaused = false;
        public static bool menuPause = false;
        public static bool jukeboxNeedsUpdating = false;
        public static bool jukeboxNeedsPlaying = false;
        public static string defaultTitle = "Spotify Jukebox Mod";
        public static string currentTrackTitle = defaultTitle;
        public static uint currentTrackLength = 0;
        public static float timeTrackStarted = 0;
        public static float playPauseTimeout = 0;
        public static uint timelinePosition = 0;
        public static int spotifyVolume = 100;
        public static float jukeboxVolume = Jukebox.volume;
        public static bool resetJukebox = false;
        public static bool noTrack;
        public static JukeboxInstance currentInstance = null;
        public static int volumeModifier = 1;
        public static int stopCounter = 0;
        public static float getTrackTimer = 0;
        public static float refreshSessionTimer = 0;
        public static float refreshSessionExpiryTime = 3600;
        public static float volumeTimer = 0;

        public async static Task SpotifyLogin()
        {
            try
            {
                // Check the config for the clientId and clientSecret
                if (null == MainPatcher.Config.clientId || null == MainPatcher.Config.clientSecret || "paste_client_id_here" == MainPatcher.Config.clientId || "paste_client_secret_here" == MainPatcher.Config.clientSecret)
                {
                    currentTrackTitle = "Follow instructions on the Nexus mod page to add your Spotify client id and secret to your config.json file, then reload your save.";
                    MainPatcher.Config.clientId = "paste_client_id_here";
                    MainPatcher.Config.clientSecret = "paste_client_secret_here";
                    MainPatcher.Config.Save();
                    QModServices.Main.AddCriticalMessage("Please add your Spotify client id and secret to your config.json file, then reload your save. Instructions are on the Nexus mod page.");
                    return;
                }

                // Check the config for a stored refresh token
                if (null != MainPatcher.Config.refreshToken)
                {
                    try
                    {
                        await RefreshSession();
                    }
                    catch (Exception e)
                    {
                        new Error("An error occurred refreshing the session", e);
                        await RunServer();
                    }
                } // If there wasn't a refresh token, we need to get one
                else
                {
                    await RunServer();
                }

                // This while loop is to give time for the client object to be fully initialised.
                while (null == client)
                {
                    await Task.Delay(1000);
                }

                justStarted = true;

                await GetDevice();

                await GetTrackInfo();

                new Log("Spotify successfully loaded");

            }
            catch (Exception e)
            {
                new Error("Something went wrong loading Spotify", e);
            }
        }

        public async static Task GetDevice()
        {
            try
            {
                // Get an available device to play tracks with.
                Device availableDevice = null;
                DeviceResponse devices = await client.Player.GetAvailableDevices();
                bool foundActiveDevice = false;

                //new Log("devices count: " + devices.Devices.Count, null);

                if (null != MainPatcher.Config.deviceId)
                {
                    availableDevice = new Device() { Id = MainPatcher.Config.deviceId };
                    //new Log("Pre-loaded latest device from config.json in case we need it: " + availableDevice.Id, null);
                }

                devices.Devices.ForEach(delegate (Device device)
                {
                    // Find the first active device.
                    if (
                        false == foundActiveDevice && device.IsActive &&
                        (null == availableDevice || (null != availableDevice && availableDevice.Id != device.Id))
                    )
                    {
                        availableDevice = device;
                        foundActiveDevice = true;

                        MainPatcher.Config.deviceId = device.Id;
                        MainPatcher.Config.Save();
                    }
                });

                // If no active device was found, choose the first one in the devices list.
                if (null == availableDevice && devices.Devices.Count > 0)
                {
                    new Log("Spotify device found");
                    MainPatcher.Config.deviceId = devices.Devices[0].Id;
                    MainPatcher.Config.Save();
                }
                else if (null == availableDevice)
                {
                    new Log("No Spotify device found. Please play/pause your Spotify app.");
                    currentTrackTitle = "No Spotify device found. Please play/pause your Spotify app then try again.";
                    return;
                }
            }
            catch (Exception e)
            {
                new Error("Something went wrong getting a device", e);
            }
        }

        // Update what's currently playing.
        public async static Task GetTrackInfo()
        {
            try
            {
                var currentlyPlaying = await client.Player.GetCurrentPlayback();

                if (null == currentlyPlaying || null == currentlyPlaying.Item)
                {
                    noTrack = true;
                    new Error("Playback not found");
                    if (MainPatcher.Config.deviceId == null) await GetDevice();
                    currentTrackTitle = "Spotify Jukebox Mod - If nothing plays, play/pause your Spotify app then try again.";
                    return;
                }

                var currentTrack = (FullTrack)currentlyPlaying.Item;

                if (uGUI_SceneLoadingPatcher.loadingDone && null != Jukebox.main._instance)
                {
                    // This crashes the game. i cri, erry day i cri
                    //while (
                    //    (currentlyPlaying.RepeatState == "context" && Jukebox.repeat != Jukebox.Repeat.All) ||
                    //    (currentlyPlaying.RepeatState == "track" && Jukebox.repeat != Jukebox.Repeat.Track) ||
                    //    (currentlyPlaying.RepeatState == "off" && Jukebox.repeat != Jukebox.Repeat.Off)
                    //)
                    //{
                    //    new Log("Hitting the repeat button.");
                    //    Jukebox.main._instance.OnButtonRepeat();
                    //    await Task.Delay(500);
                    //}
                    if (Jukebox.shuffle != currentlyPlaying.ShuffleState) Jukebox.main._instance.OnButtonShuffle();
                }

                if (currentTrackTitle == defaultTitle) playingOnStartup = currentlyPlaying.IsPlaying;

                string oldTrackTitle = currentTrackTitle;
                spotifyIsPlaying = currentlyPlaying.IsPlaying;
                startingPosition = (uint)currentlyPlaying.ProgressMs;
                currentTrackLength = (uint)currentTrack.DurationMs;
                noTrack = false;

                if (spotifyIsPlaying && jukeboxIsPaused && !justStarted)
                {
                    manualSpotifyPlay = true;
                }
                else if (!spotifyIsPlaying && !jukeboxIsPaused && !justStarted)
                {
                    manualSpotifyPause = true;
                }

                if (MainPatcher.Config.includeArtist)
                {
                    string artists = "";
                    foreach (SimpleArtist artist in currentTrack.Artists)
                    {
                        artists += (artists == "") ? artist.Name : ", " + artist.Name;
                    }
                    currentTrackTitle = (MainPatcher.Config.includeArtist) ? currentTrack.Name + " - " + artists : currentTrack.Name;
                }
                else
                {
                    currentTrackTitle = currentTrack.Name;
                }

                if (uGUI_SceneLoadingPatcher.loadingDone && (playingOnStartup || !menuPause && jukeboxIsPlaying || oldTrackTitle != currentTrackTitle)) jukeboxNeedsUpdating = true;
            }
            catch (Exception e)
            {
                new Error("Something went wrong getting track info", e);
                currentTrackTitle = "Spotify Jukebox Mod - If nothing plays, play/pause your Spotify app then try again.";
                if (MainPatcher.Config.deviceId == null) await GetDevice();
                if (e.Message == "The access token expired")
                {
                    new Log("Refreshing session in attempt to get a new access token");
                    await RefreshSession();
                }
            }
        }

        public async static Task RunServer()
        {
            try
            {
                _server = new EmbedIOAuthServer(new Uri("http://localhost:5000/callback"), 5000);
                await _server.Start();

                _server.AuthorizationCodeReceived += OnAuthorizationCodeReceived;
                _server.ErrorReceived += OnErrorReceived;

                var request = new LoginRequest(_server.BaseUri, MainPatcher.Config.clientId, LoginRequest.ResponseType.Code)
                {
                    Scope = new[] { Scopes.UserModifyPlaybackState, Scopes.UserReadPlaybackState }
                };
                BrowserUtil.Open(request.ToUri());
            }
            catch (Exception e)
            {
                new Error("Something went wrong running the server", e);
            }
        }

        private async static Task OnAuthorizationCodeReceived(object sender, AuthorizationCodeResponse response)
        {
            try
            {
                await _server.Stop();
                await SetupSpotifyClient(response.Code, true);
            }
            catch (Exception e)
            {
                new Error("Something went wrong receiving the Authorization code", e);
            }
        }

        private async static Task SetupSpotifyClient(string code, bool saveToConfig = false)
        {
            try
            {
                // Get the access token and set up the SpotifyClient.
                SpotifyClientConfig config = SpotifyClientConfig.CreateDefault();
                var tokenResponse = await new OAuthClient(config).RequestToken(
                  new AuthorizationCodeTokenRequest(
                    MainPatcher.Config.clientId, MainPatcher.Config.clientSecret, code, new Uri("http://localhost:5000/callback")
                  )
                );

                if (saveToConfig)
                {
                    MainPatcher.Config.refreshToken = tokenResponse.RefreshToken;
                    MainPatcher.Config.Save();
                }

                config = SpotifyClientConfig
                    .CreateDefault()
                    .WithAuthenticator(new AuthorizationCodeAuthenticator(MainPatcher.Config.clientId, MainPatcher.Config.clientSecret, tokenResponse));

                client = new SpotifyClient(config);
            }
            catch (Exception e)
            {
                new Error("Something went wrong setting up the Spotify client", e);
            }
        }

        private static async Task OnErrorReceived(object sender, string error, string state)
        {
            try
            {
                new Error("Server failed: " + error);
                await _server.Stop();
            }
            catch (Exception e)
            {
                new Error("Something went wrong while stopping the server after an error", e);
            }
        }

        public async static Task RefreshSession()
        {
            try
            {
                var newResponse = await new OAuthClient().RequestToken(
                  new AuthorizationCodeRefreshRequest(MainPatcher.Config.clientId, MainPatcher.Config.clientSecret, MainPatcher.Config.refreshToken)
                );

                client = new SpotifyClient(newResponse.AccessToken);
                new Log("Refreshed the Spotify session.");
                refreshSessionExpiryTime = newResponse.ExpiresIn;
                refreshSessionTimer = Time.time;
            }
            catch (Exception e)
            {
                new Error("Something went wrong refreshing the Spotify session", e);
            }
        }
    }
}
