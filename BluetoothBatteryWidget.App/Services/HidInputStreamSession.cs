using System.Buffers;
using System.IO;
using BluetoothBatteryWidget.Core.Models;
using Microsoft.Win32.SafeHandles;

namespace BluetoothBatteryWidget.App.Services;

internal sealed class HidInputStreamSession : IDisposable
{
    private readonly SafeFileHandle _sourceHandle;
    private readonly SafeFileHandle _borrowedHandle;
    private readonly bool _addRefAcquired;
    private readonly FileStream? _stream;
    private bool _disposed;

    public HidInputStreamSession(SafeFileHandle sourceHandle)
    {
        _sourceHandle = sourceHandle;
        if (!HidGamepadAccess.TryBorrowNonOwningHandle(sourceHandle, out var borrowedHandle, out var addRefAcquired))
        {
            _borrowedHandle = new SafeFileHandle(IntPtr.Zero, ownsHandle: false);
            _addRefAcquired = false;
            IsAvailable = false;
            return;
        }

        _borrowedHandle = borrowedHandle;
        _addRefAcquired = addRefAcquired;

        try
        {
            _stream = new FileStream(_borrowedHandle, FileAccess.Read, 512, isAsync: true);
            IsAvailable = true;
        }
        catch
        {
            IsAvailable = false;
            try
            {
                _borrowedHandle.Dispose();
            }
            catch
            {
                // Ignore dispose failures.
            }

            HidGamepadAccess.ReleaseBorrowedHandle(_sourceHandle, _addRefAcquired);
        }
    }

    public bool IsAvailable { get; }

    public bool HasCaptured { get; private set; }

    public IReadOnlyList<HidCapturedReportFrame> CaptureWarmupFrames(
        int minimumReportSize,
        int frameBudget,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        if (frameBudget <= 0)
        {
            return [];
        }

        var frames = new List<HidCapturedReportFrame>(frameBudget);
        for (var index = 0; index < frameBudget; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryReadReport(0x00, minimumReportSize, timeoutMs, out var frame, out _))
            {
                continue;
            }

            if (frame.ReportId == 0x00)
            {
                continue;
            }

            frames.Add(frame);
        }

        return frames;
    }

    public bool TryReadReport(
        byte expectedReportId,
        int minimumReportSize,
        int timeoutMs,
        out HidCapturedReportFrame frame,
        out bool timedOut)
    {
        frame = null!;
        timedOut = false;

        if (_disposed || !IsAvailable || _stream is null || minimumReportSize <= 0)
        {
            return false;
        }

        var deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(40, timeoutMs));
        var bufferLength = Math.Max(16, minimumReportSize);

        while (DateTime.UtcNow < deadline)
        {
            var remainingMs = Math.Max(20, (int)Math.Ceiling((deadline - DateTime.UtcNow).TotalMilliseconds));
            var rented = ArrayPool<byte>.Shared.Rent(bufferLength);
            try
            {
                Array.Clear(rented, 0, bufferLength);
                using var cts = new CancellationTokenSource(remainingMs);
                var readTask = _stream.ReadAsync(rented.AsMemory(0, bufferLength), cts.Token).AsTask();

                try
                {
                    readTask.Wait(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    timedOut = true;
                    return false;
                }

                var bytesRead = readTask.Result;
                if (bytesRead <= 0)
                {
                    continue;
                }

                var actualReportId = rented[0];
                if (expectedReportId != 0 &&
                    actualReportId != expectedReportId &&
                    actualReportId != 0x00)
                {
                    continue;
                }

                var copyLength = Math.Max(minimumReportSize, bytesRead);
                var payload = new byte[copyLength];
                Buffer.BlockCopy(rented, 0, payload, 0, Math.Min(copyLength, bytesRead));

                if (payload[0] == 0x00 && expectedReportId != 0x00)
                {
                    payload[0] = expectedReportId;
                    actualReportId = expectedReportId;
                }

                HasCaptured = true;
                frame = new HidCapturedReportFrame(
                    ReportId: actualReportId,
                    Data: payload,
                    CapturedAt: DateTimeOffset.Now);
                return true;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch
            {
                // Try again within the timeout window.
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented, clearArray: true);
            }
        }

        timedOut = true;
        return false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _stream?.Dispose();
        }
        catch
        {
            // Ignore stream dispose failures.
        }

        try
        {
            _borrowedHandle.Dispose();
        }
        catch
        {
            // Ignore borrowed handle dispose failures.
        }

        HidGamepadAccess.ReleaseBorrowedHandle(_sourceHandle, _addRefAcquired);
        _disposed = true;
    }
}
