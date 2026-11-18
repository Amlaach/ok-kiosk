using System.Reflection;
using OkKiosk.Contracts;

namespace OkKiosk.App;

/// <summary>
/// Loads the closed-source hardened security binary (OkKiosk.Security.dll) if present,
/// or falls back to OpenSecurityStub so open-source contributors can build and test the UI.
/// </summary>
public static class SecurityLoader
{
    private const string SecurityAssemblyFile = "OkKiosk.Security.dll";
    private const string ProviderTypeName = "OkKiosk.Security.OkKioskSecurityProvider";

    public static IKioskSecurityProvider Load()
    {
        string[] candidatePaths =
        [
            Path.Combine(AppContext.BaseDirectory, SecurityAssemblyFile),
            Path.Combine(AppContext.BaseDirectory, "vendor", "security", SecurityAssemblyFile)
        ];

        foreach (var path in candidatePaths)
        {
            if (!File.Exists(path)) continue;
            try
            {
                var asm = Assembly.LoadFrom(path);
                var type = asm.GetType(ProviderTypeName);
                if (type is not null && typeof(IKioskSecurityProvider).IsAssignableFrom(type))
                {
                    if (Activator.CreateInstance(type) is IKioskSecurityProvider provider)
                        return provider;
                }
            }
            catch
            {
                // If loading the hardened binary fails, fall through to stub
            }
        }

        return new OpenSecurityStub();
    }
}

/// <summary>
/// Open-source development stub. Provides non-hardened UI/UX simulation when
/// OkKiosk.Security.dll is not bundled.
/// </summary>
internal sealed class OpenSecurityStub : IKioskSecurityProvider
{
    public bool IsHardenedBinary => false;
    public string ProviderVersion => "open-stub-1.0.0";
    public IKioskGuard Guard { get; } = new StubGuard();
    public IHardLockClient HardLock { get; } = new StubHardLockClient();
    public IPathPolicy PathPolicy { get; } = new StubPathPolicy();
    public ISecretVault Vault { get; } = new StubSecretVault();

    public int RunServiceMain(string[] args) => 1;

    private sealed class StubGuard : IKioskGuard
    {
        private IReadOnlyList<AllowedProgram> _programs = Array.Empty<AllowedProgram>();
        public bool IsLocked { get; private set; }
        public event Action? EmergencyHotkeyPressed { add { } remove { } }

        public void StartSoftLock(KioskLockOptions options) => IsLocked = true;
        public void StopSoftLock() => IsLocked = false;
        public void SetAllowedPrograms(IReadOnlyList<AllowedProgram> programs) => _programs = programs;
        public bool IsProgramAllowed(string executablePath, int processId) =>
            _programs.Any(p => string.Equals(p.ExecutablePath, executablePath, StringComparison.OrdinalIgnoreCase));
        public void BringShellToFront(IntPtr shellHandle) { }
    }

    private sealed class StubHardLockClient : IHardLockClient
    {
        public HardLockStatus GetStatus() => new(false, false, false, false, false, 0, "stub");
        public SecurityResult InstallService() =>
            SecurityResult.Fail(SecurityErrorCode.UnsupportedInOpenStub, "שירות ההקשחה זמין רק בגרסה עם הבינארי המאובטח.");
        public SecurityResult UninstallService(string passwordOrRecovery) =>
            SecurityResult.Fail(SecurityErrorCode.UnsupportedInOpenStub, "לא מותקן בגרסת קוד פתוח בסיסית.");
        public SecurityResult Enable(string password, bool blockUsbStorage) =>
            SecurityResult.Fail(SecurityErrorCode.UnsupportedInOpenStub, "זמין רק עם OkKiosk.Security.dll.");
        public SecurityResult Disable(string passwordOrRecovery) => SecurityResult.Success();
        public SecurityResult TempUnlock(string tempPassword, int minutes) =>
            SecurityResult.Fail(SecurityErrorCode.UnsupportedInOpenStub, "זמין רק עם OkKiosk.Security.dll.");
        public SecurityResult EndTempUnlock() => SecurityResult.Success();
    }

    private sealed class StubPathPolicy : IPathPolicy
    {
        public bool IsProtectedSystemPath(string path)
        {
            var full = Path.GetFullPath(path);
            var win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            return full.StartsWith(win, StringComparison.OrdinalIgnoreCase)
                || full.StartsWith(pf, StringComparison.OrdinalIgnoreCase);
        }

        public bool IsPathAllowed(string path, IReadOnlyList<string> allowedRoots)
        {
            if (allowedRoots.Count == 0) return false;
            var full = Path.GetFullPath(path);
            return allowedRoots.Any(r => full.StartsWith(Path.GetFullPath(r), StringComparison.OrdinalIgnoreCase));
        }
    }

    private sealed class StubSecretVault : ISecretVault
    {
        private string? _devPassword;
        private string? _devRecovery;
        private readonly List<string> _devBackupCodes = new();

        public bool HasPasswordConfigured() => !string.IsNullOrEmpty(_devPassword);

        public SecurityResult SetPassword(string? oldPassword, string newPassword, out string? recoveryCode)
        {
            recoveryCode = null;
            if (!string.IsNullOrEmpty(_devPassword) && _devPassword != oldPassword)
                return SecurityResult.Fail(SecurityErrorCode.WrongPassword, "סיסמה שגויה.");
            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 4)
                return SecurityResult.Fail(SecurityErrorCode.InvalidInput, "סיסמה חייבת להכיל לפחות 4 תווים.");

            _devPassword = newPassword;
            _devRecovery ??= "DEV-0000-STUB";
            recoveryCode = _devRecovery;
            return SecurityResult.Success();
        }

        public SecurityResult VerifyPassword(string password) =>
            _devPassword == password
                ? SecurityResult.Success()
                : SecurityResult.Fail(SecurityErrorCode.WrongPassword, "סיסמה שגויה.");

        public SecurityResult VerifyRecoveryCode(string code) =>
            _devRecovery == code
                ? SecurityResult.Success()
                : SecurityResult.Fail(SecurityErrorCode.WrongPassword, "קוד שחזור שגוי.");

        public IReadOnlyList<string> GenerateBackupCodes(string password, int count = 15)
        {
            if (_devPassword != password)
                throw new UnauthorizedAccessException("סיסמה שגויה.");
            _devBackupCodes.Clear();
            for (int i = 1; i <= count; i++)
                _devBackupCodes.Add($"STUB-{i:0000}-CODE");
            return _devBackupCodes.ToList();
        }

        public int GetRemainingBackupCodeCount() => _devBackupCodes.Count;

        public SecurityResult VerifyBackupCode(string code)
        {
            int idx = _devBackupCodes.FindIndex(c => string.Equals(c, code.Trim(), StringComparison.OrdinalIgnoreCase));
            if (idx < 0)
                return SecurityResult.Fail(SecurityErrorCode.WrongPassword, "קוד גיבוי שגוי.");
            _devBackupCodes.RemoveAt(idx);
            return SecurityResult.Success();
        }

        public SecurityResult VerifyAnyEmergencyCode(string code)
        {
            var r = VerifyRecoveryCode(code);
            return r.Ok ? r : VerifyBackupCode(code);
        }
    }
}
