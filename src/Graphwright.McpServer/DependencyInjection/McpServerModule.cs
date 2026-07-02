using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Graphwright.McpServer.Dispatch;
using Graphwright.McpServer.Registry;
using Graphwright.McpServer.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace Graphwright.McpServer.DependencyInjection;

/// <summary>
/// Registers McpServer-layer services — the 5 frozen <see cref="IGitnexusTool"/> stubs plus
/// <see cref="ToolRegistry"/> and <see cref="ToolDispatcher"/> — into the composition root's
/// <see cref="IServiceCollection"/>. This is the single registration source both the stdio and
/// SSE transports consume, so the advertised tool surface cannot drift between transports
/// (00-spec.md Scenario 4: "transport selection does not alter the tool surface or envelope
/// contract"). Mirrors the registrar shape of
/// <see cref="Graphwright.Infrastructure.DependencyInjection.InfrastructureModule"/>.
/// </summary>
/// <remarks>
/// <see cref="ToolDispatcher"/> requires
/// <see cref="Microsoft.Extensions.Logging.ILogger{TCategoryName}"/>. This module deliberately
/// does not call <c>AddLogging()</c> itself so it never forces a logging provider choice on the
/// host — the composition root (e.g. <c>Host.CreateApplicationBuilder</c>, which adds logging by
/// default) is responsible for registering one before resolving <see cref="ToolDispatcher"/>.
/// </remarks>
public sealed class McpServerModule
{
    public static readonly McpServerModule Instance = new();

    private McpServerModule()
    {
    }

    /// <summary>
    /// Registers the 5 frozen <see cref="IGitnexusTool"/> stubs as singletons, bridges them into
    /// an <see cref="IReadOnlyList{T}"/> for constructor injection, and registers
    /// <see cref="ToolRegistry"/> and <see cref="ToolDispatcher"/> into
    /// <paramref name="services"/>.
    /// </summary>
    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Registrar seam is deliberately an instance member per project no-static-class " +
            "convention (plan D2), mirroring InfrastructureModule.")]
    public void RegisterServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IGitnexusTool, ListSymbolsTool>();
        services.AddSingleton<IGitnexusTool, GetFileTool>();
        services.AddSingleton<IGitnexusTool, FindReferencesTool>();
        services.AddSingleton<IGitnexusTool, GetCallGraphTool>();
        services.AddSingleton<IGitnexusTool, SearchTool>();

        // DI natively provides IEnumerable<IGitnexusTool>; bridge it to the IReadOnlyList<>
        // shape that ToolRegistry and ToolDispatcher accept via constructor injection.
        services.AddSingleton<IReadOnlyList<IGitnexusTool>>(
            serviceProvider => serviceProvider.GetServices<IGitnexusTool>().ToList());

        services.AddSingleton<ToolRegistry>();
        services.AddSingleton<ToolDispatcher>();
    }
}
