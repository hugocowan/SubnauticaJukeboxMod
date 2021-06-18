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
    class JukeboxPlayPatcher
    {

        [HarmonyPostfix]
        public async static void Postfix()
        {
            Logger.Log(Logger.Level.Info, "Why, Hello there.", null, true);
            Jukebox.volume = 0;
            
            await PlayTrack();
        }

        private async static Task PlayTrack()
        {
            var track = await Spotify._spotify.Tracks.Get("4iV5W9uYEdYUVa79Axb7Rh");

            Logger.Log(Logger.Level.Info, "We have a song! " + track.Name + ". Getting devices...", null, true);

            // Get an available device to play songs with.
            Device availableDevice = null;

            try
            {
                DeviceResponse devices = await Spotify._spotify.Player.GetAvailableDevices();
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

            await Spotify._spotify.Player.AddToQueue(newSongRequest);

            try
            {
                CurrentlyPlayingContext currentlyPlaying = await Spotify._spotify.Player.GetCurrentPlayback();
                FullTrack currentTrack = (FullTrack)currentlyPlaying.Item;

                // Skip tracks until we are at the first one.
                // This effectively clears the old queue, which is not currently a feature.
                while (track.Id != currentTrack.Id)
                {
                    // Skip the current song if it's not the one we want.
                    await Spotify._spotify.Player.SkipNext();

                    // We need this delay or else the song we want can get skipped.
                    await Task.Delay(250);

                    // Update what's currently playing.
                    currentlyPlaying = await Spotify._spotify.Player.GetCurrentPlayback();
                    currentTrack = (FullTrack)currentlyPlaying.Item;
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
                await Spotify._spotify.Player.ResumePlayback(playbackRequest);

            }
            catch (Exception e)
            {
                //new ErrorHandler(e, "Error resuming playback (normally plays anyway)");
            }
        }
    }
}
