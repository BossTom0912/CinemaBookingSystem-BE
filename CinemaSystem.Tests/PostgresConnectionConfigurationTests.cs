using CinemaSystem.Infrastructure.Extensions;
using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CinemaSystem.Tests;

public sealed class PostgresConnectionConfigurationTests
{
    [Fact]
    public void AddInfrastructureServices_RejectsMissingConnectionString()
    {
        var configuration = BuildConfiguration(null);
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddInfrastructureServices(configuration));

        Assert.Contains("Missing PostgreSQL connection string", exception.Message);
        Assert.Contains("ConnectionStrings__DefaultConnection", exception.Message);
    }

    [Fact]
    public void AddInfrastructureServices_RejectsHostOnlyConnectionString()
    {
        const string partialConnection = "Host=example.neon.tech;Port=5432";
        var configuration = BuildConfiguration(partialConnection);
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddInfrastructureServices(configuration));

        Assert.Contains("incomplete", exception.Message);
        Assert.Contains("Database", exception.Message);
        Assert.Contains("Username", exception.Message);
        Assert.Contains("Password or Passfile", exception.Message);
        Assert.DoesNotContain("example.neon.tech", exception.Message);
    }

    [Fact]
    public void AddInfrastructureServices_RejectsMalformedConnectionStringWithoutEchoingIt()
    {
        const string malformedConnection =
            "Host=example.neon.tech;Database=neondb;Username=test;Password=fake-secret;Broken";
        var configuration = BuildConfiguration(malformedConnection);
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddInfrastructureServices(configuration));

        Assert.Contains("is invalid", exception.Message);
        Assert.DoesNotContain("fake-secret", exception.ToString());
    }

    [Fact]
    public void AddInfrastructureServices_PreservesCompleteConnectionAndRequiresSsl()
    {
        const string completeConnection =
            "Host=example.neon.tech;Port=5432;Database=neondb;" +
            "Username=neondb_owner;Password=fake-secret;SSL Mode=Disable;" +
            "Channel Binding=Prefer";
        var configuration = BuildConfiguration(completeConnection);
        var services = new ServiceCollection();

        services.AddInfrastructureServices(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var actualConnection = new NpgsqlConnectionStringBuilder(
            dbContext.Database.GetConnectionString());

        Assert.Equal("example.neon.tech", actualConnection.Host);
        Assert.Equal("neondb", actualConnection.Database);
        Assert.Equal("neondb_owner", actualConnection.Username);
        Assert.Equal(SslMode.Require, actualConnection.SslMode);
        Assert.Equal(ChannelBinding.Prefer, actualConnection.ChannelBinding);
    }

    private static IConfiguration BuildConfiguration(string? connectionString)
    {
        var values = new Dictionary<string, string?>();
        if (connectionString is not null)
        {
            values["ConnectionStrings:DefaultConnection"] = connectionString;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
