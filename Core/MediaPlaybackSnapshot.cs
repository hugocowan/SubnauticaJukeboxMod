namespace JukeboxSpotify
{
    internal sealed class MediaPlaybackSnapshot
    {
        public static MediaPlaybackSnapshot Empty { get; } = new()
        {
            TrackTitle = "OS Media Jukebox",
            HasTrack = false,
            PlaybackState = MediaPlaybackState.Unknown,
            DurationMs = 0,
            PositionMs = 0,
            ShuffleEnabled = false,
            SourceId = string.Empty,
            Artist = string.Empty,
            Album = string.Empty,
            StatusMessage = "No active media session detected.",
        };

        public bool HasTrack { get; init; }

        public string TrackTitle { get; init; } = string.Empty;

        public string Artist { get; init; } = string.Empty;

        public string Album { get; init; } = string.Empty;

        public uint DurationMs { get; init; }

        public uint PositionMs { get; init; }

        public MediaPlaybackState PlaybackState { get; init; }

        public bool ShuffleEnabled { get; init; }

        public string SourceId { get; init; } = string.Empty;

        public string StatusMessage { get; init; } = string.Empty;

        public string DisplayTitle => string.IsNullOrWhiteSpace(Artist) ? TrackTitle : $"{TrackTitle} - {Artist}";
    }
}