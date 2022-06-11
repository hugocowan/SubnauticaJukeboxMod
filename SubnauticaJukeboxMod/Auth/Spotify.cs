using DebounceThrottle;
using QModManager.API;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace JukeboxSpotify
{
    class Spotify
    {
        private static EmbedIOAuthServer _server;
        public static ThrottleDispatcher volumeThrottler = new ThrottleDispatcher(333);
        public static bool repeatTrack;
        public static bool justStarted;
        public static uint startingPosition = 0;
        public static SpotifyClient client;
        public static bool playingOnStartup;
        public static bool newJukeboxInstance;
        public static bool jukeboxIsPlaying;
        public static bool manualPause;
        public static bool manualPlay;
        public static bool jukeboxIsPaused;
        public static bool menuPause;
        public static bool distancePause;
        public static bool wasPlayingBeforeMenuPause;
        public static bool jukeboxNeedsUpdating;
        public static string defaultTitle = "Spotify Jukebox Mod";
        public static string defaultTrack = "event:/jukebox/jukebox_takethedive";
        public static string currentTrackTitle = defaultTitle;
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
            jukeboxIsPlaying = false;
            manualPause = false;
            manualPlay = false;
            jukeboxIsPaused = false;
            menuPause = false;
            distancePause = false;
            wasPlayingBeforeMenuPause = false;
            jukeboxNeedsUpdating = false;
            currentTrackTitle = defaultTitle;
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

                justStarted = true;

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
                    if (MainPatcher.Config.deviceId == null) await GetDevice();
                    await client.Player.TransferPlayback(new PlayerTransferPlaybackRequest(new List<string>() { MainPatcher.Config.deviceId }));
                    new Error("Playback not found");
                    currentTrackTitle = "Spotify Jukebox Mod - If nothing plays, play/pause your Spotify app then try again.";
                    return;
                }

                var currentTrack = (FullTrack)currentlyPlaying.Item;

                if (uGUI_SceneLoadingPatcher.loadingDone && null != Jukebox.main._instance)
                {
                    spotifyShuffleState = currentlyPlaying.ShuffleState;
                    if (Jukebox.shuffle != currentlyPlaying.ShuffleState) Jukebox.main._instance.OnButtonShuffle();
                }

                if (justStarted) playingOnStartup = currentlyPlaying.IsPlaying;

                string oldTrackTitle = currentTrackTitle;
                startingPosition = (uint)currentlyPlaying.ProgressMs;
                currentTrackLength = (uint)currentTrack.DurationMs;
                noTrack = false;

                // Make sure no jukebox actions have taken place in the last second before setting any kind of manual spotify state.
                // This prevents situations where the playstate has been changed (e.g. paused) but GetTrackInfo still thinks Spotify is in the old playstate (e.g. playing).
                if ((Time.time > jukeboxActionTimer + 1) && !menuPause && currentlyPlaying.IsPlaying && (!jukeboxIsPlaying || jukeboxIsPaused))
                {
                    manualPlay = true;
                }
                else if ((Time.time > jukeboxActionTimer + 1) && !menuPause && !justStarted && !currentlyPlaying.IsPlaying && jukeboxIsPlaying && !jukeboxIsPaused)
                {
                    manualPause = true;
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

                if (
                    uGUI_SceneLoadingPatcher.loadingDone && 
                    (
                        playingOnStartup || (!menuPause && jukeboxIsPlaying) || 
                        oldTrackTitle != currentTrackTitle || 
                        (manualPlay && (jukeboxIsPaused || !jukeboxIsPlaying))
                    )
                ) jukeboxNeedsUpdating = true;
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
                if (!justStarted) new Log("Refreshed the Spotify session.");
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
