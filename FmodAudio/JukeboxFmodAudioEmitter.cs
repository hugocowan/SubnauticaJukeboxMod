using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FMOD;
using FMODUnity;
using UnityEngine;

namespace JukeboxSpotify
{
    [DisallowMultipleComponent]
    internal sealed class JukeboxFmodAudioEmitter : MonoBehaviour
    {
        private static readonly HashSet<JukeboxFmodAudioEmitter> Emitters = new();
        private JukeboxInstance _jukeboxInstance;
        private JukeboxFmodStreamingBridge _audioBridge;
        private IAudioCaptureSession _captureSession;
        private Channel _channel;
        private float[] _transferBuffer;
        private string _activeSourceId = string.Empty;
        private bool _playbackRequested;
        private long _lastPumpStatsLogTick;
        private long _lastNoSamplesLogTick;
        private int _samplesPumpedSinceLastLog;
        private int _pumpIterationsSinceLastLog;
        private float _lastLoggedBaseVolume = -1f;
        private float _baseVolume;
        private Vector3 _lastSoundPosition;
        private bool _hasLastSoundPosition;

        public static JukeboxFmodAudioEmitter GetOrCreate(JukeboxInstance jukeboxInstance)
        {
            JukeboxFmodAudioEmitter emitter = jukeboxInstance.GetComponent<JukeboxFmodAudioEmitter>();
            return emitter != null ? emitter : jukeboxInstance.gameObject.AddComponent<JukeboxFmodAudioEmitter>();
        }

        public static void StopAll()
        {
            Plugin.LogDebug("Stopping all native audio emitters. emitterCount=" + Emitters.Count + ".");
            foreach (JukeboxFmodAudioEmitter emitter in Emitters)
            {
                emitter.StopAndDispose();
            }
        }

        private void Awake()
        {
            Emitters.Add(this);
            _jukeboxInstance = GetComponent<JukeboxInstance>();
            Plugin.LogDebug("Created native audio emitter for game object '" + gameObject.name + "'.");
        }

        private void OnDestroy()
        {
            Plugin.LogDebug("Destroying native audio emitter for game object '" + gameObject.name + "'.");
            Emitters.Remove(this);
            StopAndDispose();
        }

        private void Update()
        {
            UpdateChannelAttributes();

            if (!_playbackRequested || _captureSession == null || _audioBridge == null)
            {
                return;
            }

            if (_captureSession.AvailableSamples == 0)
            {
                MaybeLogNoSamples();
                return;
            }

            PumpCapturedAudio();
        }

        public async Task EnsurePlayingAsync(IMediaController mediaController, float baseVolume)
        {
            if (mediaController == null)
            {
                Plugin.LogDebug("Skipping native emitter playback request because the media controller is null.");
                return;
            }

            Plugin.LogDebug("Ensuring native audio playback for game object '" + gameObject.name + "'. controller='" + mediaController.ControllerName +
                "', baseVolume=" + baseVolume + ", activeSourceId='" + _activeSourceId + "'.");

            foreach (JukeboxFmodAudioEmitter emitter in Emitters)
            {
                if (emitter != this)
                {
                    Plugin.LogDebug("Stopping competing native emitter on game object '" + emitter.gameObject.name + "'.");
                    emitter.StopAndDispose();
                }
            }

            await EnsureCaptureSessionAsync(mediaController);
            if (_captureSession == null)
            {
                Plugin.LogDebug("No capture session is available for native emitter on game object '" + gameObject.name + "'.");
                return;
            }

            _playbackRequested = true;
            SetBaseVolume(baseVolume);
            _captureSession.Start();
            EnsureChannelPlaying();
            Plugin.LogDebug("Started native capture session for game object '" + gameObject.name + "'. sourceId='" + _activeSourceId +
                "', bufferedSamples=" + _captureSession.AvailableSamples + ".");
        }

        public void SetBaseVolume(float volume)
        {
            float clampedVolume = Mathf.Clamp01(volume);
            _baseVolume = clampedVolume;

            if (HasChannelHandle())
            {
                RESULT result = _channel.setVolume(clampedVolume);
                if (result != RESULT.OK)
                {
                    Plugin.LogDebugError("Failed to set FMOD channel volume for game object '" + gameObject.name + "'. result=" + result);
                }
            }

            if (Mathf.Abs(_lastLoggedBaseVolume - clampedVolume) > 0.001f)
            {
                _lastLoggedBaseVolume = clampedVolume;
                Plugin.LogDebug("Set native emitter base volume for game object '" + gameObject.name + "' to " + clampedVolume + ".");
            }
        }

        public void SetPlaybackActive(bool playbackActive)
        {
            bool playbackStateChanged = _playbackRequested != playbackActive;
            _playbackRequested = playbackActive;
            if (playbackStateChanged)
            {
                Plugin.LogDebug("Set native emitter playback active state for game object '" + gameObject.name + "' to " + playbackActive +
                    ". soundCreated=" + (_audioBridge?.IsCreated ?? false) + ", hasChannel=" + HasChannelHandle() + ".");
            }

            if (!HasChannelHandle())
            {
                return;
            }

            RESULT result = _channel.setPaused(!playbackActive);
            if (result != RESULT.OK)
            {
                Plugin.LogDebugError("Failed to update FMOD channel pause state for game object '" + gameObject.name + "'. result=" + result);
            }
        }

        public void StopAndDispose()
        {
            Plugin.LogDebug("Stopping native audio emitter for game object '" + gameObject.name + "'. activeSourceId='" + _activeSourceId +
                "', hadCaptureSession=" + (_captureSession != null) + ", bridgeBufferedSamples=" + (_audioBridge?.BufferedSamples ?? 0) + ".");
            _playbackRequested = false;

            if (HasChannelHandle())
            {
                RESULT result = _channel.stop();
                if (result != RESULT.OK && result != RESULT.ERR_INVALID_HANDLE)
                {
                    Plugin.LogDebugError("Failed to stop FMOD channel for game object '" + gameObject.name + "'. result=" + result);
                }

                _channel.clearHandle();
            }

            _audioBridge?.Dispose();

            if (_captureSession != null)
            {
                _captureSession.Dispose();
                _captureSession = null;
            }

            _audioBridge = null;
            _transferBuffer = null;
            _activeSourceId = string.Empty;
            _samplesPumpedSinceLastLog = 0;
            _pumpIterationsSinceLastLog = 0;
            _hasLastSoundPosition = false;
        }

        private async Task EnsureCaptureSessionAsync(IMediaController mediaController)
        {
            Plugin.LogDebug("Requesting a native capture session from controller '" + mediaController.ControllerName + "' for game object '" + gameObject.name + "'.");
            IAudioCaptureSession candidateSession = await mediaController.CreateAudioCaptureSessionAsync();
            if (candidateSession == null)
            {
                Plugin.LogDebug("Controller '" + mediaController.ControllerName + "' returned no native capture session for game object '" + gameObject.name + "'.");
                return;
            }

            if (_captureSession != null && string.Equals(_activeSourceId, candidateSession.SourceId, StringComparison.OrdinalIgnoreCase))
            {
                Plugin.LogDebug("Reusing the existing native capture session for source '" + _activeSourceId + "' on game object '" + gameObject.name + "'.");
                candidateSession.Dispose();
                return;
            }

            if (_captureSession != null)
            {
                Plugin.LogDebug("Replacing the native capture session on game object '" + gameObject.name + "' from source '" + _activeSourceId +
                    "' to '" + candidateSession.SourceId + "'.");
            }

            StopAndDispose();
            _captureSession = candidateSession;
            _activeSourceId = candidateSession.SourceId;
            _audioBridge = new JukeboxFmodStreamingBridge(candidateSession.Format);
            if (!_audioBridge.EnsureCreated("JukeboxNativeMedia"))
            {
                _audioBridge.Dispose();
                _audioBridge = null;
                _captureSession.Dispose();
                _captureSession = null;
                _activeSourceId = string.Empty;
                return;
            }

            _transferBuffer = new float[Math.Max(2048, candidateSession.Format.SamplesPerSecond / 10)];
            Plugin.LogDebug("Configured native audio emitter for source '" + _activeSourceId + "' on game object '" + gameObject.name +
                "'. sampleRate=" + candidateSession.Format.SampleRate + ", channelCount=" + candidateSession.Format.ChannelCount +
                ", transferBufferSamples=" + _transferBuffer.Length + ".");
        }

        private void EnsureChannelPlaying()
        {
            if (_audioBridge == null || !_audioBridge.IsCreated)
            {
                Plugin.LogDebug("Skipping FMOD channel startup because the streaming sound is not created for game object '" + gameObject.name + "'.");
                return;
            }

            if (HasChannelHandle())
            {
                RESULT unpauseResult = _channel.setPaused(false);
                if (unpauseResult != RESULT.OK)
                {
                    Plugin.LogDebugError("Failed to unpause FMOD channel for game object '" + gameObject.name + "'. result=" + unpauseResult);
                }

                ApplyChannelDefaults();
                return;
            }

            ChannelGroup targetChannelGroup = ResolveChannelGroup();
            RESULT playResult = RuntimeManager.CoreSystem.playSound(_audioBridge.Sound, targetChannelGroup, true, out _channel);
            if (playResult != RESULT.OK)
            {
                Plugin.LogDebugError("Failed to start FMOD streaming sound for game object '" + gameObject.name + "'. result=" + playResult);
                _channel.clearHandle();
                return;
            }

            ApplyChannelDefaults();

            RESULT pauseResult = _channel.setPaused(false);
            if (pauseResult != RESULT.OK)
            {
                Plugin.LogDebugError("Failed to unpause FMOD streaming channel for game object '" + gameObject.name + "'. result=" + pauseResult);
            }

            Plugin.LogDebug("Started FMOD streaming channel for game object '" + gameObject.name + "'. routedToJukeboxChannelGroup=" + (targetChannelGroup.handle != IntPtr.Zero) + ".");
        }

        private void PumpCapturedAudio()
        {
            int pumpedSamples = 0;
            int pumpIterations = 0;

            while (_captureSession != null && _audioBridge != null && _captureSession.AvailableSamples > 0)
            {
                int requestedSamples = Math.Min(_captureSession.AvailableSamples, _transferBuffer.Length);
                int readSamples = _captureSession.Read(_transferBuffer, 0, requestedSamples);
                if (readSamples <= 0)
                {
                    Plugin.LogDebug("Native emitter stopped pumping because the capture session returned no samples for game object '" + gameObject.name + "'.");
                    break;
                }

                _audioBridge.Write(_transferBuffer, readSamples);
                pumpedSamples += readSamples;
                pumpIterations++;
            }

            if (pumpedSamples > 0)
            {
                _samplesPumpedSinceLastLog += pumpedSamples;
                _pumpIterationsSinceLastLog += pumpIterations;
                MaybeLogPumpStats();
            }
        }

        private void MaybeLogNoSamples()
        {
            if (ShouldLog(ref _lastNoSamplesLogTick))
            {
                Plugin.LogDebug("Native emitter is waiting for captured samples on game object '" + gameObject.name + "'. sourceId='" + _activeSourceId +
                    "', soundCreated=" + (_audioBridge?.IsCreated ?? false) + ", hasChannel=" + HasChannelHandle() + ".");
            }
        }

        private void MaybeLogPumpStats()
        {
            if (!ShouldLog(ref _lastPumpStatsLogTick))
            {
                return;
            }

            Plugin.LogDebug("Native emitter pump stats for game object '" + gameObject.name + "'. sourceId='" + _activeSourceId +
                "', pumpIterations=" + _pumpIterationsSinceLastLog + ", pumpedSamples=" + _samplesPumpedSinceLastLog +
                ", captureBufferedSamples=" + (_captureSession?.AvailableSamples ?? 0) + ", bridgeBufferedSamples=" + (_audioBridge?.BufferedSamples ?? 0) +
                ", hasChannel=" + HasChannelHandle() + ".");
            _samplesPumpedSinceLastLog = 0;
            _pumpIterationsSinceLastLog = 0;
        }

        private void ApplyChannelDefaults()
        {
            if (!HasChannelHandle())
            {
                return;
            }

            LogIfFailed(_channel.setMode(MODE.LOOP_NORMAL | MODE._3D | MODE._3D_WORLDRELATIVE | MODE.CREATESTREAM), "set the FMOD channel mode");
            LogIfFailed(_channel.set3DDopplerLevel(0f), "disable FMOD doppler");
            LogIfFailed(_channel.setVolume(_baseVolume), "set the FMOD channel volume");
            UpdateChannelAttributes(forceLog: true);
        }

        private void UpdateChannelAttributes(bool forceLog = false)
        {
            if (!HasChannelHandle() || _jukeboxInstance == null)
            {
                return;
            }

            Vector3 soundPosition = transform.position;
            float minDistance = 1f;
            float maxDistance = Mathf.Max(Jukebox.maxDistance, minDistance + 1f);
            bool resolvedSoundPosition = _jukeboxInstance.GetSoundPosition(out Vector3 computedPosition, out float computedMinDistance, out float power);
            if (resolvedSoundPosition)
            {
                soundPosition = computedPosition;
                if (computedMinDistance > 0f)
                {
                    minDistance = computedMinDistance;
                }
            }

            if (maxDistance <= minDistance)
            {
                maxDistance = minDistance + 1f;
            }

            VECTOR fmodPosition = ToFmodVector(soundPosition);
            VECTOR velocity = default;
            LogIfFailed(_channel.set3DAttributes(ref fmodPosition, ref velocity), "update the FMOD channel 3D attributes");
            LogIfFailed(_channel.set3DMinMaxDistance(minDistance, maxDistance), "update the FMOD channel min/max distance");

            if (forceLog || !_hasLastSoundPosition || (soundPosition - _lastSoundPosition).sqrMagnitude > 0.01f)
            {
                _lastSoundPosition = soundPosition;
                _hasLastSoundPosition = true;
                Plugin.LogDebug("Updated FMOD channel attributes for game object '" + gameObject.name + "'. resolvedSoundPosition=" + resolvedSoundPosition +
                    ", position=" + soundPosition + ", minDistance=" + minDistance + ", maxDistance=" + maxDistance + ", power=" + power + ".");
            }
        }

        private ChannelGroup ResolveChannelGroup()
        {
            if (Jukebox.main != null && Jukebox.main._channelGroup.handle != IntPtr.Zero)
            {
                return Jukebox.main._channelGroup;
            }

            return default;
        }

        private bool HasChannelHandle()
        {
            return _channel.handle != IntPtr.Zero;
        }

        private void LogIfFailed(RESULT result, string operation)
        {
            if (result != RESULT.OK)
            {
                Plugin.LogDebugError("Failed to " + operation + " for game object '" + gameObject.name + "'. result=" + result);
            }
        }

        private static VECTOR ToFmodVector(Vector3 value)
        {
            return new VECTOR
            {
                x = value.x,
                y = value.y,
                z = value.z,
            };
        }

        private static bool ShouldLog(ref long lastLogTick, int minimumIntervalMs = 1000)
        {
            long currentTick = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
            if (currentTick - lastLogTick < minimumIntervalMs)
            {
                return false;
            }

            lastLogTick = currentTick;
            return true;
        }
    }
}