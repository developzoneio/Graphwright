using Graphwright.Application.LanguageProviders;
using Xunit;

namespace Graphwright.Tests.Application.LanguageProviders;

public class FileContentResultTests
{
    [Fact]
    public void ConstructorRoundTripsAllFiveProperties()
    {
        var result = new FileContentResult("a/Foo.cs", 3, 6, 20, "line 3\nline 4\nline 5\nline 6");

        Assert.Equal("a/Foo.cs", result.File);
        Assert.Equal(3, result.StartLine);
        Assert.Equal(6, result.EndLine);
        Assert.Equal(20, result.TotalLines);
        Assert.Equal("line 3\nline 4\nline 5\nline 6", result.Content);
    }
}
