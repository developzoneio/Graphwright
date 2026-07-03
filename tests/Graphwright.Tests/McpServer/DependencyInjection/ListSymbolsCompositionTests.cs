using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Graphwright.Application.LanguageProviders;
using Graphwright.Domain.Exceptions;
using Graphwright.Infrastructure.DependencyInjection;
using Graphwright.Infrastructure.LanguageProviders;
using Graphwright.McpServer.DependencyInjection;
using Graphwright.McpServer.Dispatch;
using Graphwright.McpServer.Tools;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Graphwright.Tests.McpServer.DependencyInjection;

/// <summary>
/// End-to-end, composition-root-level test for <c>mcp__gitnexus__list_symbols</c> (00-spec.md
/// Scenario 10: "Workspace not ready yet"), built through the exact <c>Program.cs</c> wiring
/// path (<see cref="InfrastructureModule"/> then <see cref="McpServerModule"/>), plus a
/// structural proxy for Scenario 12 ("the server is not observed to build or re-load any part
/// of the workspace during the call"). Production wiring
/// (<see cref="InfrastructureModule.RegisterServices"/>) deliberately registers
/// <see cref="RoslynWorkspaceSnapshot.NotLoaded"/> — this is by design, per 00-spec.md "Out of
/// scope" ("Loading, opening, or warming the Roslyn workspace/compilation itself") and
/// 01-plan.md OQ-1's resolution to defer real workspace loading to a future indexer story. This
/// test class proves the container-wired path is honest about that (Scenario 10) and that
/// resolving the same snapshot twice yields the same instance with no observable mutation across
/// repeated calls (Scenario 12 proxy) — it does NOT close the Month 1 availability-probe DoD
/// item, exactly as 00-spec.md itself hedges: "this scenario ... does not itself deliver the
/// warm index."
/// </summary>
public class ListSymbolsCompositionTests
{
    private static ServiceCollection BuildRegisteredServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        InfrastructureModule.Instance.RegisterServices(services);
        McpServerModule.Instance.RegisterServices(services);

        return services;
    }

    [Fact]
    public async Task DispatchListSymbolsWithEmptyArgumentsThroughTheFullContainerReturnsWorkspaceNotLoadedEnvelope()
    {
        using var provider = BuildRegisteredServices().BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<ToolDispatcher>();
        using var arguments = JsonDocument.Parse("{}");

        var result = await dispatcher.DispatchAsync(
            GitnexusToolNames.LIST_SYMBOLS, arguments.RootElement, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.False(result.Error!.Ok);
        Assert.Equal("WORKSPACE_NOT_LOADED", result.Error.Error.Code);
        Assert.True(result.Error.Error.Retryable);
    }

    [Fact]
    public void RoslynWorkspaceSnapshotResolvesAsTheSameSingletonInstanceAcrossCalls()
    {
        using var provider = BuildRegisteredServices().BuildServiceProvider();

        var first = provider.GetRequiredService<RoslynWorkspaceSnapshot>();
        var second = provider.GetRequiredService<RoslynWorkspaceSnapshot>();

        Assert.Same(first, second);
    }

    [Fact]
    public async Task ConsecutiveListSymbolsAsyncCallsOnTheResolvedProviderBothThrowWithoutMutatingTheSnapshot()
    {
        using var provider = BuildRegisteredServices().BuildServiceProvider();
        var languageProvider = provider.GetRequiredService<ILanguageProvider>();
        var snapshot = provider.GetRequiredService<RoslynWorkspaceSnapshot>();
        var query = new ListSymbolsQuery(path: null, nameFilter: null, kinds: null, includeGenerated: false, maxResults: 50);

        await Assert.ThrowsAsync<WorkspaceNotLoadedException>(
            () => languageProvider.ListSymbolsAsync(query, CancellationToken.None));
        Assert.False(snapshot.IsLoaded);

        await Assert.ThrowsAsync<WorkspaceNotLoadedException>(
            () => languageProvider.ListSymbolsAsync(query, CancellationToken.None));
        Assert.False(snapshot.IsLoaded);
    }
}
