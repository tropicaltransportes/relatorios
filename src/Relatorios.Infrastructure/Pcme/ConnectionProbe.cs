using FirebirdSql.Data.FirebirdClient;

namespace Relatorios.Infrastructure.Pcme;

public sealed record ConnectionProbeResult(bool Success, string? ServerVersion, string? ProviderVersion, string Error);

public sealed class ConnectionProbe(Func<ConnectionOptions, CancellationToken, Task<string>> readVersion)
{
    private static readonly Version MinimumSupportedServerVersion = new(2, 5);
    private const int MaximumSupportedServerMajor = 5;

    public async Task<ConnectionProbeResult> ProbeAsync(ConnectionOptions options, CancellationToken cancellationToken)
    {
        if (options.TimeoutSeconds is < 1 or > 120)
            return new(false, null, null, "O tempo limite configurado é inválido.");

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.TimeoutSeconds));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            var version = await readVersion(options, linked.Token).WaitAsync(linked.Token);
            if (!TryParseServerVersion(version, out var serverVersion))
                return new(false, version, ProviderVersion, "A versão do servidor não foi reconhecida; a compatibilidade não pôde ser confirmada.");
            if (serverVersion < MinimumSupportedServerVersion || serverVersion.Major > MaximumSupportedServerMajor)
                return new(false, version, ProviderVersion, "A versão do Firebird está fora da faixa compatível declarada para este provider.");
            return new(true, version, ProviderVersion, string.Empty);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(false, null, ProviderVersion, "Tempo limite excedido ao testar a conexão.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return new(false, null, ProviderVersion, "Não foi possível conectar ao PCME. Confira a rede e os dados informados.");
        }
    }

    public static async Task<string> ReadServerVersionAsync(ConnectionOptions options, CancellationToken cancellationToken)
    {
        await using var connection = new FbConnection(options.ToBuilder().ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(CreateReadOnlyTransactionOptions(), cancellationToken);
        await using var command = new FbCommand(
            "SELECT RDB$GET_CONTEXT('SYSTEM', 'ENGINE_VERSION') FROM RDB$DATABASE", connection, transaction)
        {
            CommandTimeout = options.TimeoutSeconds
        };
        var result = await command.ExecuteScalarAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Convert.ToString(result, System.Globalization.CultureInfo.InvariantCulture)
            ?? throw new InvalidOperationException("Versão do servidor não retornada.");
    }

    public static FbTransactionOptions CreateReadOnlyTransactionOptions() => new()
    {
        TransactionBehavior = FbTransactionBehavior.Read | FbTransactionBehavior.Concurrency | FbTransactionBehavior.Wait
    };

    private static string ProviderVersion => typeof(FbConnection).Assembly.GetName().Version?.ToString() ?? "desconhecida";

    private static bool TryParseServerVersion(string value, out Version version)
    {
        var match = System.Text.RegularExpressions.Regex.Match(value, @"(?<!\d)(\d+)\.(\d+)(?:\.(\d+))?");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var major) &&
            int.TryParse(match.Groups[2].Value, out var minor))
        {
            var build = match.Groups[3].Success && int.TryParse(match.Groups[3].Value, out var parsedBuild) ? parsedBuild : 0;
            version = new Version(major, minor, build);
            return true;
        }
        version = new Version(0, 0);
        return false;
    }
}