using System;
using System.Diagnostics.CodeAnalysis;
using Graphwright.Application.LanguageProviders;
using Graphwright.Infrastructure.LanguageProviders;
using Microsoft.Extensions.DependencyInjection;

namespace Graphwright.Infrastructure.DependencyInjection;

/// <summary>
/// Registers Infrastructure-layer services into the composition root's
/// <see cref="IServiceCollection"/>. This is the only Infrastructure symbol McpServer may
/// reference directly (01-plan.md "Decision D2") — the composition root (Program.cs) calls
/// <see cref="Instance"/>.<see cref="RegisterServices"/> exactly once.
/// </summary>
public sealed class InfrastructureModule
{
    public static readonly InfrastructureModule Instance = new();

    private InfrastructureModule()
    {
    }

    /// <summary>
    /// Registers Infrastructure-layer services into <paramref name="services"/>. This is the
    /// first real registration: <see cref="RoslynWorkspaceSnapshot.NotLoaded"/> is registered
    /// deliberately unloaded pending a future indexer story that loads and warms a real
    /// workspace (00-spec.md "Out of scope"; 01-plan.md "Scope framing").
    /// </summary>
    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Registrar seam is deliberately an instance member per project no-static-class " +
            "convention (plan D2), mirroring McpServerModule.")]
    public void RegisterServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(RoslynWorkspaceSnapshot.NotLoaded());
        services.AddSingleton<ILanguageProvider, RoslynLanguageProvider>();
    }
}
