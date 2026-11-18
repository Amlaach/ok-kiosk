using System.Diagnostics;
using OkKiosk.App.Core;
using OkKiosk.Contracts;

namespace OkKiosk.App.UI;

public sealed class KioskLauncherForm : Form
{
    private readonly IKioskSecurityProvider _security;
    private readonly AppConfig _config;
    private readonly FlowLayoutPanel _appsPanel;
    private readonly Label _clockLabel;
    private readonly Label _statusBadge;
    private readonly Button _lockToggleBtn;
    private readonly System.Windows.Forms.Timer _clockTimer;

    public KioskLauncherForm(IKioskSecurityProvider security, AppConfig config)
    {
        _security = security;
        _config = config;

        Text = _config.KioskTitle;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        MinimumSize = new Size(900, 600);
        Size = new Size(1100, 720);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(18, 22, 30);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10f);

        // Top header bar
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 68,
            BackColor = Color.FromArgb(26, 32, 44),
            Padding = new Padding(20, 12, 20, 12)
        };

        var titleLabel = new Label
        {
            Text = _config.KioskTitle,
            Font = new Font("Segoe UI", 16f, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Dock = DockStyle.Right,
            TextAlign = ContentAlignment.MiddleRight
        };

        _statusBadge = new Label
        {
            AutoSize = true,
            Padding = new Padding(10, 5, 10, 5),
            Margin = new Padding(12, 8, 12, 8),
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Dock = DockStyle.Right
        };

        _clockLabel = new Label
        {
            Font = new Font("Segoe UI", 13f, FontStyle.Regular),
            ForeColor = Color.FromArgb(203, 213, 225),
            AutoSize = true,
            Dock = DockStyle.Left,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var controlsFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Left,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };

        var adminBtn = new Button
        {
            Text = "הגדרות מנהל",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(51, 65, 85),
            ForeColor = Color.White,
            Padding = new Padding(12, 6, 12, 6),
            Margin = new Padding(6, 4, 6, 4),
            Cursor = Cursors.Hand
        };
        adminBtn.FlatAppearance.BorderSize = 0;
        adminBtn.Click += (_, _) => OpenAdminSettings();

        _lockToggleBtn = new Button
        {
            Text = "הפעל נעילת קיוסק",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            Padding = new Padding(12, 6, 12, 6),
            Margin = new Padding(6, 4, 6, 4),
            Cursor = Cursors.Hand
        };
        _lockToggleBtn.FlatAppearance.BorderSize = 0;
        _lockToggleBtn.Click += (_, _) => ToggleKioskLock();

        controlsFlow.Controls.Add(adminBtn);
        controlsFlow.Controls.Add(_lockToggleBtn);

        header.Controls.Add(titleLabel);
        header.Controls.Add(_statusBadge);
        header.Controls.Add(controlsFlow);
        header.Controls.Add(_clockLabel);

        // Main apps grid
        _appsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(32),
            BackColor = Color.FromArgb(18, 22, 30)
        };

        Controls.Add(_appsPanel);
        Controls.Add(header);

        _clockTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _clockTimer.Tick += (_, _) => UpdateClock();
        _clockTimer.Start();
        _security.Guard.EmergencyHotkeyPressed += OnEmergencyHotkey;
        UpdateClock();
        RefreshSecurityBadge();
        RefreshAppsGrid();

        if (_config.AutoLockOnStart && _security.Vault.HasPasswordConfigured())
            ApplyLockState(true);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_security.Guard.IsLocked && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            MessageBox.Show(this, "הקיוסק נעול. יש לבטל נעילה עם סיסמת מנהל.", "OK Kiosk",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        _clockTimer.Dispose();
        base.OnFormClosing(e);
    }

    private void UpdateClock()
    {
        _clockLabel.Text = DateTime.Now.ToString("HH:mm:ss  |  dd/MM/yyyy");
    }

    private void RefreshSecurityBadge()
    {
        if (_security.IsHardenedBinary)
        {
            _statusBadge.Text = _security.Guard.IsLocked ? "נעול (מוקשח)" : "מצב מוקשח זמין";
            _statusBadge.BackColor = _security.Guard.IsLocked
                ? Color.FromArgb(185, 28, 28)
                : Color.FromArgb(21, 128, 61);
        }
        else
        {
            _statusBadge.Text = "מצב קוד פתוח (Stub)";
            _statusBadge.BackColor = Color.FromArgb(180, 83, 9);
        }
        _lockToggleBtn.Text = _security.Guard.IsLocked ? "שחרור נעילה" : "הפעל נעילת קיוסק";
        _lockToggleBtn.BackColor = _security.Guard.IsLocked
            ? Color.FromArgb(185, 28, 28)
            : Color.FromArgb(37, 99, 235);
    }

    public void RefreshAppsGrid()
    {
        _appsPanel.Controls.Clear();

        if (_config.AllowedPrograms.Count == 0)
        {
            var empty = new Label
            {
                Text = "אין עדיין תוכנות מאושרות.\nלחץ על 'הגדרות מנהל' כדי להוסיף תוכנות לשימוש בקיוסק.",
                ForeColor = Color.FromArgb(148, 163, 184),
                Font = new Font("Segoe UI", 12f),
                AutoSize = true,
                Padding = new Padding(20)
            };
            _appsPanel.Controls.Add(empty);
            return;
        }

        foreach (var app in _config.AllowedPrograms)
        {
            var tile = CreateAppTile(app);
            _appsPanel.Controls.Add(tile);
        }
    }

    private Control CreateAppTile(AllowedProgram app)
    {
        var card = new Button
        {
            Width = 170,
            Height = 140,
            Margin = new Padding(12),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(30, 41, 59),
            ForeColor = Color.White,
            Text = app.Name,
            TextAlign = ContentAlignment.BottomCenter,
            ImageAlign = ContentAlignment.TopCenter,
            Padding = new Padding(8, 16, 8, 12),
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        card.FlatAppearance.BorderColor = Color.FromArgb(51, 65, 85);
        card.FlatAppearance.BorderSize = 1;

        try
        {
            if (File.Exists(app.ExecutablePath))
            {
                using var ico = Icon.ExtractAssociatedIcon(app.ExecutablePath);
                if (ico is not null)
                    card.Image = new Bitmap(ico.ToBitmap(), new Size(48, 48));
            }
        }
        catch
        {
            // Ignore icon extraction errors
        }

        card.Click += (_, _) => LaunchAllowedApp(app);
        return card;
    }

    private void LaunchAllowedApp(AllowedProgram app)
    {
        if (!File.Exists(app.ExecutablePath))
        {
            MessageBox.Show(this, $"הקובץ לא נמצא:\n{app.ExecutablePath}", "OK Kiosk",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = app.ExecutablePath,
                Arguments = app.Arguments ?? string.Empty,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"שגיאה בהפעלת {app.Name}:\n{ex.Message}", "OK Kiosk",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ToggleKioskLock()
    {
        if (!_security.Vault.HasPasswordConfigured())
        {
            MessageBox.Show(this, "יש להגדיר סיסמת מנהל לפני הפעלת הנעילה.", "OK Kiosk",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            OpenAdminSettings();
            return;
        }

        if (_security.Guard.IsLocked)
        {
            var pwd = PromptPassword("הזן סיסמת מנהל (או קוד שחזור/גיבוי) לשחרור הנעילה:");
            if (pwd is null) return;

            var res = _security.Vault.VerifyPassword(pwd);
            if (!res.Ok)
            {
                var emergencyRes = _security.Vault.VerifyAnyEmergencyCode(pwd);
                if (!emergencyRes.Ok)
                {
                    MessageBox.Show(this, res.Message ?? "סיסמה או קוד שגויים.", "OK Kiosk",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }
            ApplyLockState(false);
        }
        else
        {
            ApplyLockState(true);
        }
    }

    private void OnEmergencyHotkey()
    {
        if (IsDisposed) return;
        BeginInvoke(() =>
        {
            if (!_security.Guard.IsLocked) return;
            var code = PromptPassword("שחרור חירום (Ctrl+Alt+Shift+F12) — הזן קוד שחזור ראשי או קוד גיבוי חד-פעמי:");
            if (string.IsNullOrWhiteSpace(code)) return;

            var res = _security.Vault.VerifyAnyEmergencyCode(code);
            if (!res.Ok)
            {
                MessageBox.Show(this, res.Message ?? "קוד שגוי.", "שחרור חירום — OK Kiosk",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            ApplyLockState(false);
            MessageBox.Show(this, "הקיוסק שוחרר באמצעות קוד שחזור.", "OK Kiosk",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
    }

    private void ApplyLockState(bool lockOn)
    {
        if (lockOn)
        {
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            _security.Guard.SetAllowedPrograms(_config.AllowedPrograms);
            _security.Guard.StartSoftLock(new KioskLockOptions(
                Handle,
                _config.BlockTaskManager,
                _config.KillExplorer,
                _config.InstallKeyboardHook,
                _config.EnforceForeground,
                _config.StrictShell,
                _config.AllowedPrograms));
        }
        else
        {
            _security.Guard.StopSoftLock();
            FormBorderStyle = FormBorderStyle.Sizable;
            WindowState = FormWindowState.Normal;
        }
        RefreshSecurityBadge();
    }

    private void OpenAdminSettings()
    {
        if (_security.Vault.HasPasswordConfigured())
        {
            var pwd = PromptPassword("הזן סיסמת מנהל לכניסה להגדרות:");
            if (pwd is null) return;
            var res = _security.Vault.VerifyPassword(pwd);
            if (!res.Ok)
            {
                MessageBox.Show(this, res.Message ?? "סיסמה שגויה.", "OK Kiosk",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }

        using var dlg = new AdminSettingsForm(_security, _config);
        dlg.ShowDialog(this);
        _config.Save();
        _security.Guard.SetAllowedPrograms(_config.AllowedPrograms);
        RefreshAppsGrid();
        RefreshSecurityBadge();
    }

    private static string? PromptPassword(string prompt)
    {
        using var form = new Form
        {
            Text = "אימות מנהל",
            Size = new Size(380, 170),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false,
            RightToLeft = RightToLeft.Yes,
            RightToLeftLayout = true
        };
        var lbl = new Label { Text = prompt, Dock = DockStyle.Top, Padding = new Padding(12, 12, 12, 4), AutoSize = true };
        var tb = new TextBox { UseSystemPasswordChar = true, Dock = DockStyle.Top, Margin = new Padding(12) };
        var ok = new Button { Text = "אישור", DialogResult = DialogResult.OK, Dock = DockStyle.Bottom, Height = 36 };
        form.Controls.Add(tb);
        form.Controls.Add(lbl);
        form.Controls.Add(ok);
        form.AcceptButton = ok;
        return form.ShowDialog() == DialogResult.OK ? tb.Text : null;
    }
}
