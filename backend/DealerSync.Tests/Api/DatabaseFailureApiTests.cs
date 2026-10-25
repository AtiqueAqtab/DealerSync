using System.Diagnostics;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using DealerSync.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace DealerSync.Tests.Api;

public class DatabaseFailureApiTests
{
    // Deliberately unreachable loopback port; never a development or dealership database.
    private const string Unavailable = "Host=127.0.0.1;Port=1;Database=dealersync_unavailable_test;Username=synthetic;Password=DO-NOT-EXPOSE-TEST;Timeout=1;Pooling=false";

    private static WebApplicationFactory<Program> Factory(string? connection, LogCapture? logs = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            if (logs is not null) builder.ConfigureLogging(logging => logging.AddProvider(logs));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<DealerSyncDbContext>>();
                services.AddDbContext<DealerSyncDbContext>(options => options.UseNpgsql(connection));
            });
        });

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public async Task MissingConnection_FailsStartupWithoutConnectionDetails(string? connection)
    {
        var directory = Path.Combine(Path.GetTempPath(), "dealersync-missing-config-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add(typeof(Program).Assembly.Location);
        start.ArgumentList.Add("--urls");
        start.ArgumentList.Add("http://127.0.0.1:0");
        start.Environment.Clear();
        foreach (var key in new[] {"PATH", "HOME", "DOTNET_ROOT", "DOTNET_CLI_HOME", "TMPDIR"})
            if (Environment.GetEnvironmentVariable(key) is { } value) start.Environment[key] = value;
        start.Environment["DOTNET_ENVIRONMENT"] = "Testing";
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Testing";
        if (connection is not null) start.Environment["ConnectionStrings__DefaultConnection"] = connection;
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await process.WaitForExitAsync(deadline.Token);
            Assert.NotEqual(0, process.ExitCode);
            Assert.Contains("Database connection string 'DefaultConnection' is missing.", await stderr + await stdout);
        }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("/api/sku-mappings")]
    [InlineData("/api/reconciliation/history")]
    [InlineData("/api/reconciliation?detailed=true")]
    public async Task UnavailableDatabase_ReturnsSafeFailureWithoutApprovalPayload(string path)
    {
        using var logs = new LogCapture();
        using var factory = Factory(Unavailable, logs);
        using var client = factory.CreateClient();
        using var files = new MultipartFormDataContent();
        files.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("Part Number,Description,Qty\nSYNTHETIC,Test,7\n")),
            "lightspeedFile", "lightspeed.csv");
        files.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(
            "Handle,Option 1 Value,SKU,Location,On hand (current)\nsynthetic,Default Title,SYNTHETIC,Ottawa,2\n")),
            "shopifyFile", "shopify.csv");
        using var response = path.Contains('?') ? await client.PostAsync(path, files) : await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var error = await response.Content.ReadFromJsonAsync<DatabaseError>();
        Assert.NotNull(error);
        Assert.Equal("Database unavailable. Check the local database connection and migrations, then try again.", error.Error);
        Assert.DoesNotContain("DO-NOT-EXPOSE-TEST", body);
        Assert.DoesNotContain("Npgsql", body);
        Assert.DoesNotContain("approvedInventoryExport", body);
        Assert.DoesNotContain("proposedQuantity", body);
        Assert.Contains(logs.Messages, message => message.Contains("Database operation failed; no result was returned."));
        Assert.DoesNotContain("DO-NOT-EXPOSE-TEST", string.Join('\n', logs.Messages));
    }

    [Fact]
    public async Task UntrustedOrigin_HasNoCorsPermission()
    {
        using var factory = Factory(Unavailable);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/health");
        request.Headers.Add("Origin", "https://untrusted.invalid");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    private sealed record DatabaseError(string Error);

    private sealed class LogCapture : ILoggerProvider, ILogger
    {
        public ConcurrentQueue<string> Messages { get; } = new();
        public ILogger CreateLogger(string categoryName) => this;
        public bool IsEnabled(LogLevel logLevel) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error,
            Func<TState, Exception?, string> formatter) => Messages.Enqueue(formatter(state, error) + error);
        public void Dispose() { }
    }

    [Fact]
    public async Task UntrustedHost_IsRejected()
    {
        using var factory = Factory(Unavailable);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/health");
        request.Headers.Host = "untrusted.invalid";
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
