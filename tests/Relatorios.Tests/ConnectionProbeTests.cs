using Relatorios.Infrastructure.Pcme;

namespace Relatorios.Tests;

public sealed class ConnectionProbeTests
{
    [Fact]
    public async Task FailedProbeDoesNotExposeConnectionSecrets()
    {
        const string password = "pcme-secret-test";
        const string database = "C:\\private\\pcme.fdb";
        var options = new ConnectionOptions("10.0.0.12", 3050, database, "operator", password, "WIN1252");
        var probe = new ConnectionProbe((_, _) => throw new InvalidOperationException(
            $"Failed connecting with {database} and password {password}"));

        var result = await probe.ProbeAsync(options, CancellationToken.None);

        Assert.False(result.Success);
        Assert.DoesNotContain(password, result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain(database, result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain(options.Server, result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TimedOutProbeReturnsSanitizedTimeoutMessage()
    {
        var options = new ConnectionOptions("host-secret", 3050, "db-secret", "user-secret", "password-secret", "WIN1252", 1);
        var probe = new ConnectionProbe(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return "never";
        });

        var result = await probe.ProbeAsync(options, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("tempo", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReportTransactionsAreReadOnly()
    {
        var behavior = ConnectionProbe.CreateReadOnlyTransactionOptions().TransactionBehavior;

        Assert.True((behavior & FirebirdSql.Data.FirebirdClient.FbTransactionBehavior.Read) != 0);
        Assert.False((behavior & FirebirdSql.Data.FirebirdClient.FbTransactionBehavior.Write) != 0);
    }
}