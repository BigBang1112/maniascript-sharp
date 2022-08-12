using ManiaScriptSharp.DocH.Inlines;
using System.Text;

namespace ManiaScriptSharp.DocH.Tests.Unit.Inlines;

public class PropertyHInlineTests
{
    [Fact]
    public void Read_ReadsProperty_Get()
    {
        // Arrange
        var hInline = new PropertyHInline();
        var builder = new StringBuilder();
        var match = hInline.IdentifierRegex.Match("\tconst\tInteger SlotsAvailable;");
        var expected = $"\tpublic int SlotsAvailable {{ get; }}{Environment.NewLine}";

        // Act
        hInline.Read(match, builder);

        // Assert
        Assert.Equal(expected, actual: builder.ToString());
    }

    [Fact]
    public void Read_ReadsProperty_Get_TypeOwner()
    {
        // Arrange
        var hInline = new PropertyHInline();
        var builder = new StringBuilder();
        var match = hInline.IdentifierRegex.Match("\tconst\tCSmModeEvent::EType Type;");
        var expected = $"\tpublic CSmModeEvent.EType Type {{ get; }}{Environment.NewLine}";

        // Act
        hInline.Read(match, builder);

        // Assert
        Assert.Equal(expected, actual: builder.ToString());
    }

    [Fact]
    public void Read_ReadsProperty_Get_TypeIsSameAsName()
    {
        // Arrange
        var hInline = new PropertyHInline();
        var builder = new StringBuilder();
        var match = hInline.IdentifierRegex.Match("\tconst\tCSmModeEvent::Type Type;");
        var expected = $"\t[ActualName(\"Type\")] public CSmModeEvent.Type TypeE {{ get; }}{Environment.NewLine}";

        // Act
        hInline.Read(match, builder);

        // Assert
        Assert.Equal(expected, actual: builder.ToString());
    }

    [Fact]
    public void Read_ReadsProperty_Set()
    {
        // Arrange
        var hInline = new PropertyHInline();
        var builder = new StringBuilder();
        var match = hInline.IdentifierRegex.Match("\tInteger SlotsAvailable;");
        var expected = $"\tpublic int SlotsAvailable {{ get; set; }}{Environment.NewLine}";

        // Act
        hInline.Read(match, builder);

        // Assert
        Assert.Equal(expected, actual: builder.ToString());
    }
}
