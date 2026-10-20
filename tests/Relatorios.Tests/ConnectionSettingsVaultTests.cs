using Relatorios.Infrastructure.Pcme;
using Relatorios.Infrastructure.Storage;

namespace Relatorios.Tests;

public sealed class ConnectionSettingsVaultTests
{
    [Fact]
    public async Task SavedCredentialsAreProtectedAndCanBeReadBySameWindowsProfile()
    {
        var root = Path.Combine(Path.GetTempPath(), "relatorios-tests", Guid.NewGuid().ToString("N"));
        const string password = "pcme-secret-test";
        try
        {
            var vault = new ConnectionSettingsVault(root);
            var options = new ConnectionOptions("server", 3050, "database", "operator", password);
            await vault.SaveAsync(options, CancellationToken.None);

            var storedBytes = await File.ReadAllBytesAsync(Path.Combine(root, "connection.dpapi"));
            Assert.DoesNotContain(password, System.Text.Encoding.UTF8.GetString(storedBytes), StringComparison.Ordinal);
            Assert.Equal(password, (await vault.LoadAsync(CancellationToken.None))?.Password);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}