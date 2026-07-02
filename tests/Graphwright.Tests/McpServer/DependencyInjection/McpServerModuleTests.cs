using System;
using System.Collections.Generic;
using System.Linq;
using Graphwright.McpServer.DependencyInjection;
using Graphwright.McpServer.Dispatch;
using Graphwright.McpServer.Registry;
using Graphwright.McpServer.Tools;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Graphwright.Tests.McpServer.DependencyInjection;

public class McpServerModuleTests
{
    private static ServiceCollection BuildRegisteredServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        McpServerModule.Instance.RegisterServices(services);

        return services;
    }

    [Fact]
    public void RegisterServicesResolvesToolRegistryContainingExactlyTheFiveFrozenNames()
    {
        using var provider = BuildRegisteredServices().BuildServiceProvider();

        var registry = provider.GetRequiredService<ToolRegistry>();

        var actualNames = registry.Tools.Select(tool => tool.Name).ToArray();
        Assert.Equal(GitnexusToolNames.FrozenOrderedNames, actualNames);
    }

    [Fact]
    public void AssertContractOnTheResolvedRegistryPasses()
    {
        using var provider = BuildRegisteredServices().BuildServiceProvider();
        var registry = provider.GetRequiredService<ToolRegistry>();

        var exception = Record.Exception(() => registry.AssertContract());

        Assert.Null(exception);
    }

    [Fact]
    public void RegisterServicesResolvesToolDispatcher()
    {
        using var provider = BuildRegisteredServices().BuildServiceProvider();

        var dispatcher = provider.GetRequiredService<ToolDispatcher>();

        Assert.NotNull(dispatcher);
    }

    [Fact]
    public void RegisterServicesResolvesReadOnlyListOfGitnexusToolsWithFiveEntries()
    {
        using var provider = BuildRegisteredServices().BuildServiceProvider();

        var tools = provider.GetRequiredService<IReadOnlyList<IGitnexusTool>>();

        Assert.Equal(5, tools.Count);
    }

    [Fact]
    public void InstanceIsTheSameSingletonAcrossCalls()
    {
        Assert.Same(McpServerModule.Instance, McpServerModule.Instance);
    }

    [Fact]
    public void RegisterServicesThrowsArgumentNullExceptionWhenServicesIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => McpServerModule.Instance.RegisterServices(null!));
    }
}
