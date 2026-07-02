using System;
using System.Threading.Tasks;
using Graphwright.Domain.Exceptions;
using Graphwright.Infrastructure.DependencyInjection;
using Graphwright.McpServer.DependencyInjection;
using Graphwright.McpServer.Registry;
using Graphwright.McpServer.Transport;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Graphwright.McpServer;

/// <summary>
/// Composition root. Parses the requested <see cref="TransportMode"/>, wires the layered
/// registration seams into a single host, and self-asserts the tool contract before serving a
/// single request (00-spec.md Scenario 1: "startup fails fast (non-zero exit, logged reason) if
/// the set differs in count or name").
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        TransportMode transportMode;
        try
        {
            transportMode = TransportModeParser.Instance.Parse(args);
        }
        catch (InvalidToolArgumentException ex)
        {
            await Console.Error.WriteLineAsync(ex.Message);
            return 1;
        }

        return transportMode switch
        {
            TransportMode.Stdio => await RunStdioAsync(args),
            TransportMode.Sse => await RunSseAsync(args),
            _ => throw new InvalidToolArgumentException("transport", $"Unhandled transport mode '{transportMode}'.")
        };
    }

    private static async Task<int> RunStdioAsync(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        // stdout is the MCP protocol channel for the stdio transport (00-spec.md Scenario 4);
        // logging must never write there. Pin it to stderr instead.
        ConfigureStderrLogging(builder.Logging);

        RegisterModules(builder.Services);
        McpHandlerAdapter.Instance.Configure(builder.Services.AddMcpServer()).WithStdioServerTransport();

        using var host = builder.Build();

        if (await TryAssertToolContractAsync(host.Services) == false)
        {
            return 1;
        }

        await host.RunAsync();
        return 0;
    }

    private static async Task<int> RunSseAsync(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Unlike stdio, stdout is not the MCP protocol channel over HTTP (00-spec.md
        // Scenario 4). Logging is still pinned to stderr here so operators see the same log
        // placement regardless of which --transport was chosen.
        ConfigureStderrLogging(builder.Logging);

        RegisterModules(builder.Services);
        McpHandlerAdapter.Instance.Configure(builder.Services.AddMcpServer()).WithHttpTransport();

        var app = builder.Build();
        app.MapMcp();

        if (await TryAssertToolContractAsync(app.Services) == false)
        {
            return 1;
        }

        await app.RunAsync();
        return 0;
    }

    // Decision D2 (01-plan.md): McpServer is the composition root, so it is the one place
    // permitted to touch an Infrastructure symbol directly — and only this one:
    // InfrastructureModule.Instance.RegisterServices. No other Infrastructure type may be
    // referenced from McpServer. Shared by both transport branches so the advertised tool
    // surface cannot drift between them (00-spec.md Scenario 4).
    private static void RegisterModules(IServiceCollection services)
    {
        InfrastructureModule.Instance.RegisterServices(services);
        McpServerModule.Instance.RegisterServices(services);
    }

    private static void ConfigureStderrLogging(ILoggingBuilder logging)
    {
        logging.ClearProviders();
        logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
    }

    private static async Task<bool> TryAssertToolContractAsync(IServiceProvider services)
    {
        var toolRegistry = services.GetRequiredService<ToolRegistry>();
        try
        {
            toolRegistry.AssertContract();
            return true;
        }
        catch (ToolContractViolationException ex)
        {
            await Console.Error.WriteLineAsync(ex.Message);
            return false;
        }
    }
}
