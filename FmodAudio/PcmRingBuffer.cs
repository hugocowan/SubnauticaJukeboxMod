using System;
using System.Threading;

namespace JukeboxSpotify
{
    internal sealed class PcmRingBuffer
    {
        private readonly float[] _buffer;
        private int _readIndex;
        private int _writeIndex;

        public PcmRingBuffer(int capacity)
        {
            if (capacity < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "The ring buffer capacity must be at least 2 samples.");
            }

            _buffer = new float[capacity];
        }

        public int Capacity => _buffer.Length - 1;

        public int Count
        {
            get
            {
                int writeIndex = Volatile.Read(ref _writeIndex);
                int readIndex = Volatile.Read(ref _readIndex);
                return writeIndex >= readIndex ? writeIndex - readIndex : _buffer.Length - readIndex + writeIndex;
            }
        }

        public int Write(float[] source, int offset, int sampleCount)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            int written = 0;

            while (written < sampleCount)
            {
                int writeIndex = Volatile.Read(ref _writeIndex);
                int nextWriteIndex = (writeIndex + 1) % _buffer.Length;

                if (nextWriteIndex == Volatile.Read(ref _readIndex))
                {
                    break;
                }

                _buffer[writeIndex] = source[offset + written];
                Volatile.Write(ref _writeIndex, nextWriteIndex);
                written++;
            }

            return written;
        }

        public int Read(float[] destination, int offset, int sampleCount)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            int read = 0;

            while (read < sampleCount)
            {
                int readIndex = Volatile.Read(ref _readIndex);
                if (readIndex == Volatile.Read(ref _writeIndex))
                {
                    break;
                }

                destination[offset + read] = _buffer[readIndex];
                Volatile.Write(ref _readIndex, (readIndex + 1) % _buffer.Length);
                read++;
            }

            return read;
        }

        public void Clear()
        {
            Array.Clear(_buffer, 0, _buffer.Length);
            Volatile.Write(ref _readIndex, 0);
            Volatile.Write(ref _writeIndex, 0);
        }
    }
}