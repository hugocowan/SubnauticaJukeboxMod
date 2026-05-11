using System;
using System.Collections;
using System.Reflection;
using System.Threading.Tasks;

namespace JukeboxSpotify
{
    internal sealed class WindowsMediaSessionController : IMediaController
    {
        private const string WindowsRuntimeAssembly = ", Windows, ContentType=WindowsRuntime";

        private object _sessionManager;

        private object _selectedSession;

        private bool _initialized;

        private string _lastSelectedSessionDescriptor = string.Empty;

        private string _lastSelectedSessionReason = string.Empty;

        private string _lastSnapshotSignature = string.Empty;

        public string ControllerName => "Windows Media Session";

        public bool IsReady => _initialized;

        public bool SupportsSourceVolume => false;

        public async Task InitializeAsync()
        {
            LogInfo("Initializing the native Windows media-session controller. platform=" + Environment.OSVersion.Platform + ".");

            if (!IsWindowsPlatform())
            {
                LogInfo("The native media-session backend is only available on Windows.");
                return;
            }

            if (!WinRtAsyncBridge.IsAvailable)
            {
                LogInfo("WinRT async support is unavailable in the current runtime.");
                return;
            }

            Type managerType = ResolveWinRtType("Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager");
            if (managerType == null)
            {
                LogInfo("The GSMTC session manager type is unavailable.");
                return;
            }

            try
            {
                LogInfo("Requesting the GSMTC session manager from WinRT.");
                _sessionManager = await InvokeWinRtAsync(managerType, null, "RequestAsync");
                object sessions = GetSessions();
                _selectedSession = SelectPreferredSession(sessions);
                _initialized = _sessionManager != null;

                if (_initialized)
                {
                    LogInfo("Initialized the native Windows media-session backend. sessionCount=" + CountSessions(sessions) + ".");
                }
            }
            catch (Exception e)
            {
                LogError("Failed to initialize the native Windows media-session backend", e);
            }
        }

        public async Task<MediaPlaybackSnapshot> GetPlaybackSnapshotAsync()
        {
            if (!_initialized)
            {
                LogInfo("Skipping Windows media-session snapshot because the controller is not initialized.");
                return new MediaPlaybackSnapshot
                {
                    HasTrack = false,
                    StatusMessage = "Native Windows media session support is unavailable on the current runtime.",
                };
            }

            try
            {
                object session = SelectPreferredSession(GetSessions());
                if (session == null)
                {
                    LogInfo("No active Windows media session was found while building a playback snapshot.");
                    return new MediaPlaybackSnapshot
                    {
                        HasTrack = false,
                        StatusMessage = "No active Windows media session was found.",
                    };
                }

                _selectedSession = session;
                object mediaProperties = await InvokeWinRtAsync(session.GetType(), session, "TryGetMediaPropertiesAsync");
                object playbackInfo = Invoke(session, "GetPlaybackInfo");
                object timelineProperties = Invoke(session, "GetTimelineProperties");
                MediaPlaybackSnapshot snapshot = BuildSnapshot(session, mediaProperties, playbackInfo, timelineProperties);
                LogSnapshotIfChanged(snapshot);
                return snapshot;
            }
            catch (Exception e)
            {
                LogError("Failed to read the current Windows media session state", e);
                return new MediaPlaybackSnapshot
                {
                    HasTrack = false,
                    StatusMessage = "Failed to query the active Windows media session.",
                };
            }
        }

        public Task<IAudioCaptureSession> CreateAudioCaptureSessionAsync()
        {
            if (!_initialized)
            {
                LogInfo("Skipping audio capture session creation because the Windows media-session controller is not initialized.");
                return Task.FromResult<IAudioCaptureSession>(null);
            }

            object session = SelectPreferredSession(GetSessions());
            if (session == null)
            {
                LogInfo("Skipping audio capture session creation because no Windows media session is selected.");
                return Task.FromResult<IAudioCaptureSession>(null);
            }

            string sourceId = GetStringProperty(session, "SourceAppUserModelId");
            LogInfo("Creating process-loopback capture session for Windows media source '" + sourceId + "'.");
            int? processId = MediaSourceProcessResolver.ResolveProcessId(sourceId);
            if (processId == null)
            {
                LogInfo("Could not resolve a process id for the current media session source '" + sourceId + "'.");
                return Task.FromResult<IAudioCaptureSession>(null);
            }

            IAudioCaptureSession captureSession = new WindowsProcessLoopbackCaptureSession(sourceId, processId.Value, new AudioStreamFormat(44100, 2));
            LogInfo("Created process-loopback capture session for source '" + sourceId + "' using process id " + processId.Value + ".");
            return Task.FromResult(captureSession);
        }

        public Task PlayAsync()
        {
            return TrySessionCommandAsync("TryPlayAsync");
        }

        public Task PauseAsync()
        {
            return TrySessionCommandAsync("TryPauseAsync");
        }

        public Task NextAsync()
        {
            return TrySessionCommandAsync("TrySkipNextAsync");
        }

        public Task PreviousAsync()
        {
            return TrySessionCommandAsync("TrySkipPreviousAsync");
        }

        public Task SeekAsync(long positionMs)
        {
            return TrySessionCommandAsync("TryChangePlaybackPositionAsync", positionMs);
        }

        public Task SetSourceVolumeAsync(int volumePercent)
        {
            LogInfo("Ignoring media-source volume request because volume is owned by the game. requestedVolumePercent=" + volumePercent + ".");
            return Task.CompletedTask;
        }

        public Task SetShuffleAsync(bool enabled)
        {
            return TrySessionCommandAsync("TryChangeShuffleActiveAsync", enabled);
        }

        public Task SetRepeatModeAsync(MediaRepeatMode repeatMode)
        {
            Type autoRepeatModeType = ResolveWinRtType("Windows.Media.MediaPlaybackAutoRepeatMode");
            if (autoRepeatModeType == null)
            {
                return Task.CompletedTask;
            }

            string repeatModeName = repeatMode switch
            {
                MediaRepeatMode.Track => "Track",
                MediaRepeatMode.Context => "List",
                _ => "None",
            };

            LogInfo("Changing native repeat mode to '" + repeatModeName + "'.");
            object nativeRepeatMode = Enum.Parse(autoRepeatModeType, repeatModeName, ignoreCase: false);
            return TrySessionCommandAsync("TryChangeAutoRepeatModeAsync", nativeRepeatMode);
        }

        public Task ShutdownAsync()
        {
            LogInfo("Shutting down the native Windows media-session controller.");
            _selectedSession = null;
            _sessionManager = null;
            _initialized = false;
            return Task.CompletedTask;
        }

        private MediaPlaybackSnapshot BuildSnapshot(object session, object mediaProperties, object playbackInfo, object timelineProperties)
        {
            TimeSpan startTime = GetPropertyValue(timelineProperties, "StartTime", TimeSpan.Zero);
            TimeSpan endTime = GetPropertyValue(timelineProperties, "EndTime", TimeSpan.Zero);
            TimeSpan position = GetPropertyValue(timelineProperties, "Position", TimeSpan.Zero);
            TimeSpan duration = endTime > startTime ? endTime - startTime : TimeSpan.Zero;
            string trackTitle = GetStringProperty(mediaProperties, "Title");
            string sourceId = GetStringProperty(session, "SourceAppUserModelId");

            return new MediaPlaybackSnapshot
            {
                HasTrack = !string.IsNullOrWhiteSpace(trackTitle),
                TrackTitle = string.IsNullOrWhiteSpace(trackTitle) ? sourceId : trackTitle,
                Artist = GetStringProperty(mediaProperties, "Artist"),
                Album = GetStringProperty(mediaProperties, "AlbumTitle"),
                DurationMs = ToMilliseconds(duration),
                PositionMs = ToMilliseconds(position),
                PlaybackState = MapPlaybackState(GetPropertyValue(playbackInfo, "PlaybackStatus")?.ToString()),
                ShuffleEnabled = GetPropertyValue(playbackInfo, "IsShuffleActive", false),
                SourceId = sourceId,
                StatusMessage = string.IsNullOrWhiteSpace(trackTitle)
                    ? "The active Windows media session does not expose track metadata."
                    : string.Empty,
            };
        }

        private object GetSessions()
        {
            return _sessionManager == null ? null : Invoke(_sessionManager, "GetSessions");
        }

        private object SelectPreferredSession(object sessionsObject)
        {
            object currentSession = _sessionManager == null ? null : Invoke(_sessionManager, "GetCurrentSession");
            if (currentSession != null)
            {
                TrackSelectedSession(currentSession, "session manager current session");
                return currentSession;
            }

            if (sessionsObject is not IEnumerable sessions)
            {
                TrackSelectedSession(_selectedSession, "cached previous session");
                return _selectedSession;
            }

            object pausedSession = null;
            object firstSession = null;
            int scannedSessionCount = 0;

            foreach (object session in sessions)
            {
                scannedSessionCount++;
                firstSession ??= session;

                object playbackInfo = Invoke(session, "GetPlaybackInfo");
                string statusName = GetPropertyValue(playbackInfo, "PlaybackStatus")?.ToString();
                if (statusName == "Playing")
                {
                    TrackSelectedSession(session, "playing session; scannedSessions=" + scannedSessionCount);
                    return session;
                }

                if (pausedSession == null && statusName == "Paused")
                {
                    pausedSession = session;
                }
            }

            object selectedSession = pausedSession ?? firstSession ?? _selectedSession;
            string selectionReason = pausedSession != null
                ? "paused session fallback"
                : firstSession != null
                    ? "first available session fallback"
                    : "cached previous session";
            TrackSelectedSession(selectedSession, selectionReason + "; scannedSessions=" + scannedSessionCount);
            return selectedSession;
        }

        private async Task TrySessionCommandAsync(string methodName, params object[] arguments)
        {
            if (!_initialized)
            {
                LogInfo("Skipping Windows media-session command '" + methodName + "' because the controller is not initialized.");
                return;
            }

            object session = SelectPreferredSession(GetSessions());
            if (session == null)
            {
                LogInfo("Skipping Windows media-session command '" + methodName + "' because no session is selected.");
                return;
            }

            try
            {
                LogInfo("Invoking Windows media-session command '" + methodName + "' against " + DescribeSession(session) + " with args [" + FormatArguments(arguments) + "].");
                object commandResult = await InvokeWinRtAsync(session.GetType(), session, methodName, arguments);
                LogInfo("Windows media-session command '" + methodName + "' completed with result='" + (commandResult ?? "<null>") + "'.");
            }
            catch (Exception e)
            {
                LogError($"The native Windows media-session command '{methodName}' failed", e);
            }
        }

        private static async Task<object> InvokeWinRtAsync(Type ownerType, object instance, string methodName, params object[] arguments)
        {
            MethodInfo method = ownerType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
            object asyncOperation = method?.Invoke(instance, arguments);
            return await WinRtAsyncBridge.AwaitResultAsync(asyncOperation);
        }

        private static object Invoke(object instance, string methodName, params object[] arguments)
        {
            return instance?
                .GetType()
                .GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance)
                ?.Invoke(instance, arguments);
        }

        private static T GetPropertyValue<T>(object instance, string propertyName, T fallbackValue)
        {
            object value = GetPropertyValue(instance, propertyName);
            return value is T typedValue ? typedValue : fallbackValue;
        }

        private static object GetPropertyValue(object instance, string propertyName)
        {
            return instance?
                .GetType()
                .GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(instance);
        }

        private static string GetStringProperty(object instance, string propertyName)
        {
            return GetPropertyValue(instance, propertyName)?.ToString() ?? string.Empty;
        }

        private static Type ResolveWinRtType(string fullTypeName)
        {
            return Type.GetType(fullTypeName + WindowsRuntimeAssembly, throwOnError: false);
        }

        private static uint ToMilliseconds(TimeSpan timeSpan)
        {
            double milliseconds = Math.Max(0d, timeSpan.TotalMilliseconds);
            return milliseconds >= uint.MaxValue ? uint.MaxValue : (uint)milliseconds;
        }

        private static MediaPlaybackState MapPlaybackState(string playbackStatus)
        {
            return playbackStatus switch
            {
                "Playing" => MediaPlaybackState.Playing,
                "Paused" => MediaPlaybackState.Paused,
                "Stopped" => MediaPlaybackState.Stopped,
                _ => MediaPlaybackState.Unknown,
            };
        }

        private static bool IsWindowsPlatform()
        {
            return Environment.OSVersion.Platform == PlatformID.Win32NT;
        }

        private void TrackSelectedSession(object session, string reason)
        {
            string descriptor = session == null ? "<none>" : DescribeSession(session);
            if (descriptor == _lastSelectedSessionDescriptor && reason == _lastSelectedSessionReason)
            {
                return;
            }

            _lastSelectedSessionDescriptor = descriptor;
            _lastSelectedSessionReason = reason;
            LogInfo("Selected Windows media session: " + descriptor + " (" + reason + ").");
        }

        private void LogSnapshotIfChanged(MediaPlaybackSnapshot snapshot)
        {
            string signature = snapshot.HasTrack + "|" + snapshot.SourceId + "|" + snapshot.TrackTitle + "|" + snapshot.Artist + "|" + snapshot.PlaybackState + "|" + snapshot.ShuffleEnabled;
            if (signature == _lastSnapshotSignature)
            {
                return;
            }

            _lastSnapshotSignature = signature;
            LogInfo("Windows media-session snapshot changed. " + DescribeSnapshot(snapshot));
        }

        private static int CountSessions(object sessionsObject)
        {
            if (sessionsObject is not IEnumerable sessions)
            {
                return 0;
            }

            int count = 0;
            foreach (object _ in sessions)
            {
                count++;
            }

            return count;
        }

        private static string DescribeSession(object session)
        {
            object playbackInfo = Invoke(session, "GetPlaybackInfo");
            return "sourceId='" + GetStringProperty(session, "SourceAppUserModelId") + "', playbackStatus='" + GetPropertyValue(playbackInfo, "PlaybackStatus") + "'";
        }

        private static string DescribeSnapshot(MediaPlaybackSnapshot snapshot)
        {
            return "hasTrack=" + snapshot.HasTrack +
                ", playbackState=" + snapshot.PlaybackState +
                ", trackTitle='" + snapshot.TrackTitle + "'" +
                ", artist='" + snapshot.Artist + "'" +
                ", durationMs=" + snapshot.DurationMs +
                ", positionMs=" + snapshot.PositionMs +
                ", shuffleEnabled=" + snapshot.ShuffleEnabled +
                ", sourceId='" + snapshot.SourceId + "'" +
                ", statusMessage='" + snapshot.StatusMessage + "'";
        }

        private static string FormatArguments(object[] arguments)
        {
            if (arguments == null || arguments.Length == 0)
            {
                return "<none>";
            }

            string[] formattedArguments = new string[arguments.Length];
            for (int index = 0; index < arguments.Length; index++)
            {
                formattedArguments[index] = arguments[index]?.ToString() ?? "<null>";
            }

            return string.Join(", ", formattedArguments);
        }

        private static void LogInfo(string message)
        {
            Plugin.LogDebug(message);
        }

        private static void LogError(string message, Exception exception)
        {
            Plugin.LogDebugError(message, exception);
        }
    }
}