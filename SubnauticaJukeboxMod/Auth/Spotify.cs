using System;
using System.Threading.Tasks;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using QModManager.Utility;

namespace JukeboxSpotify
{
    class Spotify
    {
        public static EmbedIOAuthServer _server = null;
        public static SpotifyClient _spotify = null;
        public static string _refreshToken = null;
        public static Device _device = null;
        public static bool _checkingTrack = false;

        public async static Task SpotifyLogin()
        {

            try
            {
                // Check the database for a stored refresh token
                _refreshToken = SQL.ReadData("SELECT * FROM Auth");

                if (null != _refreshToken)
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

                // This while loop is to give time for the _spotify object to be fully initialised.
                while (null == _spotify)
                {
                    await Task.Delay(1000);
                }

                await GetDevice();

                Logger.Log(Logger.Level.Info, "Spotify successfully loaded ", null, true);

                await GetCurrentTrack();
            }
            catch (Exception e)
            {
                new ErrorHandler(e, "Something went wrong loading Spotify");
            }
        }

        public async static Task GetDevice()
        {
            // Get an available device to play songs with.
            Device availableDevice = null;

            try
            {
                DeviceResponse devices = await _spotify.Player.GetAvailableDevices();
                bool foundActiveDevice = false;
                int counter = 1;

                devices.Devices.ForEach(delegate (Device device)
                {
                    // Find the first active device.
                    if (false == foundActiveDevice && device.IsActive)
                    {
                        availableDevice = device;
                        foundActiveDevice = true;
                    }

                    counter++;
                });

                // If no active device was found, choose the first one in the devices list.
                if (null == availableDevice && devices.Devices.Count > 0) availableDevice = devices.Devices[0];


                _device = availableDevice;
            }
            catch (Exception e)
            {
                new ErrorHandler(e, "Something went wrong getting a device");
            }
        }

        // Update what's currently playing.
        public async static Task GetCurrentTrack(bool plsRepeat = false)
        {
            var currentlyPlaying = await _spotify.Player.GetCurrentPlayback();
            var currentTrack = (FullTrack) currentlyPlaying.Item;

            MainPatcher._currentTrackTitle = currentTrack.Name;
            MainPatcher._currentTrackLength = (uint) currentTrack.DurationMs;

            if (plsRepeat || false == _checkingTrack)
            {
                var repeat = SetInterval(GetCurrentTrack, 10000);
            }
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
                    Scopes.AppRemoteControl,
                    Scopes.PlaylistModifyPrivate,
                    Scopes.PlaylistModifyPublic,
                    Scopes.PlaylistReadCollaborative,
                    Scopes.PlaylistReadPrivate,
                    Scopes.Streaming,
                    Scopes.UgcImageUpload,
                    Scopes.UserFollowModify,
                    Scopes.UserFollowRead,
                    Scopes.UserLibraryModify,
                    Scopes.UserLibraryRead,
                    Scopes.UserModifyPlaybackState,
                    Scopes.UserReadCurrentlyPlaying,
                    Scopes.UserReadEmail,
                    Scopes.UserReadPlaybackPosition,
                    Scopes.UserReadPlaybackState,
                    Scopes.UserReadPrivate,
                    Scopes.UserReadRecentlyPlayed,
                    Scopes.UserTopRead
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

            _spotify = new SpotifyClient(config);
        }

        private static async Task OnErrorReceived(object sender, string error, string state)
        {
            Console.WriteLine($"Aborting authorization, error received: {error}");
            await _server.Stop();
        }

        private async static Task RefreshSession(bool plsRepeat = false)
        {
            var newResponse = await new OAuthClient().RequestToken(
              new AuthorizationCodeRefreshRequest(Variables._clientId, Variables._clientSecret, _refreshToken)
            );

            _spotify = new SpotifyClient(newResponse.AccessToken);

            var repeat = SetInterval(RefreshSession, newResponse.ExpiresIn - 50);
        }

        private static async Task SetInterval(Func<bool, Task> method, int timeout = 36000 - 50)
        {
            await Task.Delay(timeout).ConfigureAwait(false);
            var run = method(true);
        }
    }
}
