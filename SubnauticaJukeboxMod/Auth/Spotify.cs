﻿using DebounceThrottle;
using QModManager.API;
using QModManager.Utility;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JukeboxSpotify
{
    class Spotify
    {
        private static EmbedIOAuthServer _server;
        public static DebounceDispatcher trackDebouncer = new DebounceDispatcher(1000);
        public static ThrottleDispatcher volumeThrottler = new ThrottleDispatcher(250);
        public static bool? jukeboxIsPlaying;
        public static bool jukeboxIsPaused = false;
        public static bool spotifyIsPlaying = false;
        public static bool repeatTrack = false;
        public static bool playingOnStartup = false;
        public static bool justStarted = true;
        public static uint startingPosition = 0;
        public static SpotifyClient client;
        public static Device device;
        public static bool jukeboxNeedsUpdating = false;
        public static bool jukeboxNeedsPlaying = false;
        public static string currentTrackTitle = "Spotify Jukebox Mod (If nothing plays, play/pause Spotify and try again)";
        public static uint currentTrackLength = 0;
        public static float timeTrackStarted = 0;
        public static uint timelinePosition = 0;
        public static int spotifyVolume = 100;
        public static float jukeboxVolume = Jukebox.volume;

        public async static Task SpotifyLogin()
        {
            try
            {
                // Check the config for a stored refresh token

                if (null == MainPatcher.Config.clientId || null == MainPatcher.Config.clientSecret)
                {
                    QModServices.Main.AddCriticalMessage("Please add your Spotify client id and secret to your config.json file, then reload your save. Instructions are on the Nexus mod page.");
                    return;
                }

                if (null != MainPatcher.Config.refreshToken)
                {
                    try
                    {
                        await RefreshSession();
                    }
                    catch (Exception e)
                    {
                        new ErrorHandler(e, "An error occurred refreshing the session");
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

                await Retry.Do(() => GetTrackInfo(true), TimeSpan.FromSeconds(1));

                Logger.Log(Logger.Level.Info, "Spotify successfully loaded", null, false);

            }
            catch (Exception e)
            {
                Logger.Log(Logger.Level.Warn, "Spotify successfully loaded", e, false);
            }
        }

        public async static Task GetDevice()
        {
            // Get an available device to play tracks with.
            Device availableDevice = null;

            try
            {
                DeviceResponse devices = await client.Player.GetAvailableDevices();
                bool foundActiveDevice = false;

                if (null != MainPatcher.Config.deviceId)
                {
                    availableDevice = new Device() { Id = MainPatcher.Config.deviceId };
                    Logger.Log(Logger.Level.Info, "Pre-loaded latest device from database in case we need it: " + availableDevice.Id, null, false);
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
                if (null == availableDevice)
                {
                    Logger.Log(Logger.Level.Info, "No Spotify device found. Please play/pause your spotify client and reload your game.", null, false);

                    return;
                }

                device = availableDevice;
            }
            catch (Exception e)
            {
                new ErrorHandler(e, "Something went wrong getting a device");
            }
        }

        // Update what's currently playing.
        public async static Task GetTrackInfo(bool plsRepeat = false)
        {
            try
            {
                var currentlyPlaying = await client.Player.GetCurrentPlayback();
                
                if (null == currentlyPlaying)
                {
                    Logger.Log(Logger.Level.Info, "Playback not found", null, false);
                    await client.Player.TransferPlayback(new PlayerTransferPlaybackRequest(new List<string>() { device.Id }));
                }

                var currentTrack = (FullTrack) currentlyPlaying.Item;

                if (uGUI_SceneLoadingPatcher.loadingDone && null != Jukebox.main._instance)
                {
                    // This crashes the game. i cri, erry day i cri
                    //while (
                    //    (currentlyPlaying.RepeatState == "context" && Jukebox.repeat != Jukebox.Repeat.All) ||
                    //    (currentlyPlaying.RepeatState == "track" && Jukebox.repeat != Jukebox.Repeat.Track) ||
                    //    (currentlyPlaying.RepeatState == "off" && Jukebox.repeat != Jukebox.Repeat.Off)
                    //)
                    //{
                    //    Logger.Log(Logger.Level.Info, "Hitting the repeat button. currentlyPlaying.RepeatState: ", null, true);
                    //    Jukebox.main._instance.OnButtonRepeat();
                    //    await Task.Delay(500);
                    //}

                    if (Jukebox.shuffle != currentlyPlaying.ShuffleState) Jukebox.main._instance.OnButtonShuffle();
                }

                if (currentTrackTitle == "Spotify Jukebox Mod") playingOnStartup = currentlyPlaying.IsPlaying;
                spotifyIsPlaying = currentlyPlaying.IsPlaying;
                startingPosition = (uint) currentlyPlaying.ProgressMs;
                currentTrackTitle = currentTrack.Name;
                currentTrackLength = (uint) currentTrack.DurationMs;
                jukeboxNeedsUpdating = true;
            } catch(Exception e)
            {
                Logger.Log(Logger.Level.Error, "Something went wrong getting track info.", e, false);
                await GetDevice();
            }

            if (plsRepeat) _ = SetInterval(GetTrackInfo, 5000);
        }

        public async static Task RunServer()
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

        private async static Task OnAuthorizationCodeReceived(object sender, AuthorizationCodeResponse response)
        {
            await _server.Stop();
            await SetupSpotifyClient(response.Code, true);
        }

        private async static Task SetupSpotifyClient(string code, bool saveToConfig = false)
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

        private static async Task OnErrorReceived(object sender, string error, string state)
        {
            await _server.Stop();
        }

        private async static Task RefreshSession(bool plsRepeat = false)
        {
            var newResponse = await new OAuthClient().RequestToken(
              new AuthorizationCodeRefreshRequest(MainPatcher.Config.clientId, MainPatcher.Config.clientSecret, MainPatcher.Config.refreshToken)
            );

            client = new SpotifyClient(newResponse.AccessToken);
            var repeat = SetInterval(RefreshSession, newResponse.ExpiresIn - 50);
        }

        private static async Task SetInterval(Func<bool, Task> method, int timeout = 36000 - 50)
        {
            await Task.Delay(timeout).ConfigureAwait(false);
            var run = method(true);
        }
    }
}
