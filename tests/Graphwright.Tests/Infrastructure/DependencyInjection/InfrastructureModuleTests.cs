using System;
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
