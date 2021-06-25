using DebounceThrottle;
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
        private static EmbedIOAuthServer _server = null;
        public static DebounceDispatcher trackDebouncer = new DebounceDispatcher(1000);
        public static ThrottleDispatcher volumeThrottler = new ThrottleDispatcher(250);
        public static bool? isPlaying = null;
        public static bool isPaused = false;
        public static bool repeatTrack = false;
        public static bool playingOnStartup = false;
        public static bool isCurrentlyPlaying = false;
        public static bool justStarted = true;
        public static uint startingPosition = 0;
        public static SpotifyClient client = null;
        public static string refreshToken = null;
        public static Device device = null;
        public static bool jukeboxNeedsUpdating = false;
        public static bool jukeboxNeedsPlaying = false;
        public static string currentTrackTitle = "Spotify Jukebox Mod";
        public static uint currentTrackLength = 0;
        public static float timeTrackStarted = 0;
        public static uint timelinePosition = 0;
        public static int spotifyVolume = 100;
        public static float jukeboxVolume = Jukebox.volume;

        public async static Task SpotifyLogin()
        {
            try
            {
                // Check the database for a stored refresh token
                List<string> result = SQL.ReadData("SELECT * FROM Auth");

                if (null != result && result.Count > 0)
                {
                    refreshToken = result[0];
                }

                if (null != refreshToken)
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

                Logger.Log(Logger.Level.Info, "Spotify successfully loaded", null, true);

            }
            catch (Exception e)
            {
                new ErrorHandler(e, "Something went wrong loading Spotify");
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

                List<string> result = SQL.ReadData("SELECT * FROM Device", "device");

                if (result != null && result.Count > 0)
                {
                    availableDevice = new Device() { Id = result[0], Name = result[1] };
                    Logger.Log(Logger.Level.Info, "Pre-loaded latest device from database in case we need it: " + availableDevice.Name, null, false);
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

                        SQL.QueryTable("INSERT INTO Device (device_id, device_name) VALUES(" +
                            "'" + availableDevice.Id + "'," +
                            "'" + availableDevice.Name + "'" +
                        ")");
                    }
                });

                // If no active device was found, choose the first one in the devices list.
                if (null == availableDevice)
                {
                    Logger.Log(Logger.Level.Info, "No Spotify device found. Please play/pause your spotify client and reload your game.", null, true);

                    return;
                }

                device = availableDevice;

                await Spotify.client.Player.TransferPlayback(new PlayerTransferPlaybackRequest(new List<string>() { device.Id }));
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
                
                if (null == currentlyPlaying || null == currentlyPlaying.Item)
                {
                    Logger.Log(Logger.Level.Info, "No track currently playing", null, false);
                    return;
                }

                var currentTrack = (FullTrack) currentlyPlaying.Item;
                //Logger.Log(Logger.Level.Info, "Current track: " + currentTrack.Name, null, true);

                if (currentTrackTitle == "Spotify Jukebox Mod") playingOnStartup = currentlyPlaying.IsPlaying;
                isCurrentlyPlaying = currentlyPlaying.IsPlaying;
                startingPosition = (uint) currentlyPlaying.ProgressMs;
                currentTrackTitle = currentTrack.Name;
                currentTrackLength = (uint) currentTrack.DurationMs;
                jukeboxNeedsUpdating = true;
            } catch(Exception e)
            {
                new ErrorHandler(e, "Something went wrong getting track info");
            }

            if (plsRepeat) _ = SetInterval(GetTrackInfo, 5000);
        }

        public async static Task RunServer()
        {
            _server = new EmbedIOAuthServer(new Uri("http://localhost:5000/callback"), 5000);
            await _server.Start();

            _server.AuthorizationCodeReceived += OnAuthorizationCodeReceived;
            _server.ErrorReceived += OnErrorReceived;

            var request = new LoginRequest(_server.BaseUri, Variables._clientId, LoginRequest.ResponseType.Code)
            {
                Scope = new[] {
                    Scopes.UserModifyPlaybackState,
                    Scopes.UserReadPlaybackState
                }
            };
            BrowserUtil.Open(request.ToUri());
        }

        private async static Task OnAuthorizationCodeReceived(object sender, AuthorizationCodeResponse response)
        {
            await _server.Stop();
            await SetupSpotifyClient(response.Code, true);
        }

        private async static Task SetupSpotifyClient(string code, bool saveToDB = false)
        {
            // Get the access token and set up the SpotifyClient.
            SpotifyClientConfig config = SpotifyClientConfig.CreateDefault();
            var tokenResponse = await new OAuthClient(config).RequestToken(
              new AuthorizationCodeTokenRequest(
                Variables._clientId, Variables._clientSecret, code, new Uri("http://localhost:5000/callback")
              )
            );


            if (saveToDB) SQL.QueryTable("INSERT INTO Auth (authorization_code, access_token, refresh_token, expires_in) VALUES(" +
                "'" + code + "'," +
                "'" + tokenResponse.AccessToken + "'," +
                "'" + tokenResponse.RefreshToken + "'," +
                tokenResponse.ExpiresIn +
            ")");

            config = SpotifyClientConfig
                .CreateDefault()
                .WithAuthenticator(new AuthorizationCodeAuthenticator(Variables._clientId, Variables._clientSecret, tokenResponse));

            client = new SpotifyClient(config);
        }

        private static async Task OnErrorReceived(object sender, string error, string state)
        {
            Console.WriteLine($"Aborting authorization, error received: {error}");
            await _server.Stop();
        }

        private async static Task RefreshSession(bool plsRepeat = false)
        {
            var newResponse = await new OAuthClient().RequestToken(
              new AuthorizationCodeRefreshRequest(Variables._clientId, Variables._clientSecret, refreshToken)
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
