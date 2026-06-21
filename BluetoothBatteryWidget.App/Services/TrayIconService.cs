using DrawingIcon = System.Drawing.Icon;
using Forms = System.Windows.Forms;

namespace BluetoothBatteryWidget.App.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon _trayIcon;
    private readonly Forms.ToolStripMenuItem _openMenuItem;
    private readonly Forms.ToolStripMenuItem _refreshMenuItem;
    private readonly Forms.ToolStripMenuItem _resetPositionMenuItem;
    private readonly Forms.ToolStripMenuItem _autostartMenuItem;
    private readonly Forms.ToolStripMenuItem _startMinimizedToTrayMenuItem;
    private readonly Forms.ToolStripMenuItem _exitMenuItem;
    private bool _disposed;

    public TrayIconService(
        DrawingIcon icon,
        string appDisplayName,
        Action open,
        Action refresh,
        Action resetPosition,
        Action toggleAutostart,
        Action toggleStartMinimizedToTray,
        Action exit)
    {
        var contextMenu = new Forms.ContextMenuStrip();

        _openMenuItem = new Forms.ToolStripMenuItem();
        _openMenuItem.Click += (_, _) => open();
        contextMenu.Items.Add(_openMenuItem);

        _refreshMenuItem = new Forms.ToolStripMenuItem();
        _refreshMenuItem.Click += (_, _) => refresh();
        contextMenu.Items.Add(_refreshMenuItem);

        _resetPositionMenuItem = new Forms.ToolStripMenuItem();
        _resetPositionMenuItem.Click += (_, _) => resetPosition();
        contextMenu.Items.Add(_resetPositionMenuItem);

        contextMenu.Items.Add(new Forms.ToolStripSeparator());
        _autostartMenuItem = new Forms.ToolStripMenuItem();
        _autostartMenuItem.Click += (_, _) => toggleAutostart();
        contextMenu.Items.Add(_autostartMenuItem);

        _startMinimizedToTrayMenuItem = new Forms.ToolStripMenuItem();
        _startMinimizedToTrayMenuItem.Click += (_, _) => toggleStartMinimizedToTray();
        contextMenu.Items.Add(_startMinimizedToTrayMenuItem);

        contextMenu.Items.Add(new Forms.ToolStripSeparator());
        _exitMenuItem = new Forms.ToolStripMenuItem();
        _exitMenuItem.Click += (_, _) => exit();
        contextMenu.Items.Add(_exitMenuItem);

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = icon,
            Text = appDisplayName,
            Visible = true,
            ContextMenuStrip = contextMenu
        };
        _trayIcon.DoubleClick += (_, _) => open();
    }

    public void RefreshTexts(TrayIconTexts texts)
    {
        _openMenuItem.Text = texts.OpenWidget;
        _refreshMenuItem.Text = texts.RefreshNow;
        _resetPositionMenuItem.Text = texts.ResetPosition;
        _autostartMenuItem.Text = texts.Autostart;
        _autostartMenuItem.Checked = texts.AutostartEnabled;
        _startMinimizedToTrayMenuItem.Text = texts.StartMinimizedToTray;
        _startMinimizedToTrayMenuItem.Checked = texts.StartMinimizedToTrayEnabled;
        _exitMenuItem.Text = texts.Exit;
    }

    public void ShowNotification(string title, string message, Forms.ToolTipIcon icon)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _trayIcon.BalloonTipTitle = title;
            _trayIcon.BalloonTipText = message;
            _trayIcon.BalloonTipIcon = icon;
            _trayIcon.ShowBalloonTip(2500);
        }
        catch
        {
            // Ignore tray notification failures.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            _trayIcon.Visible = false;
        }
        catch
        {
            // Ignore tray visibility failures during shutdown.
        }

        try
        {
            _trayIcon.Dispose();
        }
        catch
        {
            // Ignore tray dispose failures during shutdown.
        }
    }
}

public sealed record TrayIconTexts(
    string OpenWidget,
    string RefreshNow,
    string ResetPosition,
    string Autostart,
    bool AutostartEnabled,
    string StartMinimizedToTray,
    bool StartMinimizedToTrayEnabled,
    string Exit);
