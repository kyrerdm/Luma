using System;
using System.Drawing;
using System.Linq;
using Luma.Services;
using Luma.Themes;
using Forms = System.Windows.Forms;

namespace Luma;

public sealed partial class AppController
{
    private readonly UpdateMonitor updates;
    private readonly bool automaticUpdatesAllowed;
    private readonly UpdateNotification updateNotification;
    private Icon? updateTrayIcon;
    internal bool DiagnosticUpdateIndicator => updates.Current?.Available == true;
    internal Forms.NotifyIcon DiagnosticTray => tray;

    private void ConfigureAutomaticUpdates() => updates.SetEnabled(automaticUpdatesAllowed && settings.AutomaticUpdateChecks);

    private void OnUpdateChanged(UpdateResult result)
    {
        if (disposed) return;
        window.SetUpdateResult(result);
        RefreshUpdateIndicator();
    }

    private void OnBackgroundUpdateAvailable(UpdateResult result)
    {
        if (!disposed && automaticUpdatesAllowed && settings.AutomaticUpdateChecks) updateNotification.Notify(result);
    }

    private void ShowUpdateNotice(UpdateNotice notice) => tray.ShowBalloonTip(10000, notice.Title, notice.Body, Forms.ToolTipIcon.Info);

    internal void OnUpdateNoticeClicked(object? sender, EventArgs e)
    {
        if (disposed || updates.Current?.Available != true) return;
        ShowSettings();
        window.ShowUpdates();
    }

    private void RefreshUpdateIndicator()
    {
        var available = updates.Current?.Available == true;
        if (available) updateTrayIcon ??= BrandIcon.CreateTrayIcon(updateAvailable: true);
        tray.Icon = available ? updateTrayIcon : trayIcon;
        tray.Text = available ? "Luma · " + Strings.T("Güncelleme var") : "Luma";
        var menu = (DarkTrayMenu)tray.ContextMenuStrip!;
        var action = menu.Items.Cast<Forms.ToolStripItem>().FirstOrDefault(item => item.Tag is TrayAction.Update);
        if (available && action is null)
            menu.AddAction(TrayAction.Update, Strings.T("Güncelleme var"), (_, _) => { ShowSettings(); window.ShowUpdates(); }, 1);
        else if (!available && action is not null)
        {
            menu.Items.Remove(action);
            action.Dispose();
        }
    }
}
