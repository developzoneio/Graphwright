using System;
using System.Threading;
using System.Threading.Tasks;
using Graphwright.Application.LanguageProviders;
using Graphwright.Domain.Exceptions;
using Graphwright.Infrastructure.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Graphwright.Tests.Infrastructure.DependencyInjection;

public class InfrastructureModuleTests
{
    [Fact]
    public void RegisterServicesCompletesWithoutThrowingAndTheCollectionBuildsAProvider()
    {
        var services = new ServiceCollection();

        var exception = Record.Exception(() => InfrastructureModule.Instance.RegisterServices(services));

        Assert.Null(exception);
        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider);
    }

    [Fact]
    public async Task RegisteredLanguageProviderThrowsWorkspaceNotLoadedExceptionBecauseTheSnapshotIsUnloaded()
    {
        var services = new ServiceCollection();
        InfrastructureModule.Instance.RegisterServices(services);
        using var provider = services.BuildServiceProvider();

        var languageProvider = provider.GetRequiredService<ILanguageProvider>();
        var query = new ListSymbolsQuery(
            path: null, nameFilter: null, kinds: null, includeGenerated: false, maxResults: 50);

        await Assert.ThrowsAsync<WorkspaceNotLoadedException>(
            () => languageProvider.ListSymbolsAsync(query, CancellationToken.None));
    }

    [Fact]
    public void InstanceIsTheSameSingletonAcrossCalls()
    {
        Assert.Same(InfrastructureModule.Instance, InfrastructureModule.Instance);
    }

    [Fact]
    public void RegisterServicesThrowsArgumentNullExceptionWhenServicesIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => InfrastructureModule.Instance.RegisterServices(null!));
    }
}
