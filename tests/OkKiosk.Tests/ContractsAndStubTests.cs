using OkKiosk.App;
using OkKiosk.App.Core;
using OkKiosk.Contracts;
using Xunit;

namespace OkKiosk.Tests;

public class ContractsAndStubTests
{
    [Fact]
    public void SecurityLoader_WithoutBinary_FallsBackToOpenStub()
    {
        var provider = SecurityLoader.Load();
        Assert.NotNull(provider);
        // In open-source CI without OkKiosk.Security.dll, IsHardenedBinary is false
        Assert.NotNull(provider.Guard);
        Assert.NotNull(provider.HardLock);
        Assert.NotNull(provider.PathPolicy);
        Assert.NotNull(provider.Vault);
    }

    [Fact]
    public void StubVault_RejectsShortPassword_AndSetsValidPassword()
    {
        var provider = SecurityLoader.Load();
        if (provider.IsHardenedBinary) return;

        var bad = provider.Vault.SetPassword(null, "12", out _);
        Assert.False(bad.Ok);
        Assert.Equal(SecurityErrorCode.InvalidInput, bad.ErrorCode);

        var ok = provider.Vault.SetPassword(null, "1234", out var recovery);
        Assert.True(ok.Ok);
        Assert.False(string.IsNullOrWhiteSpace(recovery));
        Assert.True(provider.Vault.VerifyPassword("1234").Ok);
        Assert.False(provider.Vault.VerifyPassword("wrong").Ok);

        var backups = provider.Vault.GenerateBackupCodes("1234", 15);
        Assert.Equal(15, backups.Count);
        Assert.Equal(15, provider.Vault.GetRemainingBackupCodeCount());
        Assert.True(provider.Vault.VerifyAnyEmergencyCode(backups[0]).Ok);
        Assert.Equal(14, provider.Vault.GetRemainingBackupCodeCount());
        Assert.False(provider.Vault.VerifyAnyEmergencyCode(backups[0]).Ok);
    }

    [Fact]
    public void PathPolicy_BlocksProtectedSystemDirectories()
    {
        var provider = SecurityLoader.Load();
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        Assert.True(provider.PathPolicy.IsProtectedSystemPath(Path.Combine(winDir, "System32")));
    }

    [Fact]
    public void AppConfig_DefaultValuesAreValid()
    {
        var cfg = new AppConfig();
        Assert.Equal("OK Kiosk", cfg.KioskTitle);
        Assert.True(cfg.BlockTaskManager);
        Assert.Empty(cfg.AllowedPrograms);
    }
}
