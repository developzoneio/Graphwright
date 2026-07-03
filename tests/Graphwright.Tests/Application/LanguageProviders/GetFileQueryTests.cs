using Graphwright.Application.LanguageProviders;
using Xunit;

namespace Graphwright.Tests.Application.LanguageProviders;

public class GetFileQueryTests
{
    [Fact]
    public void ConstructorRoundTripsAllFivePropertiesWhenAllOptionalLineFieldsAreSet()
    {
        var query = new GetFileQuery("a/Foo.cs", 3, 6, 10, 5);

        Assert.Equal("a/Foo.cs", query.Path);
        Assert.Equal(3, query.StartLine);
        Assert.Equal(6, query.EndLine);
        Assert.Equal(10, query.AroundLine);
        Assert.Equal(5, query.Context);
    }

    [Fact]
    public void ConstructorRoundTripsNullForAllThreeOptionalLineFields()
    {
        var query = new GetFileQuery("a/Foo.cs", null, null, null, 2);

        Assert.Equal("a/Foo.cs", query.Path);
        Assert.Null(query.StartLine);
        Assert.Null(query.EndLine);
        Assert.Null(query.AroundLine);
        Assert.Equal(2, query.Context);
    }
}
