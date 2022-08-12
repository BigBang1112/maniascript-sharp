using ManiaScriptSharp.DocH.Inlines;
using System.Text;

namespace ManiaScriptSharp.DocH.Tests.Unit.Inlines;

public class IndexerHInlineTests
{
    [Fact]
    public void Read_ReadsIndexer()
    {
        // Arrange
        var hInline = new IndexerHInline();
        var builder = new StringBuilder();
        var match = hInline.IdentifierRegex.Match("    ElemType operator[](Integer Index);");
        var expected = $"\tpublic ElemType this[int Index] {{ get; set; }}{Environment.NewLine}";

        // Act
        hInline.Read(match, builder);

        // Assert
        Assert.Equal(expected, actual: builder.ToString());
    }
}
