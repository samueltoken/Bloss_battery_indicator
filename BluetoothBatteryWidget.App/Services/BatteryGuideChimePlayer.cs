using System.IO;
using System.Media;

namespace BluetoothBatteryWidget.App.Services;

internal sealed class BatteryGuideChimePlayer : IDisposable
{
    private readonly object _sync = new();
    private readonly byte[] _waveData;
    private MemoryStream? _stream;
    private SoundPlayer? _player;

    public BatteryGuideChimePlayer(byte[] waveData)
    {
        _waveData = waveData.Length == 0 ? BatteryGuideChimeBuilder.CreateDreamChimeWave() : waveData;
    }

    public void PlayFromStart()
    {
        lock (_sync)
        {
            StopLocked();

            try
            {
                _stream = new MemoryStream(_waveData, writable: false);
                _player = new SoundPlayer(_stream);
                _player.Load();
                _player.Play();
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
            _stream?.Dispose();
        }
        catch
        {
            // Ignore shutdown races.
        }

        _stream = null;
    }
}
