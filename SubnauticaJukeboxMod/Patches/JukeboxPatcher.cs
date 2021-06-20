using HarmonyLib;
using QModManager.Utility;
using SpotifyAPI.Web;

namespace JukeboxSpotify
{
    [HarmonyPatch(typeof(Jukebox))]
    class JukeboxPatcher
    {
        [HarmonyPostfix]
        [HarmonyPatch("GetInfo", typeof(string))]
        public static void GetInfoPostfix(string id, ref Jukebox.TrackInfo __result)
        {
            //Logger.Log(Logger.Level.Info, "id: " + id + " | info.label: " + info.label + " | info.length: " + info.length, null, true);
            //Logger.Log(Logger.Level.Info, "id: " + id, null, true);
            //Logger.Log(Logger.Level.Info, "old result.label: " + __result.label, null, true);
            //Logger.Log(Logger.Level.Info, "old result.length: " + __result.length, null, true);

            __result = new Jukebox.TrackInfo() { label = MainPatcher._currentTrackTitle, length = MainPatcher._currentTrackLength };

            //Logger.Log(Logger.Level.Info, "new result.label: " + __result.label, null, true);
            //Logger.Log(Logger.Level.Info, "new result.length: " + __result.length, null, true);
        }

        [HarmonyPostfix]
        [HarmonyPatch("GetNext")]
        public async static void GetNextPostfix(JukeboxInstance jukebox, bool forward)
        {

            if (forward)
            {
                await Spotify._spotify.Player.SkipNext(new PlayerSkipNextRequest() { DeviceId = Spotify._device.Id });
            }
            else
            {
                await Spotify._spotify.Player.SkipPrevious(new PlayerSkipPreviousRequest() { DeviceId = Spotify._device.Id });
            }

            if (null == MainPatcher._isPlaying || false == MainPatcher._isPlaying)
            {
                await Spotify._spotify.Player.PausePlayback(new PlayerPausePlaybackRequest() { DeviceId = Spotify._device.Id });
            }
        }


        [HarmonyPostfix]
        [HarmonyPatch("OnApplicationQuit")]
        public async static void OnApplicationQuitPostfix()
        {
            MainPatcher._isPlaying = null;
            var playbackRequest = new PlayerPausePlaybackRequest() { DeviceId = Spotify._device.Id };
            await Spotify._spotify.Player.PausePlayback(playbackRequest);
        }

        [HarmonyPostfix]
        [HarmonyPatch("Play")]
        public async static void PlayPostfix()
        {
            Jukebox.volume = 0;

            MainPatcher._isPlaying = true;
            await Spotify._spotify.Player.ResumePlayback(new PlayerResumePlaybackRequest() { DeviceId = Spotify._device.Id });
        }

        [HarmonyPostfix]
        [HarmonyPatch("StopInternal")]
        public async static void StopInternalPostfix()
        {
            MainPatcher._isPlaying = null;
            MainPatcher._isPaused = false;
            await Spotify._spotify.Player.PausePlayback(new PlayerPausePlaybackRequest() { DeviceId = Spotify._device.Id });
            await Spotify._spotify.Player.SeekTo(new PlayerSeekToRequest(0));
        }

        [HarmonyPostfix]
        [HarmonyPatch("UpdateStudio")]
        public static void UpdateStudioPostfix(ref bool ____paused)
        {
            if (____paused && MainPatcher._isPaused == false)
            {
                MainPatcher._isPaused = true;
                Spotify._spotify.Player.PausePlayback(new PlayerPausePlaybackRequest() { DeviceId = Spotify._device.Id });
            }
            else if (!____paused && true == MainPatcher._isPaused && true == MainPatcher._isPlaying)
            {
                MainPatcher._isPaused = false;
                Spotify._spotify.Player.ResumePlayback(new PlayerResumePlaybackRequest() { DeviceId = Spotify._device.Id });
            }
        }

        //private async static Task PlayTrack()
        //{
        //var track = await Spotify._spotify.Tracks.Get("4iV5W9uYEdYUVa79Axb7Rh");

        // Add a song to the device's queue.
        //var newSongRequest = new PlayerAddToQueueRequest("spotify:track:" + track.Id) { DeviceId = availableDevice.Id };

        //await Spotify._spotify.Player.AddToQueue(newSongRequest);

        //try
        //{
        //    CurrentlyPlayingContext currentlyPlaying = await Spotify._spotify.Player.GetCurrentPlayback();
        //    FullTrack currentTrack = (FullTrack)currentlyPlaying.Item;

        //    // Skip tracks until we are at the first one.
        //    // This effectively clears the old queue, which is not currently a feature.
        //    while (track.Id != currentTrack.Id)
        //    {
        //        // Skip the current song if it's not the one we want.
        //        await Spotify._spotify.Player.SkipNext(new PlayerSkipNextRequest() { DeviceId = Spotify._device.Id });

        //        // We need this delay or else the song we want can get skipped.
        //        await Task.Delay(250);

        //        // Update what's currently playing.
        //        currentlyPlaying = await Spotify._spotify.Player.GetCurrentPlayback();
        //        currentTrack = (FullTrack)currentlyPlaying.Item;
        //    }

        //}
        //catch (Exception e)
        //{
        //    new ErrorHandler(e, "Skipping song went wrong");
        //}



        // Play the song.

        //}
    }
}
