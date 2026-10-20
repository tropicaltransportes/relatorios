using System.Security.Cryptography;
using System.Text.Json;
using Relatorios.Infrastructure.Pcme;

namespace Relatorios.Infrastructure.Storage;

public sealed class ConnectionSettingsVault
{
    private readonly string filePath;

    public ConnectionSettingsVault(string? root = null)
    {
        var dataRoot = root ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tropical", "Relatorios");
        filePath = Path.Combine(dataRoot, "connection.dpapi");
    }

    public async Task SaveAsync(ConnectionOptions options, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new SavedConnection(
            options.Server, options.Port, options.Database, options.User, options.Password, options.Charset, options.TimeoutSeconds));
        var protectedPayload = ProtectedData.Protect(payload, null, DataProtectionScope.CurrentUser);
        var directory = Path.GetDirectoryName(filePath)!;
        Directory.CreateDirectory(directory);
        var temp = Path.Combine(directory, $"connection.{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllBytesAsync(temp, protectedPayload, cancellationToken);
            File.Move(temp, filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
            CryptographicOperations.ZeroMemory(payload);
        }
    }

    public async Task<ConnectionOptions?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath)) return null;
        var protectedPayload = await File.ReadAllBytesAsync(filePath, cancellationToken);
        byte[]? payload = null;
        try
        {
            payload = ProtectedData.Unprotect(protectedPayload, null, DataProtectionScope.CurrentUser);
            var saved = JsonSerializer.Deserialize<SavedConnection>(payload)
                ?? throw new InvalidDataException("A configuração local está vazia.");
            return new ConnectionOptions(saved.Server, saved.Port, saved.Database, saved.User,
                saved.Password, saved.Charset, saved.TimeoutSeconds);
        }
        catch (CryptographicException)
        {
            throw new InvalidDataException("Não foi possível ler a configuração protegida deste usuário Windows.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protectedPayload);
            if (payload is not null) CryptographicOperations.ZeroMemory(payload);
        }
    }

    private sealed record SavedConnection(
        string Server, int Port, string Database, string User, string Password, string Charset, int TimeoutSeconds);
}