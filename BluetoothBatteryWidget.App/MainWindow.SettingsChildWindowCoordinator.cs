using System.Windows;

namespace BluetoothBatteryWidget.App;

public partial class MainWindow
{
    private void ShowSettingsChildWindowAbovePopup(Window dialog)
    {
        if (_viewModel.UseCenteredSettingsPopup && SettingsPopup.IsOpen)
        {
            dialog.Topmost = true;
        }

        dialog.Show();
        dialog.Activate();
        dialog.Focus();
    }

    private bool ShowOpenFileDialogAboveCenteredSettingsPopup(Microsoft.Win32.OpenFileDialog dialog)
    {
        var shouldRestoreSettingsPopup = SuspendCenteredSettingsPopupForExternalWindow();
        try
        {
            return dialog.ShowDialog(this) == true;
        }
        finally
        {
            RestoreCenteredSettingsPopupAfterExternalWindow(shouldRestoreSettingsPopup);
        }
    }

    private bool SuspendCenteredSettingsPopupForExternalWindow()
    {
        if (!_viewModel.UseCenteredSettingsPopup || !SettingsPopup.IsOpen)
        {
            return false;
        }

        FinishPopupDrag();
        SettingsPopup.IsOpen = false;
        return true;
    }

    private void RestoreCenteredSettingsPopupAfterExternalWindow(bool shouldRestore)
    {
        if (!shouldRestore || _isExiting)
        {
            return;
        }

        UpdateSettingsPopupLayout();
        SettingsPopup.IsOpen = true;
        Dispatcher.BeginInvoke(
            new Action(UpdateSettingsPopupLayout),
            System.Windows.Threading.DispatcherPriority.Loaded);
    }
}
