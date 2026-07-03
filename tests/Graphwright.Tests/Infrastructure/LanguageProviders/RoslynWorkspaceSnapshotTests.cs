using System;
using System.Linq;
using System.Threading.Tasks;
using Graphwright.Infrastructure.LanguageProviders;
using Xunit;

namespace Graphwright.Tests.Infrastructure.LanguageProviders;

public class RoslynWorkspaceSnapshotTests
{
    private static readonly string[] _expectedFilePaths = { "a/Foo.cs", "b/Bar.cs" };

    [Fact]
    public void NotLoadedIsNotLoadedAndHasAnEmptySolution()
    {
        var snapshot = RoslynWorkspaceSnapshot.NotLoaded();

        Assert.False(snapshot.IsLoaded);
        Assert.Empty(snapshot.Solution.Projects);
    }

    [Fact]
    public void BuildSnapshotProducesALoadedSnapshotWithExactlyOneProjectAndExactRelativePaths()
    {
        var snapshot = RoslynWorkspaceTestFixtures.Instance.BuildSnapshot(
            ("a/Foo.cs", "namespace A { public class Foo { } }"),
            ("b/Bar.cs", "namespace B { public class Bar { } }"));

        Assert.True(snapshot.IsLoaded);
        var project = Assert.Single(snapshot.Solution.Projects);
        var actualFilePaths = project.Documents
            .Select(document => document.FilePath)
            .OrderBy(filePath => filePath, StringComparer.Ordinal);
        Assert.Equal(_expectedFilePaths, actualFilePaths);
    }

    [Fact]
    public async Task FixtureDocumentResolvesASemanticModelBackedByRealBclTypes()
    {
        var snapshot = RoslynWorkspaceTestFixtures.Instance.BuildSnapshot(
            ("a/Foo.cs", "namespace A { public class Foo { } }"));
        var project = Assert.Single(snapshot.Solution.Projects);
        var document = Assert.Single(project.Documents);

        var semanticModel = await document.GetSemanticModelAsync();

        Assert.NotNull(semanticModel);
        var taskType = semanticModel!.Compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
        Assert.NotNull(taskType);
    }
}
