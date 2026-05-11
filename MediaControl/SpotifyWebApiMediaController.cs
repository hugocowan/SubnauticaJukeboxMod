using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace JukeboxSpotify
{
    internal sealed class SpotifyWebApiMediaController : IMediaController
    {
        private SpotifyClient _client;
        private EmbedIOAuthServer _server;
        private float _refreshSessionTimer;
        private float _refreshSessionExpiryTime = 3600f;

        public string ControllerName => "Spotify Web API";

        public bool IsReady => _client != null;

        public bool SupportsSourceVolume => false;

        public async Task InitializeAsync()
        {
            try
            {
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

                if (null != Plugin.config.refreshToken)
                {
                    try
                    {
                        await RefreshSessionAsync();
                    }
                    catch (Exception e)
                    {
                        if (Plugin.config.logging)
                        {
                            Plugin.Logger.LogError("An error occurred refreshing the session : " + e);
                        }

                        await RunServerAsync();
                    }
                }
                else
                {
                    await RunServerAsync();
                }

                while (null == _client)
                {
                    await Task.Delay(1000);
                }

                await EnsureDeviceAsync();

                if (Plugin.config.logging)
                {
                    Plugin.Logger.LogInfo("Spotify successfully loaded through the media controller");
                }
            }
            catch (Exception e)
            {
                if (Plugin.config.logging)
                {
                    Plugin.Logger.LogError("Something went wrong loading Spotify : " + e);
                }
            }
        }

        public async Task<MediaPlaybackSnapshot> GetPlaybackSnapshotAsync()
        {
            try
            {
                await EnsureFreshSessionAsync();

                if (null == _client)
                {
                    return MediaPlaybackSnapshot.Empty;
                }

                var currentlyPlaying = await _client.Player.GetCurrentPlayback();

                if (null == currentlyPlaying || null == currentlyPlaying.Item)
                {
                    if (Plugin.config.deviceId == null)
                    {
                        await EnsureDeviceAsync();
                    }

                    if (Plugin.config.deviceId != null)
                    {
                        await _client.Player.TransferPlayback(new PlayerTransferPlaybackRequest(new List<string>() { Plugin.config.deviceId }));
                    }

                    return new MediaPlaybackSnapshot
                    {
                        HasTrack = false,
                        StatusMessage = "Spotify Jukebox Mod - If nothing plays, play/pause your Spotify app then try again.",
                    };
                }

                if (currentlyPlaying.Item is not FullTrack currentTrack)
                {
                    return new MediaPlaybackSnapshot
                    {
                        HasTrack = false,
                        StatusMessage = "The current Spotify item is not a music track.",
                    };
                }

                string artists = string.Empty;
                foreach (SimpleArtist artist in currentTrack.Artists)
                {
                    artists += artists == string.Empty ? artist.Name : ", " + artist.Name;
                }

                return new MediaPlaybackSnapshot
                {
                    HasTrack = true,
                    TrackTitle = currentTrack.Name,
                    Artist = artists,
                    Album = currentTrack.Album?.Name ?? string.Empty,
                    DurationMs = (uint)currentTrack.DurationMs,
                    PositionMs = (uint)currentlyPlaying.ProgressMs,
                    PlaybackState = currentlyPlaying.IsPlaying ? MediaPlaybackState.Playing : MediaPlaybackState.Paused,
                    ShuffleEnabled = currentlyPlaying.ShuffleState,
                    SourceId = Plugin.config.deviceId ?? string.Empty,
                };
            }
            catch (Exception e)
            {
                if (Plugin.config.logging)
                {
                    Plugin.Logger.LogError("Something went wrong getting track info : " + e);
                }

                return new MediaPlaybackSnapshot
                {
                    HasTrack = false,
                    StatusMessage = "Spotify Jukebox Mod - If nothing plays, play/pause your Spotify app then try again.",
                };
            }
        }

        public Task<IAudioCaptureSession> CreateAudioCaptureSessionAsync()
        {
            return Task.FromResult<IAudioCaptureSession>(null);
        }

        public async Task PlayAsync()
        {
            if (await EnsureReadyAsync())
            {
                await _client.Player.ResumePlayback(new PlayerResumePlaybackRequest() { DeviceId = Plugin.config.deviceId });
            }
        }

        public async Task PauseAsync()
        {
            if (await EnsureReadyAsync())
            {
                await _client.Player.PausePlayback(new PlayerPausePlaybackRequest() { DeviceId = Plugin.config.deviceId });
            }
        }

        public async Task NextAsync()
        {
            if (await EnsureReadyAsync())
            {
                await _client.Player.SkipNext(new PlayerSkipNextRequest() { DeviceId = Plugin.config.deviceId });
            }
        }

        public async Task PreviousAsync()
        {
            if (await EnsureReadyAsync())
            {
                await _client.Player.SkipPrevious(new PlayerSkipPreviousRequest() { DeviceId = Plugin.config.deviceId });
            }
        }

        public async Task SeekAsync(long positionMs)
        {
            if (await EnsureReadyAsync())
            {
                await _client.Player.SeekTo(new PlayerSeekToRequest(positionMs) { DeviceId = Plugin.config.deviceId });
            }
        }

        public Task SetSourceVolumeAsync(int volumePercent)
        {
            if (Plugin.config.logging)
            {
                Plugin.Logger.LogInfo("Ignoring source-volume change because jukebox volume is game-owned.");
            }

            return Task.CompletedTask;
        }

        public async Task SetShuffleAsync(bool enabled)
        {
            if (await EnsureReadyAsync())
            {
                await _client.Player.SetShuffle(new PlayerShuffleRequest(enabled));
            }
        }

        public async Task SetRepeatModeAsync(MediaRepeatMode repeatMode)
        {
            if (!await EnsureReadyAsync())
            {
                return;
            }

            PlayerSetRepeatRequest.State state = repeatMode switch
            {
                MediaRepeatMode.Track => PlayerSetRepeatRequest.State.Track,
                MediaRepeatMode.Context => PlayerSetRepeatRequest.State.Context,
                _ => PlayerSetRepeatRequest.State.Off,
            };

            await _client.Player.SetRepeat(new PlayerSetRepeatRequest(state));
        }

        public async Task ShutdownAsync()
        {
            if (_server != null)
            {
                await _server.Stop();
                _server = null;
            }
        }

        private async Task<bool> EnsureReadyAsync()
        {
            await EnsureFreshSessionAsync();
            return _client != null;
        }

        private async Task EnsureFreshSessionAsync()
        {
            if (_client == null)
            {
                return;
            }

            if (_refreshSessionTimer != 0 && Time.time > (_refreshSessionTimer + _refreshSessionExpiryTime - 2))
            {
                await RefreshSessionAsync();
            }
        }

        private async Task EnsureDeviceAsync()
        {
            try
            {
                if (_client == null)
                {
                    return;
                }

                Device availableDevice = null;
                DeviceResponse devices = await _client.Player.GetAvailableDevices();
                bool foundActiveDevice = false;

                if (null != Plugin.config.deviceId)
                {
                    availableDevice = new Device() { Id = Plugin.config.deviceId };
                }

                devices.Devices.ForEach(delegate (Device device)
                {
                    if (!foundActiveDevice && device.IsActive && (null == availableDevice || availableDevice.Id != device.Id))
                    {
                        availableDevice = device;
                        foundActiveDevice = true;

                        Plugin.config.deviceId = device.Id;
                        Plugin.config.Save();
                    }
                });

                if (null == availableDevice && devices.Devices.Count > 0)
                {
                    if (Plugin.config.logging)
                    {
                        Plugin.Logger.LogInfo("Spotify device found");
                    }

                    Plugin.config.deviceId = devices.Devices[0].Id;
                    Plugin.config.Save();
                }
                else if (null == availableDevice)
                {
                    if (Plugin.config.logging)
                    {
                        Plugin.Logger.LogInfo("No Spotify device found. Please play/pause your Spotify app.");
                    }

                    Vars.currentTrackTitle = "No Spotify device found. Please play/pause your Spotify app then try again.";
                }
            }
            catch (Exception e)
            {
                if (Plugin.config.logging)
                {
                    Plugin.Logger.LogError("Something went wrong getting a device : " + e);
                }
            }
        }

        private async Task RunServerAsync()
        {
            try
            {
                _server = new EmbedIOAuthServer(new Uri("http://localhost:5000/callback"), 5000);
                await _server.Start();
                _server.AuthorizationCodeReceived += OnAuthorizationCodeReceived;
                _server.ErrorReceived += OnErrorReceived;

                var request = new LoginRequest(_server.BaseUri, Plugin.config.clientId, LoginRequest.ResponseType.Code)
                {
                    Scope = new[] { Scopes.UserModifyPlaybackState, Scopes.UserReadPlaybackState, Scopes.UserReadEmail }
                };
                BrowserUtil.Open(request.ToUri());
            }
            catch (Exception e)
            {
                if (Plugin.config.logging)
                {
                    Plugin.Logger.LogError("Something went wrong running the server : " + e);
                }
            }
        }

        private async Task OnAuthorizationCodeReceived(object sender, AuthorizationCodeResponse response)
        {
            try
            {
                await _server.Stop();
                if (Plugin.config.logging)
                {
                    Plugin.Logger.LogMessage("We have the code: " + response.Code);
                }

                await SetupSpotifyClientAsync(response.Code);
            }
            catch (Exception e)
            {
                if (Plugin.config.logging)
                {
                    Plugin.Logger.LogError("Something went wrong receiving the Authorization code : " + e);
                }
            }
        }

        private async Task SetupSpotifyClientAsync(string code)
        {
            try
            {
                SpotifyClientConfig config = SpotifyClientConfig.CreateDefault();
                if (Plugin.config.logging)
                {
                    Plugin.Logger.LogMessage("SpotifyClientConfig created");
                }

                var tokenResponse = await new OAuthClient(config).RequestToken(
                    new AuthorizationCodeTokenRequest(
                        Plugin.config.clientId,
                        Plugin.config.clientSecret,
                        code,
                        new Uri("http://localhost:5000/callback")
                    )
                );
                if (Plugin.config.logging)
                {
                    Plugin.Logger.LogMessage("Token request completed");
                }

                Plugin.config.refreshToken = tokenResponse.RefreshToken;
                Plugin.config.Save();
                if (Plugin.config.logging)
                {
                    Plugin.Logger.LogMessage("Config saved");
                }

                config = SpotifyClientConfig
                    .CreateDefault()
                    .WithAuthenticator(new AuthorizationCodeAuthenticator(Plugin.config.clientId, Plugin.config.clientSecret, tokenResponse));
                _client = new SpotifyClient(config);

                if (Plugin.config.logging)
                {
                    Plugin.Logger.LogMessage("SetupSpotifyClient completed successfully");
                }
            }
            catch (Exception e)
            {
                if (Plugin.config.logging)
                {
                    Plugin.Logger.LogError("Something went wrong setting up the Spotify client : " + e);
                }
            }
        }

        private async Task OnErrorReceived(object sender, string error, string state)
        {
            try
            {
                if (Plugin.config.logging)
                {
                    Plugin.Logger.LogError("Server failed: " + error);
                }

                await _server.Stop();
            }
            catch (Exception e)
            {
                if (Plugin.config.logging)
                {
                    Plugin.Logger.LogError("Something went wrong while stopping the server after an error : " + e);
                }
            }
        }

        private async Task RefreshSessionAsync()
        {
            var newResponse = await new OAuthClient().RequestToken(
                new AuthorizationCodeRefreshRequest(Plugin.config.clientId, Plugin.config.clientSecret, Plugin.config.refreshToken)
            );

            _client = new SpotifyClient(newResponse.AccessToken);
            if (!Vars.justStarted && Plugin.config.logging)
            {
                Plugin.Logger.LogInfo("Refreshed the Spotify session.");
            }

            _refreshSessionExpiryTime = newResponse.ExpiresIn;
            _refreshSessionTimer = Time.time;
        }
    }
}