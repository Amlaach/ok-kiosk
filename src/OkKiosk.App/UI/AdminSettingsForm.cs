using OkKiosk.App.Core;
using OkKiosk.Contracts;

namespace OkKiosk.App.UI;

public sealed class AdminSettingsForm : Form
{
    private readonly IKioskSecurityProvider _security;
    private readonly AppConfig _config;
    private readonly ListBox _programsList;
    private readonly CheckBox _chkTaskMgr;
    private readonly CheckBox _chkHook;
    private readonly CheckBox _chkExplorer;
    private readonly CheckBox _chkUsb;

    public AdminSettingsForm(IKioskSecurityProvider security, AppConfig config)
    {
        _security = security;
        _config = config;

        Text = "הגדרות מנהל — OK Kiosk";
        Size = new Size(640, 560);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        Font = new Font("Segoe UI", 9.5f);

        var tabs = new TabControl { Dock = DockStyle.Fill };

        // Tab 1: Allowed Programs
        var tabApps = new TabPage("תוכנות מאושרות") { Padding = new Padding(12) };
        _programsList = new ListBox { Dock = DockStyle.Fill };
        foreach (var p in _config.AllowedPrograms)
            _programsList.Items.Add($"{p.Name}  —  {p.ExecutablePath}");

        var appBtns = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44 };
        var addAppBtn = new Button { Text = "הוסף תוכנה (.exe)", AutoSize = true };
        addAppBtn.Click += (_, _) => AddProgram();
        var remAppBtn = new Button { Text = "הסר נבחרת", AutoSize = true };
        remAppBtn.Click += (_, _) => RemoveSelectedProgram();
        appBtns.Controls.Add(addAppBtn);
        appBtns.Controls.Add(remAppBtn);
        tabApps.Controls.Add(_programsList);
        tabApps.Controls.Add(appBtns);

        // Tab 2: Security & Password
        var tabSec = new TabPage("אבטחה והקשחה") { Padding = new Padding(16) };
        var secFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true
        };

        var pwdBtn = new Button { Text = "הגדר / שנה סיסמת מנהל", AutoSize = true, Padding = new Padding(8, 4, 8, 4) };
        pwdBtn.Click += (_, _) => ChangePassword();

        _chkTaskMgr = new CheckBox { Text = "חסימת מנהל המשימות (Task Manager)", Checked = _config.BlockTaskManager, AutoSize = true };
        _chkHook = new CheckBox { Text = "חסימת קיצורי מערכת (Win / Alt+Tab / Alt+F4)", Checked = _config.InstallKeyboardHook, AutoSize = true };
        _chkExplorer = new CheckBox { Text = "הסתרת שולחן עבודה ושורת משימות (explorer.exe)", Checked = _config.KillExplorer, AutoSize = true };
        _chkUsb = new CheckBox { Text = "חסימת התקני אחסון USB (בשירות מוקשח)", Checked = _config.BlockUsbStorage, AutoSize = true };

        var hardStatus = _security.HardLock.GetStatus();
        var hardLabel = new Label
        {
            Text = $"מודול אבטחה: {(_security.IsHardenedBinary ? "בינארי מוקשח פעיל" : "Stub קוד פתוח (ללא הקשחה)")} ({_security.ProviderVersion})\n" +
                   $"שירות SYSTEM: {(hardStatus.Installed ? (hardStatus.Running ? "מותקן ורץ" : "מותקן אך כבוי") : "לא מותקן")}",
            AutoSize = true,
            Padding = new Padding(0, 12, 0, 8)
        };

        var installSvcBtn = new Button { Text = "התקן שירות נעילה מוקשחת (UAC)", AutoSize = true, Padding = new Padding(8, 4, 8, 4) };
        installSvcBtn.Click += (_, _) =>
        {
            var r = _security.HardLock.InstallService();
            MessageBox.Show(this, r.Message ?? (r.Ok ? "השירות הותקן בהצלחה." : "התקנה נכשלה."), "OK Kiosk");
        };

        secFlow.Controls.AddRange([pwdBtn, _chkTaskMgr, _chkHook, _chkExplorer, _chkUsb, hardLabel, installSvcBtn]);
        tabSec.Controls.Add(secFlow);

        tabs.TabPages.Add(tabApps);
        tabs.TabPages.Add(tabSec);

        var closeBtn = new Button { Text = "שמור וסגור", Dock = DockStyle.Bottom, Height = 40, DialogResult = DialogResult.OK };
        closeBtn.Click += (_, _) =>
        {
            _config.BlockTaskManager = _chkTaskMgr.Checked;
            _config.InstallKeyboardHook = _chkHook.Checked;
            _config.KillExplorer = _chkExplorer.Checked;
            _config.BlockUsbStorage = _chkUsb.Checked;
        };

        Controls.Add(tabs);
        Controls.Add(closeBtn);
    }

    private void AddProgram()
    {
        using var ofd = new OpenFileDialog
        {
            Filter = "תוכנות (*.exe)|*.exe",
            Title = "בחר תוכנה מותרת"
        };
        if (ofd.ShowDialog(this) != DialogResult.OK) return;

        var name = Path.GetFileNameWithoutExtension(ofd.FileName);
        var prog = new AllowedProgram(Guid.NewGuid().ToString("N")[..8], name, ofd.FileName);
        _config.AllowedPrograms.Add(prog);
        _programsList.Items.Add($"{prog.Name}  —  {prog.ExecutablePath}");
    }

    private void RemoveSelectedProgram()
    {
        int idx = _programsList.SelectedIndex;
        if (idx < 0 || idx >= _config.AllowedPrograms.Count) return;
        _config.AllowedPrograms.RemoveAt(idx);
        _programsList.Items.RemoveAt(idx);
    }

    private void ChangePassword()
    {
        using var f = new Form
        {
            Text = "הגדרת סיסמת מנהל",
            Size = new Size(360, 210),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            RightToLeft = RightToLeft.Yes,
            RightToLeftLayout = true
        };
        var oldTb = new TextBox { PlaceholderText = "סיסמה נוכחית (אם קיימת)", UseSystemPasswordChar = true, Dock = DockStyle.Top };
        var newTb = new TextBox { PlaceholderText = "סיסמה חדשה (לפחות 4 תווים)", UseSystemPasswordChar = true, Dock = DockStyle.Top };
        var ok = new Button { Text = "שמור סיסמה", DialogResult = DialogResult.OK, Dock = DockStyle.Bottom, Height = 36 };
        f.Controls.Add(newTb);
        f.Controls.Add(oldTb);
        f.Controls.Add(ok);
        f.AcceptButton = ok;

        if (f.ShowDialog(this) != DialogResult.OK) return;

        var res = _security.Vault.SetPassword(
            string.IsNullOrEmpty(oldTb.Text) ? null : oldTb.Text,
            newTb.Text,
            out var recoveryCode);

        if (!res.Ok)
        {
            MessageBox.Show(this, res.Message ?? "שגיאה בהגדרת סיסמה.", "OK Kiosk", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var msg = recoveryCode is not null
            ? $"הסיסמה נשמרה.\n\nקוד שחזור חד-פעמי (שמור במקום בטוח!):\n{recoveryCode}"
            : "הסיסמה עודכנה בהצלחה.";
        MessageBox.Show(this, msg, "OK Kiosk", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
