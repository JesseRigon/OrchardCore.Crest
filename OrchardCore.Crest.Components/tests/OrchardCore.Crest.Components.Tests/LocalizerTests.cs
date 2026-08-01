using System.Globalization;
using Crest.Components.Primitives;
using NSubstitute;
using Xunit;

namespace Crest.Components.Tests;

public sealed class LocalizerTests
{
    [Fact]
    public void UnknownKeyFallsBackToTheKeyItself()
    {
        var localizer = new Localizer(null);

        var result = localizer.Get("ThisKeyDoesNotExist", CultureInfo.InvariantCulture);

        Assert.Equal("ThisKeyDoesNotExist", result);
    }

    [Fact]
    public void CustomLocalizerIsConsultedFirst()
    {
        var custom = Substitute.For<ILocalizer>();
        custom.Get("SomeKey", Arg.Any<CultureInfo>()).Returns("Custom translation");
        var localizer = new Localizer(custom);

        var result = localizer.Get("SomeKey", CultureInfo.InvariantCulture);

        Assert.Equal("Custom translation", result);
    }

    [Fact]
    public void CustomLocalizerReturningNullFallsBackToDefault()
    {
        var custom = Substitute.For<ILocalizer>();
        custom.Get(Arg.Any<string>(), Arg.Any<CultureInfo>()).Returns((string?)null);
        var localizer = new Localizer(custom);

        var result = localizer.Get("ThisKeyDoesNotExist", CultureInfo.InvariantCulture);

        Assert.Equal("ThisKeyDoesNotExist", result);
    }
}
