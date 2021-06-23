using HarmonyLib;
using SpotifyAPI.Web;
using System.Collections.Generic;
using UnityEngine;

namespace JukeboxSpotify
{
    [HarmonyPatch(typeof(Jukebox))]
    class JukeboxPatcher
    {
        private static AccessTools.FieldRef<Jukebox, List<string>> _playlistRef = AccessTools.FieldRefAccess<Jukebox, List<string>>("_playlist");
        private static AccessTools.FieldRef<Jukebox, string> _fileRef = AccessTools.FieldRefAccess<Jukebox, string>("_file");
        private static AccessTools.FieldRef<Jukebox, uint> _lengthRef = AccessTools.FieldRefAccess<Jukebox, uint>("_length");

        [HarmonyPostfix]
        [HarmonyPatch("GetNext")]
        public async static void GetNextPostfix(JukeboxInstance jukebox, bool forward)
        {

            if (Spotify.repeatTrack)
            {
                await Spotify.client.Player.SeekTo(new PlayerSeekToRequest(0));
                Spotify.timeTrackStarted = Time.time;
                Spotify.startingPosition = 0;
                return;
            }

            await Spotify.client.Player.SetVolume(new PlayerVolumeRequest(0));
            
            if (forward)
            {
                //QModManager.Utility.Logger.Log(QModManager.Utility.Logger.Level.Info, "Skip next track", null, true);
                await Spotify.client.Player.SkipNext(new PlayerSkipNextRequest() { DeviceId = Spotify.device.Id });
                Spotify.timeTrackStarted = Time.time;
                Spotify.startingPosition = 1000;
            }
            else
            {
                //QModManager.Utility.Logger.Log(QModManager.Utility.Logger.Level.Info, "Skip previous track", null, true);
                await Spotify.client.Player.SkipPrevious(new PlayerSkipPreviousRequest() { DeviceId = Spotify.device.Id });
                Spotify.timeTrackStarted = Time.time;
                Spotify.startingPosition = 1000;
            }

            if (true != Spotify.isPlaying)
            {
                await Spotify.client.Player.PausePlayback(new PlayerPausePlaybackRequest() { DeviceId = Spotify.device.Id });
            }

            await Spotify.client.Player.SetVolume(new PlayerVolumeRequest(Spotify.volume));
            await Spotify.trackDebouncer.DebounceAsync(() => Spotify.GetTrackInfo());
        }


        [HarmonyPostfix]
        [HarmonyPatch("OnApplicationQuit")]
        public async static void OnApplicationQuitPostfix()
        {
            Spotify.trackDebouncer.Debounce(() => { }); // Clear the debouncer
            if (Spotify.playingOnStartup) return;
            Spotify.isPlaying = null;
            var playbackRequest = new PlayerPausePlaybackRequest() { DeviceId = Spotify.device.Id };
            await Spotify.client.Player.PausePlayback(playbackRequest);
        }

        [HarmonyPostfix]
        [HarmonyPatch("Play")]
        public async static void PlayPostfix()
        {
            //QModManager.Utility.Logger.Log(QModManager.Utility.Logger.Level.Info, "Play track", null, true);
            Jukebox.volume = 0;
            Spotify.isPlaying = true;

            try
            {
                await Spotify.client.Player.ResumePlayback(new PlayerResumePlaybackRequest() { DeviceId = Spotify.device.Id });
                Spotify.isCurrentlyPlaying = true;
            } catch
            {
                //QModManager.Utility.Logger.Log(QModManager.Utility.Logger.Level.Info, "This fails when Spotify is already playing", null, true);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch("StopInternal")]
        public async static void StopInternalPostfix()
        {
            //QModManager.Utility.Logger.Log(QModManager.Utility.Logger.Level.Info, "Stop track", null, true);
            Spotify.isPlaying = null;
            Spotify.isPaused = false;
            await Spotify.client.Player.PausePlayback(new PlayerPausePlaybackRequest() { DeviceId = Spotify.device.Id });
            Spotify.isCurrentlyPlaying = false;
            await Spotify.client.Player.SeekTo(new PlayerSeekToRequest(0));
            Spotify.timeTrackStarted = Time.time;
            Spotify.startingPosition = 0;
        }

        [HarmonyPrefix]
        [HarmonyPatch("UpdateStudio")]
        public static void UpdateStudioPrefix(Jukebox __instance, ref bool ____paused, ref uint ____length, ref string ____file, ref uint ____position, ref JukeboxInstance ____instance)
        {

            if (Spotify.jukeboxNeedsUpdating)
            {
                Spotify.jukeboxNeedsUpdating = false;
                _playlistRef(__instance).Add(Spotify.currentTrackTitle); // might be adding over and over again, maybe. Need to check
                
                // Not sure how necessary these two are.
                _fileRef(__instance) = Spotify.currentTrackTitle;
                _lengthRef(__instance) = Spotify.currentTrackLength;

                // This is the part that actually updates the track info.
                if (null != ____instance && ____instance.file != Spotify.currentTrackTitle)
                {
                    ____instance.file = Spotify.currentTrackTitle;
                    JukeboxInstance.NotifyInfo(Spotify.currentTrackTitle, new Jukebox.TrackInfo() { label = Spotify.currentTrackTitle, length = Spotify.currentTrackLength });
                }
                Spotify.timeTrackStarted = Time.time - Spotify.startingPosition / 1000;

                if (Spotify.startingPosition > 0 && Spotify.playingOnStartup)
                {
                    Spotify.jukeboxNeedsPlaying = true;
                }
            }

            if (!____paused && true == Spotify.isPlaying) ____position = (uint)(Time.time - Spotify.timeTrackStarted) * 1000;

            //QModManager.Utility.Logger.Log(QModManager.Utility.Logger.Level.Info, "____position: " + ____position + " | Spotify.timeTrackStarted: " + Spotify.timeTrackStarted, null, true);

            if (____paused && Spotify.isPaused == false)
            {
                //QModManager.Utility.Logger.Log(QModManager.Utility.Logger.Level.Info, "Pause track", null, true);
                Spotify.isPaused = true;
                Spotify.client.Player.PausePlayback(new PlayerPausePlaybackRequest() { DeviceId = Spotify.device.Id });
                Spotify.isCurrentlyPlaying = false;
            }
            else if (!____paused && true == Spotify.isPaused && true == Spotify.isPlaying)
            {
                //QModManager.Utility.Logger.Log(QModManager.Utility.Logger.Level.Info, "Resume track", null, true);
                Spotify.isPaused = false;
                Spotify.client.Player.ResumePlayback(new PlayerResumePlaybackRequest() { DeviceId = Spotify.device.Id });
                Spotify.isCurrentlyPlaying = true;
            }
        }
    }
}
