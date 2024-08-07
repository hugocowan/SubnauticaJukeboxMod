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
            Vars.spotifyLoginStarted = true;
            try
            {
                // Check the config for the clientId and clientSecret
                if (null == Plugin.config.clientId || null == Plugin.config.clientSecret || "paste_client_id_here" == Plugin.config.clientId || "paste_client_secret_here" == Plugin.config.clientSecret)
                {
                    Vars.currentTrackTitle = "Follow instructions on the Nexus mod page to add your Spotify client id and secret to your config.json file, then reload your save.";
                    Plugin.config.clientId = "paste_client_id_here";
                    Plugin.config.clientSecret = "paste_client_secret_here";
                    Plugin.config.Save();
                    ErrorMessage.AddMessage("Please add your Spotify client id and secret to your config.json file, then reload your save. Instructions are on the Nexus mod page.");
                    return;
                }

                Vars.justStarted = true;

                // Check the config for a stored refresh token
                if (null != Plugin.config.refreshToken)
                {
                    try
                    {
                        await RefreshSession();
                    }
                    catch (Exception e)
                    {
                        if (Plugin.config.logging) Plugin.Logger.LogError("An error occurred refreshing the session : " + e);
                        await RunServer();
                    }
                } // If there wasn't a refresh token, we need to get one
                else
                {
                    await RunServer();
                }

                // This while loop is to give time for the client object to be fully initialised.
                while (null == Vars.client)
                {
                    await Task.Delay(1000);
                }

                await GetDevice();

                await GetTrackInfo();

                if (Plugin.config.logging) Plugin.Logger.LogInfo("Spotify successfully loaded");
            }
            catch (Exception e)
            {
                if (Plugin.config.logging) Plugin.Logger.LogError("Something went wrong loading Spotify : " + e);
            }
        }

        public async static Task GetDevice()
        {
            try
            {
                // Get an available device to play tracks with.
                Device availableDevice = null;
                DeviceResponse devices = await Vars.client.Player.GetAvailableDevices();
                bool foundActiveDevice = false;

                //if (Plugin.config.logging) Plugin.Logger.LogInfo("devices count: " + devices.Devices.Count);

                if (null != Plugin.config.deviceId)
                {
                    availableDevice = new Device() { Id = Plugin.config.deviceId };
                    //if (Plugin.config.logging) Plugin.Logger.LogInfo("Pre-loaded latest device from config.json in case we need it: " + availableDevice.Id);
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

                        Plugin.config.deviceId = device.Id;
                        Plugin.config.Save();
                    }
                });

                // If no active device was found, choose the first one in the devices list.
                if (null == availableDevice && devices.Devices.Count > 0)
                {
                    if (Plugin.config.logging) Plugin.Logger.LogInfo("Spotify device found");
                    Plugin.config.deviceId = devices.Devices[0].Id;
                    Plugin.config.Save();
                }
                else if (null == availableDevice)
                {
                    if (Plugin.config.logging) Plugin.Logger.LogInfo("No Spotify device found. Please play/pause your Spotify app.");
                    Vars.currentTrackTitle = "No Spotify device found. Please play/pause your Spotify app then try again.";
                    return;
                }
            }
            catch (Exception e)
            {
                if (Plugin.config.logging) Plugin.Logger.LogError("Something went wrong getting a device : " + e);
            }
        }

        // Update what's currently playing.
        public async static Task GetTrackInfo()
        {
            try
            {
                var currentlyPlaying = await Vars.client.Player.GetCurrentPlayback();

                if (null == currentlyPlaying || null == currentlyPlaying.Item)
                {
                    Vars.noTrack = true;
                    if (Plugin.config.deviceId == null) await GetDevice();
                    await Vars.client.Player.TransferPlayback(new PlayerTransferPlaybackRequest(new List<string>() { Plugin.config.deviceId }));
                    if (Plugin.config.logging) Plugin.Logger.LogError("Playback not found");
                    Vars.currentTrackTitle = "Spotify Jukebox Mod - If nothing plays, play/pause your Spotify app then try again.";
                    return;
                }

                var currentTrack = (FullTrack)currentlyPlaying.Item;

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

                if (Plugin.config.logging) Plugin.Logger.LogInfo("currentlyPlaying: " + currentlyPlaying.IsPlaying);

                // Make sure no jukebox actions have taken place in the last second before setting any kind of manual spotify state.
                // This prevents situations where the playstate has been changed (e.g. paused) but GetTrackInfo still thinks Spotify is in the old playstate (e.g. playing).
                if ((Time.time > Vars.jukeboxActionTimestamp + 3) && !Vars.menuPause && currentlyPlaying.IsPlaying && (!Vars.jukeboxIsRunning || Vars.jukeboxIsPaused))
                {
                    Vars.manualPlay = true;
                }
                else if ((Time.time > Vars.jukeboxActionTimestamp + 3) && !Vars.menuPause && !Vars.justStarted && !currentlyPlaying.IsPlaying && Vars.jukeboxIsRunning && !Vars.jukeboxIsPaused)
                {
                    Vars.manualPause = true;
                }

                if (Plugin.config.includeArtist)
                {
                    string artists = "";
                    foreach (SimpleArtist artist in currentTrack.Artists)
                    {
                        artists += (artists == "") ? artist.Name : ", " + artist.Name;
                    }

                    Vars.currentTrackTitle = (Plugin.config.includeArtist) ? currentTrack.Name + " - " + artists : currentTrack.Name;
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
                if (Plugin.config.logging) Plugin.Logger.LogError("Something went wrong getting track info : " + e);
                Vars.currentTrackTitle = "Spotify Jukebox Mod - If nothing plays, play/pause your Spotify app then try again.";
                if (Plugin.config.deviceId == null) await GetDevice();
                if (e.Message == "The access token expired")
                {
                    if (Plugin.config.logging) Plugin.Logger.LogInfo("Refreshing session in attempt to get a new access token");
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

                var request = new LoginRequest(Vars._server.BaseUri, Plugin.config.clientId, LoginRequest.ResponseType.Code)
                {
                    Scope = new[] { Scopes.UserModifyPlaybackState, Scopes.UserReadPlaybackState, Scopes.UserReadEmail }
                };
                BrowserUtil.Open(request.ToUri());
            }
            catch (Exception e)
            {
                if (Plugin.config.logging) Plugin.Logger.LogError("Something went wrong running the server : " + e);
            }
        }

        private async static Task OnAuthorizationCodeReceived(object sender, AuthorizationCodeResponse response)
        {
            try
            {
                await Vars._server.Stop();
                if (Plugin.config.logging) Plugin.Logger.LogMessage("We have the code: " + response.Code);
                await SetupSpotifyClient(response.Code);
            }
            catch (Exception e)
            {
                if (Plugin.config.logging) Plugin.Logger.LogError("Something went wrong receiving the Authorization code : " + e);
            }
        }

        private async static Task SetupSpotifyClient(string code)
        {
            try
            {
                SpotifyClientConfig config = SpotifyClientConfig.CreateDefault();
                if (Plugin.config.logging) Plugin.Logger.LogMessage("SpotifyClientConfig created");

                var tokenResponse = await new OAuthClient(config).RequestToken(
                  new AuthorizationCodeTokenRequest(
                    Plugin.config.clientId, Plugin.config.clientSecret, code, new Uri("http://localhost:5000/callback")
                  )
                );
                if (Plugin.config.logging) Plugin.Logger.LogMessage("Token request completed");

                Plugin.config.refreshToken = tokenResponse.RefreshToken;
                Plugin.config.Save();
                if (Plugin.config.logging) Plugin.Logger.LogMessage("Config saved");

                config = SpotifyClientConfig
                    .CreateDefault()
                    .WithAuthenticator(new AuthorizationCodeAuthenticator(Plugin.config.clientId, Plugin.config.clientSecret, tokenResponse));
                Vars.client = new SpotifyClient(config);

                if (Plugin.config.logging) Plugin.Logger.LogMessage("SetupSpotifyClient completed successfully");
            }
            catch (Exception e)
            {
                if (Plugin.config.logging) Plugin.Logger.LogError("Something went wrong setting up the Spotify client : " + e);
            }
        }

        private static async Task OnErrorReceived(object sender, string error, string state)
        {
            try
            {
                if (Plugin.config.logging) Plugin.Logger.LogError("Server failed: " + error);
                await Vars._server.Stop();
            }
            catch (Exception e)
            {
                if (Plugin.config.logging) Plugin.Logger.LogError("Something went wrong while stopping the server after an error : " + e);
            }
        }

        public async static Task RefreshSession()
        {
            try
            {
                var newResponse = await new OAuthClient().RequestToken(
                  new AuthorizationCodeRefreshRequest(Plugin.config.clientId, Plugin.config.clientSecret, Plugin.config.refreshToken)
                );
                Vars.client = new SpotifyClient(newResponse.AccessToken);
                if (!Vars.justStarted) if (Plugin.config.logging) Plugin.Logger.LogInfo("Refreshed the Spotify session.");
                Vars.refreshSessionExpiryTime = newResponse.ExpiresIn;
                Vars.refreshSessionTimer = Time.time;
            }
            catch (Exception e)
            {
                if (Plugin.config.logging) Plugin.Logger.LogError("Something went wrong refreshing the Spotify session : " + e);
            }
        }
    }
}
