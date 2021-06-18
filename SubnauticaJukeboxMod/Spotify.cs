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

        public async static Task SpotifyLogin()
        {

            Logger.Log(Logger.Level.Info, "Loading Spotify with Spotify ID: " + Variables._clientId, null, true);

            try
            {
                // Check the database for stored authCodes
                _refreshToken = SQL.ReadData("SELECT * FROM Auth");

                if (null != _refreshToken)
                {
                    try
                    {
                        Logger.Log(Logger.Level.Info, "We have a refresh token! " + _refreshToken, null, true);
                        await RefreshSession();
                    }
                    catch (Exception e)
                    {
                        new ErrorHandler(e, "An error occurred refreshing the session");
                        await RunServer();
                    }
                }
                else
                {
                    await RunServer();
                }

                // This while loop is to give time for the _spotify object to be fully initialised.
                while (null == _spotify)
                {
                    await Task.Delay(1000);
                }

            }
            catch (Exception e)
            {
                new ErrorHandler(e, "Something went wrong");
            }
        }

        public async static Task RunServer()
        {
            Logger.Log(Logger.Level.Info, "Starting up server...", null, true);
            _server = new EmbedIOAuthServer(new Uri("http://localhost:5000/callback"), 5000);
            await _server.Start();

            _server.AuthorizationCodeReceived += OnAuthorizationCodeReceived;
            _server.ErrorReceived += OnErrorReceived;


            Logger.Log(Logger.Level.Info, "Requesting user account access...", null, true);
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


            if (saveToDB) SQL.queryTable("INSERT INTO Auth (authorization_code, access_token, refresh_token, expires_in) VALUES(" +
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

        private async static Task RefreshSession()
        {
            var newResponse = await new OAuthClient().RequestToken(
              new AuthorizationCodeRefreshRequest(Variables._clientId, Variables._clientSecret, _refreshToken)
            );

            _spotify = new SpotifyClient(newResponse.AccessToken);

            var _setRefresh = WaitForNextRefresh(newResponse.ExpiresIn - 50);
        }

        private static async Task WaitForNextRefresh(int timeout = 36000 - 50)
        {
            await Task.Delay(timeout).ConfigureAwait(false);
            var refresh = RefreshSession();
        }
    }
}
