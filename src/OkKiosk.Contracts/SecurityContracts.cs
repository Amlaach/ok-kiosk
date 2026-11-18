namespace OkKiosk.Contracts;

public enum SecurityErrorCode
{
    None = 0,
    WrongPassword,
    LockedOut,
    NotConfigured,
    ServiceNotInstalled,
    AccessDenied,
    Cancelled,
    InvalidInput,
    UnsupportedInOpenStub,
    InternalError
}

public sealed record SecurityResult(bool Ok, SecurityErrorCode ErrorCode = SecurityErrorCode.None, string? Message = null)
{
    public static SecurityResult Success(string? message = null) => new(true, SecurityErrorCode.None, message);
    public static SecurityResult Fail(SecurityErrorCode code, string message) => new(false, code, message);
}

public sealed record AllowedProgram(
    string Id,
    string Name,
    string ExecutablePath,
    IReadOnlyList<string>? ExtraProcessNames = null,
    string? Arguments = null,
    string? Category = null);

public sealed record KioskLockOptions(
    IntPtr ShellWindowHandle,
    bool BlockTaskManager = true,
    bool KillExplorer = true,
    bool InstallKeyboardHook = true,
    bool EnforceForeground = true,
    bool StrictShell = false,
    IReadOnlyList<AllowedProgram>? AllowedPrograms = null);

public sealed record HardLockStatus(
    bool Installed,
    bool Running,
    bool Active,
    bool BlockUsbStorage,
    bool TempUnlockActive,
    int TempUnlockSecondsLeft,
    string? Version);

public interface IKioskGuard
{
    bool IsLocked { get; }
    void StartSoftLock(KioskLockOptions options);
    void StopSoftLock();
    void SetAllowedPrograms(IReadOnlyList<AllowedProgram> programs);
    bool IsProgramAllowed(string executablePath, int processId);
    void BringShellToFront(IntPtr shellHandle);
}

public interface IHardLockClient
{
    HardLockStatus GetStatus();
    SecurityResult InstallService();
    SecurityResult UninstallService(string passwordOrRecovery);
    SecurityResult Enable(string password, bool blockUsbStorage);
    SecurityResult Disable(string passwordOrRecovery);
    SecurityResult TempUnlock(string tempPassword, int minutes);
    SecurityResult EndTempUnlock();
}

public interface IPathPolicy
{
    bool IsProtectedSystemPath(string path);
    bool IsPathAllowed(string path, IReadOnlyList<string> allowedRoots);
}

public interface ISecretVault
{
    bool HasPasswordConfigured();
    SecurityResult SetPassword(string? oldPassword, string newPassword, out string? recoveryCode);
    SecurityResult VerifyPassword(string password);
    SecurityResult VerifyRecoveryCode(string code);
}

/// <summary>
/// Entry-point contract implemented by OkKiosk.Security.dll (private binary)
/// or OpenSecurityStub (open-source development fallback).
/// </summary>
public interface IKioskSecurityProvider
{
    bool IsHardenedBinary { get; }
    string ProviderVersion { get; }
    IKioskGuard Guard { get; }
    IHardLockClient HardLock { get; }
    IPathPolicy PathPolicy { get; }
    ISecretVault Vault { get; }
    int RunServiceMain(string[] args);
}
