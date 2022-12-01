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
        public async static Task SpotifyLogin()
        {
            new Log("we are here woop");
            try
            {
                // Check the config for the clientId and clientSecret
                if (null == MainPatcher.Config.clientId || null == MainPatcher.Config.clientSecret || "paste_client_id_here" == MainPatcher.Config.clientId || "paste_client_secret_here" == MainPatcher.Config.clientSecret)
                {
                    Vars.currentTrackTitle = "Follow instructions on the Nexus mod page to add your Spotify client id and secret to your config.json file, then reload your save.";
                    MainPatcher.Config.clientId = "paste_client_id_here";
                    MainPatcher.Config.clientSecret = "paste_client_secret_here";
                    MainPatcher.Config.Save();
                    QModServices.Main.AddCriticalMessage("Please add your Spotify client id and secret to your config.json file, then reload your save. Instructions are on the Nexus mod page.");
                    return;
                }

                Vars.justStarted = true;

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
                    new Log("No refresh token, let's get credentials");
                    await RunServer();
                }

                // This while loop is to give time for the client object to be fully initialised.
                while (null == Vars.spotify)
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
                DeviceResponse devices = await Vars.spotify.Player.GetAvailableDevices();
                bool foundActiveDevice = false;

                new Log("devices count: " + devices.Devices.Count);

                if (null != MainPatcher.Config.deviceId)
                {
                    availableDevice = new Device() { Id = MainPatcher.Config.deviceId };
                    //new Log("Pre-loaded latest device from config.json in case we need it: " + availableDevice.Id, null);
                }

                devices.Devices.ForEach(delegate (Device device)
                {
                    // Find the first active device.
                    new Log("device: " + device.Id);
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
                    Vars.currentTrackTitle = "No Spotify device found. Please play/pause your Spotify app then try again.";
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
                var currentlyPlaying = await Vars.spotify.Player.GetCurrentPlayback();

                if (null == currentlyPlaying || null == currentlyPlaying.Item)
                {
                    Vars.noTrack = true;
                    if (MainPatcher.Config.deviceId == null) await GetDevice();
                    await Vars.spotify.Player.TransferPlayback(new PlayerTransferPlaybackRequest(new List<string>() { MainPatcher.Config.deviceId }));
                    new Error("Playback not found");
                    Vars.currentTrackTitle = "Spotify Jukebox Mod - If nothing plays, play/pause your Spotify app then try again.";
                    return;
                }

                var currentTrack = (FullTrack) currentlyPlaying.Item;

                if (uGUI_SceneLoadingPatcher.loadingDone && null != Jukebox.main._instance)
                {
                    Vars.spotifyShuffleState = currentlyPlaying.ShuffleState;
                    if (Jukebox.shuffle != currentlyPlaying.ShuffleState) Jukebox.main._instance.OnButtonShuffle();
                }

                if (Vars.justStarted) Vars.playingOnStartup = currentlyPlaying.IsPlaying;

                string oldTrackTitle = Vars.currentTrackTitle;
                Vars.startingPosition = (uint)currentlyPlaying.ProgressMs;
                Vars.currentTrackLength = (uint)currentTrack.DurationMs;
                Vars.noTrack = false;

                // Make sure no jukebox actions have taken place in the last 5 seconds before setting any kind of manual spotify state.
                // This prevents situations where the playstate has been changed (e.g. paused) but GetTrackInfo still thinks Spotify is in the old playstate (e.g. playing).
                if ((Time.time > Vars.jukeboxActionTimer + 5) && !Vars.menuPause && currentlyPlaying.IsPlaying && (!Vars.jukeboxIsRunning || Vars.jukeboxIsPaused))
                {
                    Vars.manualPlay = true;
                }
                else if ((Time.time > Vars.jukeboxActionTimer + 5) && !Vars.menuPause && !Vars.justStarted && !currentlyPlaying.IsPlaying && Vars.jukeboxIsRunning && !Vars.jukeboxIsPaused)
                {
                    Vars.manualPause = true;
                }

                if (MainPatcher.Config.includeArtist)
                {
                    string artists = "";
                    foreach (SimpleArtist artist in currentTrack.Artists)
                    {
                        artists += (artists == "") ? artist.Name : ", " + artist.Name;
                    }

                    Vars.currentTrackTitle = (MainPatcher.Config.includeArtist) ? currentTrack.Name + " - " + artists : currentTrack.Name;
                }
                else
                {
                    Vars.currentTrackTitle = currentTrack.Name;
                }

                if (
                    uGUI_SceneLoadingPatcher.loadingDone && 
                    (
                        Vars.playingOnStartup || (!Vars.menuPause && Vars.jukeboxIsRunning) || 
                        oldTrackTitle != Vars.currentTrackTitle || 
                        (Vars.manualPlay && (Vars.jukeboxIsPaused || !Vars.jukeboxIsRunning))
                    )
                )
                    Vars.jukeboxNeedsUpdating = true;
            }
            catch (Exception e)
            {
                new Error("Something went wrong getting track info", e);
                Vars.currentTrackTitle = "Spotify Jukebox Mod - If nothing plays, play/pause your Spotify app then try again.";
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
                Vars._server = new EmbedIOAuthServer(new Uri("http://localhost:5000/callback"), 5000);
                await Vars._server.Start();
                Vars._server.AuthorizationCodeReceived += OnAuthorizationCodeReceived;
                Vars._server.ErrorReceived += OnErrorReceived;

                var request = new LoginRequest(Vars._server.BaseUri, MainPatcher.Config.clientId, LoginRequest.ResponseType.Code)
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

        private static async Task OnAuthorizationCodeReceived(object sender, AuthorizationCodeResponse response)
        {
            await Vars._server.Stop();

            var config = SpotifyClientConfig.CreateDefault();
            var tokenResponse = await new OAuthClient(config).RequestToken(
              new AuthorizationCodeTokenRequest(
                MainPatcher.Config.clientId, MainPatcher.Config.clientSecret, response.Code, new Uri("http://localhost:5000/callback")
              )
            );

            Vars.spotify = new SpotifyClient(tokenResponse.AccessToken);
            MainPatcher.Config.refreshToken = tokenResponse.RefreshToken;
            MainPatcher.Config.Save();
        }

        private static async Task OnErrorReceived(object sender, string error, string state)
        {
            Console.WriteLine($"Aborting authorization, error received: {error}");
            await Vars._server.Stop();
        }

        public async static Task RefreshSession()
        {
            try
            {
                var newResponse = await new OAuthClient().RequestToken(
                 new AuthorizationCodeRefreshRequest(MainPatcher.Config.clientId, MainPatcher.Config.clientSecret, MainPatcher.Config.refreshToken)
               );
                new Log("AccessToken: " + newResponse.AccessToken);
                new Log("TokenType: " + newResponse.TokenType);
                new Log("RefreshToken: " + newResponse.RefreshToken);

                if (string.IsNullOrEmpty(newResponse.RefreshToken))
                {
                    new Log("RefreshToken is empty D:");
                }

                Vars.spotify = new SpotifyClient(newResponse.AccessToken);
                if (!Vars.justStarted) new Log("Refreshed the Spotify session.");
                Vars.refreshSessionExpiryTime = newResponse.ExpiresIn;
                Vars.refreshSessionTimer = Time.time;
                MainPatcher.Config.refreshToken = newResponse.RefreshToken;
                MainPatcher.Config.Save();
            }
            catch (Exception e)
            {
                new Error("Something went wrong refreshing the Spotify session", e);
            }
        }
    }
}
