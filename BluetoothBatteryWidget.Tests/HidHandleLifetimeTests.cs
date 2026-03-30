using BluetoothBatteryWidget.App.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class HidHandleLifetimeTests
{
    [Fact]
    public void BorrowedNonOwningHandle_DoesNotCloseOriginalHandle()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, [1, 2, 3, 4]);
            using var sourceStream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            var sourceHandle = sourceStream.SafeFileHandle;

            Assert.False(sourceHandle.IsClosed);
            Assert.True(HidGamepadAccess.TryBorrowNonOwningHandle(sourceHandle, out var borrowedHandle, out var addRefAcquired));

            try
            {
                using var borrowedStream = new FileStream(borrowedHandle, FileAccess.Read, 64, isAsync: false);
                var first = borrowedStream.ReadByte();
                Assert.True(first >= 0);
            }
            finally
            {
                HidGamepadAccess.ReleaseBorrowedHandle(sourceHandle, addRefAcquired);
            }

            Assert.False(sourceHandle.IsClosed);
            sourceStream.Position = sourceStream.Length;
            sourceStream.WriteByte(0x5A);
            sourceStream.Flush();
            Assert.True(sourceStream.Length >= 5);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
