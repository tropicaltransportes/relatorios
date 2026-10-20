using System.Text.Json.Serialization;
using FirebirdSql.Data.FirebirdClient;

namespace Relatorios.Infrastructure.Pcme;

public sealed record ConnectionOptions
{
    public string Server { get; init; }
    public int Port { get; init; }
    public string Database { get; init; }
    public string User { get; init; }
    [JsonIgnore]
    public string Password { get; init; }
    public string Charset { get; init; }
    public int TimeoutSeconds { get; init; }

    public ConnectionOptions(string server, int port, string database, string user, string password,
        string charset = "WIN1252", int timeoutSeconds = 10)
    {
        Server = server;
        Port = port;
        Database = database;
        User = user;
        Password = password;
        Charset = charset;
        TimeoutSeconds = timeoutSeconds;
    }

    public FbConnectionStringBuilder ToBuilder()
    {
        if (string.IsNullOrWhiteSpace(Server) || string.IsNullOrWhiteSpace(Database) || string.IsNullOrWhiteSpace(User))
            throw new ArgumentException("Informe servidor, base e usuário.");
        if (Port is < 1 or > 65535 || TimeoutSeconds is < 1 or > 120)
            throw new ArgumentOutOfRangeException(nameof(Port));

        return new FbConnectionStringBuilder
        {
            DataSource = Server.Trim(),
            Port = Port,
            Database = Database.Trim(),
            UserID = User.Trim(),
            Password = Password,
            Charset = Charset.Trim(),
            ConnectionTimeout = TimeoutSeconds,
            Pooling = false,
            Dialect = 3
        };
    }
}