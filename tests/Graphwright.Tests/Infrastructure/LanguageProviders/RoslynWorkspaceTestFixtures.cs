using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Graphwright.Infrastructure.LanguageProviders;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Graphwright.Tests.Infrastructure.LanguageProviders;

/// <summary>
/// Test-only builder for a loaded, in-memory <see cref="RoslynWorkspaceSnapshot"/> backed by an
/// <see cref="AdhocWorkspace"/>. Confined to <c>tests/</c> — never referenced from <c>src/</c>
/// (01-plan.md Decision D2/T03: the pinned enumeration strategy relies on a real,
/// reference-resolving compilation for its fixtures). Metadata references come from the full
/// running runtime's <c>TRUSTED_PLATFORM_ASSEMBLIES</c> set, not a hand-picked assembly list, so
/// fixture compilations resolve real BCL types rather than silently producing error types
/// (01-plan.md risk row on the <c>AdhocWorkspace</c>-built <see cref="Compilation"/>).
/// </summary>
public sealed class RoslynWorkspaceTestFixtures
{
    public static readonly RoslynWorkspaceTestFixtures Instance = new();

    private RoslynWorkspaceTestFixtures()
    {
    }

    /// <summary>
    /// Builds a loaded <see cref="RoslynWorkspaceSnapshot"/> containing exactly one project with
    /// one document per <paramref name="documents"/> pair. Each pair's <c>RelativePath</c> is set
    /// verbatim as the document's <see cref="DocumentInfo.FilePath"/>; callers must supply
    /// unambiguous, non-colliding casing (01-plan.md OQ-3 sidestep — e.g. <c>a/Foo.cs</c>, never
    /// two paths differing only by case).
    /// </summary>
    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Instance member per project no-static-class convention (Instance singleton, " +
            "mirrors InfrastructureModule/ExceptionEnvelopeMapper), not a state-accessing method.")]
    public RoslynWorkspaceSnapshot BuildSnapshot(params (string RelativePath, string SourceText)[] documents)
    {
        ArgumentNullException.ThrowIfNull(documents);

        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();

        var documentInfos = documents
            .Select(document => CreateDocumentInfo(projectId, document.RelativePath, document.SourceText))
            .ToList();

        var projectInfo = ProjectInfo.Create(
            projectId,
            VersionStamp.Create(),
            name: "GraphwrightTestFixture",
            assemblyName: "GraphwrightTestFixture",
            language: LanguageNames.CSharp,
            documents: documentInfos,
            parseOptions: new CSharpParseOptions(),
            compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
            metadataReferences: CreateRuntimeMetadataReferences());

        var project = workspace.AddProject(projectInfo);

        return RoslynWorkspaceSnapshot.Loaded(project.Solution);
    }

    private static DocumentInfo CreateDocumentInfo(ProjectId projectId, string relativePath, string sourceText)
    {
        var textAndVersion = TextAndVersion.Create(SourceText.From(sourceText), VersionStamp.Create(), relativePath);

        return DocumentInfo.Create(
            DocumentId.CreateNewId(projectId),
            name: Path.GetFileName(relativePath),
            loader: TextLoader.From(textAndVersion),
            filePath: relativePath);
    }

    private static List<MetadataReference> CreateRuntimeMetadataReferences()
    {
        // Null-forgiving: TRUSTED_PLATFORM_ASSEMBLIES is always set by the .NET host for any
        // running process (01-plan.md D2 risk row pins this exact technique); a null here would
        // mean the process itself is not a normal .NET runtime host.
        var trustedPlatformAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator);

        return trustedPlatformAssemblies
            .Select(assemblyPath => (MetadataReference)MetadataReference.CreateFromFile(assemblyPath))
            .ToList();
    }
}
