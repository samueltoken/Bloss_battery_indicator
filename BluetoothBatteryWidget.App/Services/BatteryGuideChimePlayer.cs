using System.IO;
using System.Media;
using System.Windows.Media;
using ThreadingTimer = System.Threading.Timer;

namespace BluetoothBatteryWidget.App.Services;

internal sealed class BatteryGuideChimePlayer : IDisposable
{
    private readonly object _sync = new();
    private readonly byte[] _waveData;
    private MemoryStream? _stream;
    private SoundPlayer? _player;
    private MediaPlayer? _mediaPlayer;
    private ThreadingTimer? _completionTimer;
    private int _playbackVersion;

    public event EventHandler? PlaybackEnded;

    public BatteryGuideChimePlayer(byte[] waveData)
    {
        _waveData = waveData.Length == 0 ? BatteryGuideChimeBuilder.CreateDreamChimeWave() : waveData;
    }

    public void PlayFromStart()
    {
        PlayFromStart(BatteryGuideSoundCatalog.ResolveGuideSound(null));
    }

    public void PlayFromStart(BatteryGuideSoundOption option)
    {
        lock (_sync)
        {
            StopLocked();
            var playbackVersion = ++_playbackVersion;

            try
            {
                if (option.IsWave)
                {
                    PlayWaveLocked(BatteryGuideSoundCatalog.LoadBytes(option), playbackVersion);
                    return;
                }

                var path = BatteryGuideSoundCatalog.EnsureTempFile(option);
                if (string.IsNullOrWhiteSpace(path))
                {
                    PlayWaveLocked(_waveData, playbackVersion);
                    return;
                }

                PlayMediaLocked(path, playbackVersion);
            }
            catch
            {
                StopLocked();
            }
        }
    }

    public void Stop()
    {
        lock (_sync)
        {
            StopLocked();
            _playbackVersion++;
        }
    }

    public void Dispose()
    {
        Stop();
    }

    private void StopLocked()
    {
        try
        {
            _player?.Stop();
        }
        catch
        {
            // Audio feedback is optional; the popup should still work without it.
        }

        try
        {
            _player?.Dispose();
        }
        catch
        {
            // Ignore shutdown races.
        }

        _player = null;

        try
        {
            _mediaPlayer?.Stop();
            _mediaPlayer?.Close();
        }
        catch
        {
            // Ignore shutdown races.
        }

        _mediaPlayer = null;

        try
        {
            _completionTimer?.Dispose();
        }
        catch
        {
            // Ignore shutdown races.
        }

        _completionTimer = null;

        try
        {
            _stream?.Dispose();
        }
        catch
        {
            // Ignore shutdown races.
        }

        _stream = null;
    }

    private void PlayWaveLocked(byte[] waveData, int playbackVersion)
    {
        var data = waveData.Length == 0 ? _waveData : waveData;
        _stream = new MemoryStream(data, writable: false);
        _player = new SoundPlayer(_stream);
        _player.Load();
        _player.Play();
        ScheduleCompletionLocked(playbackVersion, TryGetWaveDuration(data) ?? TimeSpan.FromSeconds(8));
    }

    private void PlayMediaLocked(string path, int playbackVersion)
    {
        var player = new MediaPlayer();
        player.MediaOpened += (_, _) =>
        {
            if (player.NaturalDuration.HasTimeSpan)
            {
                ScheduleCompletion(playbackVersion, player.NaturalDuration.TimeSpan);
            }
        };
        player.MediaEnded += (_, _) => CompletePlayback(playbackVersion);
        player.MediaFailed += (_, _) => CompletePlayback(playbackVersion);

        _mediaPlayer = player;
        _mediaPlayer.Open(new Uri(path, UriKind.Absolute));
        _mediaPlayer.Play();
    }

    private void ScheduleCompletion(int playbackVersion, TimeSpan duration)
    {
        lock (_sync)
        {
            if (playbackVersion != _playbackVersion)
            {
                return;
            }

            ScheduleCompletionLocked(playbackVersion, duration);
        }
    }

    private void ScheduleCompletionLocked(int playbackVersion, TimeSpan duration)
    {
        var safeDuration = duration <= TimeSpan.Zero ? TimeSpan.FromSeconds(8) : duration;
        _completionTimer?.Dispose();
        _completionTimer = new ThreadingTimer(
            _ => CompletePlayback(playbackVersion),
            null,
            safeDuration + TimeSpan.FromMilliseconds(350),
            Timeout.InfiniteTimeSpan);
    }

    private void CompletePlayback(int playbackVersion)
    {
        var shouldRaise = false;
        lock (_sync)
        {
            if (playbackVersion != _playbackVersion)
            {
                return;
            }

            StopLocked();
            _playbackVersion++;
            shouldRaise = true;
        }

        if (shouldRaise)
        {
            PlaybackEnded?.Invoke(this, EventArgs.Empty);
        }
    }

    private static TimeSpan? TryGetWaveDuration(byte[] waveData)
    {
        if (waveData.Length < 44 ||
            waveData[0] != 'R' || waveData[1] != 'I' || waveData[2] != 'F' || waveData[3] != 'F' ||
            waveData[8] != 'W' || waveData[9] != 'A' || waveData[10] != 'V' || waveData[11] != 'E')
        {
            return null;
        }

        var offset = 12;
        var byteRate = 0;
        var dataSize = 0;
        while (offset + 8 <= waveData.Length)
        {
            var chunkId = System.Text.Encoding.ASCII.GetString(waveData, offset, 4);
            var chunkSize = BitConverter.ToInt32(waveData, offset + 4);
            var dataOffset = offset + 8;
            if (chunkSize < 0 || dataOffset + chunkSize > waveData.Length)
            {
                break;
            }

            if (string.Equals(chunkId, "fmt ", StringComparison.Ordinal) && chunkSize >= 16)
            {
                byteRate = BitConverter.ToInt32(waveData, dataOffset + 8);
            }
            else if (string.Equals(chunkId, "data", StringComparison.Ordinal))
            {
                dataSize = chunkSize;
            }

            if (byteRate > 0 && dataSize > 0)
            {
                return TimeSpan.FromSeconds(dataSize / (double)byteRate);
            }

            offset = dataOffset + chunkSize + (chunkSize % 2);
        }

        return null;
    }
}
