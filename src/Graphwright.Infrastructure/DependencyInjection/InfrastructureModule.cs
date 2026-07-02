using System;
using System.Diagnostics.CodeAnalysis;
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
    /// Registers Infrastructure-layer services into <paramref name="services"/>. Implementations
    /// arrive with the later GW-1 stories; the seam keeps Program.cs stable as Infrastructure
    /// gains real services.
    /// </summary>
    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Registrar seam is deliberately an instance member per project no-static-class " +
            "convention (plan D2); implementations arrive in later GW-1 stories.")]
    public void RegisterServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Implementations arrive with the later GW-1 stories; the seam keeps Program.cs stable.
    }
}
