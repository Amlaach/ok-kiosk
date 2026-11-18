using OkKiosk.App.Core;
using OkKiosk.Contracts;

namespace OkKiosk.App.UI;

public sealed class TrayManager : IDisposable
{
    private readonly NotifyIcon _icon;

    public TrayManager(Form mainForm, IKioskSecurityProvider security, AppConfig config)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("פתח חלון קיוסק", null, (_, _) =>
        {
            mainForm.Show();
            if (mainForm.WindowState == FormWindowState.Minimized)
                mainForm.WindowState = FormWindowState.Normal;
            mainForm.Activate();
        });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("יציאה", null, (_, _) =>
        {
            if (security.Guard.IsLocked)
            {
                MessageBox.Show("לא ניתן לצאת בזמן שהקיוסק נעול.", "OK Kiosk",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Application.Exit();
        });

        _icon = new NotifyIcon
        {
            Text = config.KioskTitle,
            Icon = SystemIcons.Shield,
            ContextMenuStrip = menu,
            Visible = true
        };
        _icon.DoubleClick += (_, _) => mainForm.Activate();
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
