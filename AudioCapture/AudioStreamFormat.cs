namespace JukeboxSpotify
{
    internal readonly struct AudioStreamFormat
    {
        public AudioStreamFormat(int sampleRate, int channelCount)
        {
            SampleRate = sampleRate;
            ChannelCount = channelCount;
        }

        public int SampleRate { get; }

        public int ChannelCount { get; }

        public int SamplesPerSecond => SampleRate * ChannelCount;
    }
}