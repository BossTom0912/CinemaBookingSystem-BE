using System.Net.Sockets;
using System.Text;
using CinemaSystem.Application.Interfaces;

namespace CinemaSystem.Infrastructure.Services;

public sealed class RedisSeatLockStore : ISeatLockStore
{
    private readonly RedisEndpoint _endpoint;

    public RedisSeatLockStore(string connectionString)
    {
        _endpoint = RedisEndpoint.Parse(connectionString);
    }

    public async Task<bool> TryLockAsync(
        string lockKey,
        string userId,
        TimeSpan ttl,
        CancellationToken cancellationToken)
    {
        var response = await ExecuteAsync(
            cancellationToken,
            "SET",
            lockKey,
            userId,
            "EX",
            ((int)ttl.TotalSeconds).ToString(),
            "NX");

        return string.Equals(response, "+OK", StringComparison.OrdinalIgnoreCase);
    }

    public async Task ReleaseAsync(
        string lockKey,
        CancellationToken cancellationToken)
    {
        await ExecuteAsync(cancellationToken, "DEL", lockKey);
    }

    private async Task<string> ExecuteAsync(
        CancellationToken cancellationToken,
        params string[] args)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(_endpoint.Host, _endpoint.Port, cancellationToken);
        await using var stream = client.GetStream();

        if (!string.IsNullOrWhiteSpace(_endpoint.Password))
        {
            await WriteCommandAsync(stream, cancellationToken, "AUTH", _endpoint.Password);
            await ReadLineAsync(stream, cancellationToken);
        }

        await WriteCommandAsync(stream, cancellationToken, args);
        return await ReadLineAsync(stream, cancellationToken);
    }

    private static async Task WriteCommandAsync(
        NetworkStream stream,
        CancellationToken cancellationToken,
        params string[] args)
    {
        var builder = new StringBuilder();
        builder.Append('*').Append(args.Length).Append("\r\n");
        foreach (var arg in args)
        {
            var bytes = Encoding.UTF8.GetByteCount(arg);
            builder.Append('$').Append(bytes).Append("\r\n");
            builder.Append(arg).Append("\r\n");
        }

        var payload = Encoding.UTF8.GetBytes(builder.ToString());
        await stream.WriteAsync(payload, cancellationToken);
    }

    private static async Task<string> ReadLineAsync(
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        var bytes = new List<byte>();
        var buffer = new byte[1];

        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            if (buffer[0] == '\n')
            {
                break;
            }

            if (buffer[0] != '\r')
            {
                bytes.Add(buffer[0]);
            }
        }

        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    private sealed record RedisEndpoint(string Host, int Port, string? Password)
    {
        public static RedisEndpoint Parse(string connectionString)
        {
            var hostPort = connectionString.Split(',', StringSplitOptions.RemoveEmptyEntries)[0];
            var password = connectionString
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Trim())
                .FirstOrDefault(part => part.StartsWith("password=", StringComparison.OrdinalIgnoreCase))
                ?.Split('=', 2)[1];

            var pieces = hostPort.Split(':', 2);
            var host = pieces[0].Trim();
            var port = pieces.Length == 2 && int.TryParse(pieces[1], out var parsedPort)
                ? parsedPort
                : 6379;

            return new RedisEndpoint(host, port, password);
        }
    }
}
