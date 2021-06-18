using HarmonyLib;
using QModManager.Utility;
using System;
using System.Threading.Tasks;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;

namespace JukeboxSpotify
{
    [HarmonyPatch(typeof(Jukebox))]
    [HarmonyPatch("Play")]
    internal class JukeboxPatcher
    {
        private static EmbedIOAuthServer _server = null;
        private static SpotifyClient _spotify = null;
        private static string _refreshToken = null;

        [HarmonyPostfix]
        public async static void Postfix()
        {
            Logger.Log(Logger.Level.Info, "Why, Hello there.", null, true);
            Jukebox.volume = 0;

            new SQL();
            await SpotifyLogin();
        }

        private async static Task SpotifyLogin()
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
                    } catch (Exception e)
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
                while(null == _spotify)
                {
                    await Task.Delay(1000);
                }

                var track = await _spotify.Tracks.Get("4iV5W9uYEdYUVa79Axb7Rh");

                Logger.Log(Logger.Level.Info, "We have a song! " + track.Name + ". Getting devices...", null, true);

                // Get an available device to play songs with.
                Device availableDevice = null;

                try
                {
                    DeviceResponse devices = await _spotify.Player.GetAvailableDevices();
                    bool foundActiveDevice = false;
                    int counter = 1;

                    devices.Devices.ForEach(delegate (Device device)
                    {
                        Logger.Log(Logger.Level.Info, "Device found with name: " + device.Name + " and ID: " + device.Id, null, true);

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
                }
                catch (Exception e)
                {
                    new ErrorHandler(e, "Something went wrong getting a device");
                }

                // Add a song to the device's queue.
                var newSongRequest = new PlayerAddToQueueRequest("spotify:track:" + track.Id) { DeviceId = availableDevice.Id };

                await _spotify.Player.AddToQueue(newSongRequest);

                try
                {
                    CurrentlyPlayingContext currentlyPlaying = await _spotify.Player.GetCurrentPlayback();
                    FullTrack currentTrack = (FullTrack)currentlyPlaying.Item;

                    // Skip tracks until we are at the first one.
                    // This effectively clears the old queue, which is not currently a feature.
                    while (track.Id != currentTrack.Id)
                    {
                        // Skip the current song if it's not the one we want.
                        await _spotify.Player.SkipNext();

                        // We need this delay or else the song we want can get skipped.
                        await Task.Delay(250);

                        // Update what's currently playing.
                        currentlyPlaying = await _spotify.Player.GetCurrentPlayback();
                        currentTrack = (FullTrack) currentlyPlaying.Item;
                    }

                }
                catch (Exception e)
                {
                    new ErrorHandler(e, "Skipping song went wrong");
                }

                try
                {
                    var playbackRequest = new PlayerResumePlaybackRequest() { DeviceId = availableDevice.Id };

                    // Play the song.
                    await _spotify.Player.ResumePlayback(playbackRequest);

                }
                catch (Exception e)
                {
                    new ErrorHandler(e, "Error resuming playback (normally plays anyway)");
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
